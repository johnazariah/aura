using System.Globalization;
using Serilog;
using Serilog.Events;
using Aura.Foundation;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Aura.Module.Developer;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Configure as Windows Service when installed as service
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "AuraService";
});

// Configure Serilog for file logging
var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "Aura", "logs", "aura-.log");
var logDir = Path.GetDirectoryName(logPath);
if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
        .MinimumLevel.Override("Polly", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            formatProvider: CultureInfo.InvariantCulture);

    // On Windows, also log warnings and errors to Windows Event Log
    if (OperatingSystem.IsWindows())
    {
        configuration.WriteTo.EventLog(
            source: "Aura",
            logName: "Application",
            restrictedToMinimumLevel: LogEventLevel.Warning,
            formatProvider: CultureInfo.InvariantCulture);
    }
});

// Add Aspire service defaults (telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Add PostgreSQL with EF Core
// Connection string comes from Aspire AppHost via configuration
var connectionString = builder.Configuration.GetConnectionString("auradb");
builder.Services.AddDbContext<AuraDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector())
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Add Aura Foundation services
builder.Services.AddAuraFoundation(builder.Configuration);

// Add Developer Module
var developerModule = new DeveloperModule();
developerModule.ConfigureServices(builder.Services, builder.Configuration);

// Add CORS for the VS Code extension
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply EF Core migrations on startup (required for pgvector)
// Skip database operations in Testing environment (unit tests use stubs without a real DB)
// Integration tests use Testcontainers which handle their own migrations
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Apply Foundation migrations first
    var foundationDb = scope.ServiceProvider.GetRequiredService<AuraDbContext>();
    await ApplyMigrationsAsync(foundationDb, "Foundation", logger);

    // Apply Developer module migrations (includes its own entities)
    var developerDb = scope.ServiceProvider.GetRequiredService<Aura.Module.Developer.Data.DeveloperDbContext>();
    await ApplyMigrationsAsync(developerDb, "Developer", logger);

    // Register Developer Module tools with the tool registry
    var toolRegistry = scope.ServiceProvider.GetRequiredService<Aura.Foundation.Tools.IToolRegistry>();
    developerModule.RegisterTools(toolRegistry, scope.ServiceProvider);
    logger.LogInformation("Registered {Count} Developer Module tools", toolRegistry.GetAllTools().Count);
}

// Map Aspire default endpoints (health, alive)
app.MapDefaultEndpoints();

// Enable CORS
app.UseCors();

// Health endpoints
app.MapGet("/health", () => new
{
    status = "healthy",
    healthy = true,
    version = "0.1.0",
    timestamp = DateTime.UtcNow
});

app.MapGet("/health/db", async (AuraDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return new
        {
            healthy = canConnect,
            details = canConnect ? "Connected to PostgreSQL" : "Cannot connect",
            timestamp = DateTime.UtcNow
        };
    }
    catch (Exception ex)
    {
        return new
        {
            healthy = false,
            details = ex.Message,
            timestamp = DateTime.UtcNow
        };
    }
});

app.MapGet("/health/rag", async (IRagService ragService) =>
{
    try
    {
        var healthy = await ragService.IsHealthyAsync();
        var stats = healthy ? await ragService.GetStatsAsync() : null;
        return Results.Ok(new
        {
            healthy,
            details = healthy
                ? "RAG service operational - " + (stats?.TotalChunks ?? 0) + " chunks indexed"
                : "RAG service unavailable",
            totalDocuments = stats?.TotalDocuments ?? 0,
            totalChunks = stats?.TotalChunks ?? 0,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            healthy = false,
            details = ex.Message,
            totalDocuments = 0,
            totalChunks = 0,
            timestamp = DateTime.UtcNow
        });
    }
});

app.MapGet("/health/ollama", async (ILlmProviderRegistry registry) =>
{
    var provider = registry.GetProvider("ollama") ?? registry.GetDefaultProvider();
    if (provider is null)
    {
        return Results.Ok(new { healthy = false, details = "No LLM provider configured" });
    }

    try
    {
        var models = await provider.ListModelsAsync();
        return Results.Ok(new
        {
            healthy = true,
            details = models.Count + " models available",
            models = models.Select(m => m.Name).ToList(),
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            healthy = false,
            details = ex.Message,
            timestamp = DateTime.UtcNow
        });
    }
});

// Critical agent health check
app.MapGet("/health/agents", (IAgentRegistry registry) =>
{
    // Check foundation capabilities (always required)
    var foundationResults = Capabilities.Foundation.Select(cap =>
    {
        var agent = registry.GetBestForCapability(cap);
        return new
        {
            capability = cap,
            category = "foundation",
            available = agent is not null,
            agentId = agent?.AgentId
        };
    }).ToList();

    // Check for ingest:* capability (required for RAG)
    var ingestAgent = registry.GetBestForCapability(Capabilities.IngestWildcard);
    foundationResults.Add(new
    {
        capability = Capabilities.IngestWildcard,
        category = "foundation",
        available = ingestAgent is not null,
        agentId = ingestAgent?.AgentId
    });

    // Check developer module capabilities
    var developerResults = Capabilities.Developer.Select(cap =>
    {
        var agent = registry.GetBestForCapability(cap);
        return new
        {
            capability = cap,
            category = "developer",
            available = agent is not null,
            agentId = agent?.AgentId
        };
    }).ToList();

    var allResults = foundationResults.Concat(developerResults).ToList();
    var foundationHealthy = foundationResults.All(r => r.available);
    var developerHealthy = developerResults.All(r => r.available);
    var allHealthy = foundationHealthy && developerHealthy;

    var missingFoundation = foundationResults.Where(r => !r.available).Select(r => r.capability).ToList();
    var missingDeveloper = developerResults.Where(r => !r.available).Select(r => r.capability).ToList();

    string details;
    if (allHealthy)
    {
        details = "All critical agents available";
    }
    else if (!foundationHealthy)
    {
        details = $"Missing foundation: {string.Join(", ", missingFoundation)}";
    }
    else
    {
        details = $"Missing developer: {string.Join(", ", missingDeveloper)}";
    }

    return Results.Ok(new
    {
        healthy = allHealthy,
        foundationHealthy,
        developerHealthy,
        details,
        agents = allResults,
        timestamp = DateTime.UtcNow
    });
});

// Agent endpoints
app.MapGet("/api/agents", (IAgentRegistry registry, string? capability, string? language) =>
{
    IEnumerable<IAgent> agents = capability is not null
        ? registry.GetByCapability(capability, language)
        : registry.Agents.OrderBy(a => a.Metadata.Priority);

    return agents.Select(a => new
    {
        id = a.AgentId,
        name = a.Metadata.Name,
        description = a.Metadata.Description,
        capabilities = a.Metadata.Capabilities,
        priority = a.Metadata.Priority,
        languages = a.Metadata.Languages,
        provider = a.Metadata.Provider,
        model = a.Metadata.Model,
        tags = a.Metadata.Tags
    });
});

app.MapGet("/api/agents/best", (IAgentRegistry registry, string capability, string? language) =>
{
    var agent = registry.GetBestForCapability(capability, language);
    if (agent is null)
    {
        return Results.NotFound(new { error = "No agent found for capability '" + capability + "'" + (language is not null ? " with language '" + language + "'" : "") });
    }

    return Results.Ok(new
    {
        id = agent.AgentId,
        name = agent.Metadata.Name,
        description = agent.Metadata.Description,
        capabilities = agent.Metadata.Capabilities,
        priority = agent.Metadata.Priority,
        languages = agent.Metadata.Languages,
        provider = agent.Metadata.Provider,
        model = agent.Metadata.Model,
        tags = agent.Metadata.Tags
    });
});

app.MapGet("/api/agents/{agentId}", (string agentId, IAgentRegistry registry) =>
{
    var agent = registry.GetAgent(agentId);
    if (agent is null)
    {
        return Results.NotFound(new { error = "Agent '" + agentId + "' not found" });
    }

    return Results.Ok(new
    {
        id = agent.AgentId,
        name = agent.Metadata.Name,
        description = agent.Metadata.Description,
        capabilities = agent.Metadata.Capabilities,
        priority = agent.Metadata.Priority,
        languages = agent.Metadata.Languages,
        provider = agent.Metadata.Provider,
        model = agent.Metadata.Model,
        temperature = agent.Metadata.Temperature,
        tools = agent.Metadata.Tools,
        tags = agent.Metadata.Tags
    });
});

app.MapPost("/api/agents/{agentId}/execute", async (
    string agentId,
    ExecuteAgentRequest request,
    IAgentRegistry registry,
    AuraDbContext db,
    CancellationToken cancellationToken) =>
{
    var agent = registry.GetAgent(agentId);
    if (agent is null)
    {
        return Results.NotFound(new { error = "Agent '" + agentId + "' not found" });
    }

    // Start timing
    var stopwatch = Stopwatch.StartNew();
    var execution = new AgentExecution
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        Prompt = request.Prompt,
        Provider = agent.Metadata.Provider,
        Model = agent.Metadata.Model,
        StartedAt = DateTimeOffset.UtcNow,
    };

    var context = new AgentContext(
        Prompt: request.Prompt,
        WorkspacePath: request.WorkspacePath);

    try
    {
        var output = await agent.ExecuteAsync(context, cancellationToken);

        stopwatch.Stop();
        execution.DurationMs = stopwatch.ElapsedMilliseconds;
        execution.CompletedAt = DateTimeOffset.UtcNow;
        execution.Success = true;
        execution.Response = output.Content;
        execution.TokensUsed = output.TokensUsed;

        // Try to save execution record, but do not fail if DB is unavailable
        try
        {
            db.AgentExecutions.Add(execution);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception dbEx)
        {
            // Log but do not fail - DB might not be set up yet
            app.Logger.LogWarning(dbEx, "Failed to save execution record - database may not be initialized");
        }

        return Results.Ok(new
        {
            content = output.Content,
            tokensUsed = output.TokensUsed,
            artifacts = output.Artifacts
        });
    }
    catch (AgentException ex)
    {
        stopwatch.Stop();
        execution.DurationMs = stopwatch.ElapsedMilliseconds;
        execution.CompletedAt = DateTimeOffset.UtcNow;
        execution.Success = false;
        execution.ErrorMessage = ex.Message;

        // Try to save execution record, but do not fail if DB is unavailable
        try
        {
            db.AgentExecutions.Add(execution);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception dbEx)
        {
            app.Logger.LogWarning(dbEx, "Failed to save execution record - database may not be initialized");
        }

        return Results.BadRequest(new
        {
            error = ex.Message,
            code = ex.Code.ToString()
        });
    }
    catch (OperationCanceledException)
    {
        stopwatch.Stop();
        execution.DurationMs = stopwatch.ElapsedMilliseconds;
        execution.CompletedAt = DateTimeOffset.UtcNow;
        execution.Success = false;
        execution.ErrorMessage = "Cancelled";

        // Try to save execution record, but do not fail if DB is unavailable
        try
        {
            db.AgentExecutions.Add(execution);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception dbEx)
        {
            app.Logger.LogWarning(dbEx, "Failed to save execution record - database may not be initialized");
        }

        return Results.StatusCode(499); // Client Closed Request
    }
});

// RAG-enriched agent execution
app.MapPost("/api/agents/{agentId}/execute/rag", async (
    string agentId,
    ExecuteWithRagRequest request,
    IRagEnrichedExecutor executor,
    AuraDbContext db,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        var output = await executor.ExecuteAsync(
            agentId,
            request.Prompt,
            request.WorkspacePath,
            request.UseRag,
            request.UseCodeGraph,
            request.TopK.HasValue ? new RagQueryOptions { TopK = request.TopK.Value } : null,
            cancellationToken);

        stopwatch.Stop();

        return Results.Ok(new
        {
            content = output.Content,
            tokensUsed = output.TokensUsed,
            artifacts = output.Artifacts,
            ragEnriched = true,
            codeGraphEnriched = request.UseCodeGraph ?? true,
            durationMs = stopwatch.ElapsedMilliseconds
        });
    }
    catch (AgentException ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message,
            code = ex.Code.ToString()
        });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
});

// Agentic RAG execution - uses tools to explore codebase
app.MapPost("/api/agents/{agentId}/execute/agentic", async (
    string agentId,
    ExecuteAgenticRequest request,
    IRagEnrichedExecutor executor,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        var output = await executor.ExecuteAgenticAsync(
            agentId,
            request.Prompt,
            request.WorkspacePath,
            request.UseRag,
            request.UseCodeGraph,
            request.MaxSteps ?? 10,
            cancellationToken);

        stopwatch.Stop();

        return Results.Ok(new
        {
            content = output.Content,
            tokensUsed = output.TokensUsed,
            toolSteps = output.ToolSteps.Select(s => new
            {
                toolId = s.ToolId,
                input = s.Input,
                output = s.Output,
                success = s.Success
            }),
            stepCount = output.ToolSteps.Count,
            durationMs = stopwatch.ElapsedMilliseconds
        });
    }
    catch (AgentException ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message,
            code = ex.Code.ToString()
        });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
});

// Conversation endpoints
app.MapGet("/api/conversations", async (AuraDbContext db, int? limit) =>
{
    var query = db.Conversations
        .OrderByDescending(c => c.UpdatedAt)
        .Take(limit ?? 50);

    var conversations = await query.Select(c => new
    {
        id = c.Id,
        title = c.Title,
        agentId = c.AgentId,
        messageCount = c.Messages.Count,
        createdAt = c.CreatedAt,
        updatedAt = c.UpdatedAt
    }).ToListAsync();

    return Results.Ok(conversations);
});

app.MapGet("/api/conversations/{id:guid}", async (Guid id, AuraDbContext db) =>
{
    var conversation = await db.Conversations
        .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
        .FirstOrDefaultAsync(c => c.Id == id);

    if (conversation is null)
    {
        return Results.NotFound(new { error = "Conversation not found" });
    }

    return Results.Ok(new
    {
        id = conversation.Id,
        title = conversation.Title,
        agentId = conversation.AgentId,
        workspacePath = conversation.RepositoryPath,
        createdAt = conversation.CreatedAt,
        updatedAt = conversation.UpdatedAt,
        messages = conversation.Messages.Select(m => new
        {
            id = m.Id,
            role = m.Role.ToString().ToLowerInvariant(),
            content = m.Content,
            model = m.Model,
            tokensUsed = m.TokensUsed,
            createdAt = m.CreatedAt
        })
    });
});

app.MapPost("/api/conversations", async (CreateConversationRequest request, AuraDbContext db) =>
{
    var conversation = new Conversation
    {
        Id = Guid.NewGuid(),
        Title = request.Title ?? "New Conversation",
        AgentId = request.AgentId,
        RepositoryPath = request.WorkspacePath,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    db.Conversations.Add(conversation);
    await db.SaveChangesAsync();

    return Results.Created("/api/conversations/" + conversation.Id, new
    {
        id = conversation.Id,
        title = conversation.Title,
        agentId = conversation.AgentId
    });
});

app.MapPost("/api/conversations/{id:guid}/messages", async (
    Guid id,
    AddMessageRequest request,
    AuraDbContext db,
    IAgentRegistry registry,
    CancellationToken cancellationToken) =>
{
    var conversation = await db.Conversations.FindAsync([id], cancellationToken);
    if (conversation is null)
    {
        return Results.NotFound(new { error = "Conversation not found" });
    }

    // Add user message
    var userMessage = new Message
    {
        Id = Guid.NewGuid(),
        ConversationId = id,
        Role = MessageRole.User,
        Content = request.Content,
        CreatedAt = DateTimeOffset.UtcNow,
    };
    db.Messages.Add(userMessage);

    // Execute agent
    var agent = registry.GetAgent(conversation.AgentId);
    if (agent is null)
    {
        return Results.BadRequest(new { error = "Agent '" + conversation.AgentId + "' not found" });
    }

    var context = new AgentContext(
        Prompt: request.Content,
        WorkspacePath: conversation.RepositoryPath);

    try
    {
        var output = await agent.ExecuteAsync(context, cancellationToken);

        // Add assistant message
        var assistantMessage = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = id,
            Role = MessageRole.Assistant,
            Content = output.Content,
            Model = agent.Metadata.Model,
            TokensUsed = output.TokensUsed,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Messages.Add(assistantMessage);

        // Update conversation timestamp
        conversation.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            userMessage = new { id = userMessage.Id, role = "user", content = userMessage.Content },
            assistantMessage = new
            {
                id = assistantMessage.Id,
                role = "assistant",
                content = assistantMessage.Content,
                model = assistantMessage.Model,
                tokensUsed = assistantMessage.TokensUsed
            }
        });
    }
    catch (AgentException ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message,
            code = ex.Code.ToString()
        });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499); // Client Closed Request
    }
});

// Agent execution history
app.MapGet("/api/executions", async (AuraDbContext db, int? limit, bool? failedOnly) =>
{
    var query = db.AgentExecutions.AsQueryable();

    if (failedOnly == true)
    {
        query = query.Where(e => !e.Success);
    }

    var executions = await query
        .OrderByDescending(e => e.StartedAt)
        .Take(limit ?? 50)
        .Select(e => new
        {
            id = e.Id,
            agentId = e.AgentId,
            success = e.Success,
            durationMs = e.DurationMs,
            tokensUsed = e.TokensUsed,
            startedAt = e.StartedAt,
            errorMessage = e.ErrorMessage
        })
        .ToListAsync();

    return Results.Ok(executions);
});

// =============================================================================
// RAG Endpoints
// =============================================================================

// Index content
app.MapPost("/api/rag/index", async (
    IndexContentRequest request,
    IRagService ragService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var contentType = Enum.TryParse<RagContentType>(request.ContentType, true, out var ct)
            ? ct
            : RagContentType.PlainText;

        var content = new RagContent(request.ContentId, request.Text, contentType)
        {
            SourcePath = request.SourcePath,
            Language = request.Language,
        };

        await ragService.IndexAsync(content, cancellationToken);

        return Results.Ok(new
        {
            success = true,
            contentId = request.ContentId,
            message = "Content indexed successfully"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = ex.Message
        });
    }
});

// Query the RAG index
app.MapPost("/api/rag/query", async (
    RagQueryRequest request,
    IRagService ragService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var options = new RagQueryOptions
        {
            TopK = request.TopK ?? 5,
            MinScore = request.MinScore,
            SourcePathPrefix = request.SourcePathPrefix,
        };

        var results = await ragService.QueryAsync(request.Query, options, cancellationToken);

        return Results.Ok(new
        {
            query = request.Query,
            resultCount = results.Count,
            results = results.Select(r => new
            {
                contentId = r.ContentId,
                chunkIndex = r.ChunkIndex,
                text = r.Text,
                score = r.Score,
                sourcePath = r.SourcePath,
                contentType = r.ContentType.ToString()
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message
        });
    }
});

// Get RAG statistics
app.MapGet("/api/rag/stats", async (IRagService ragService, CancellationToken cancellationToken) =>
{
    try
    {
        var stats = await ragService.GetStatsAsync(cancellationToken);

        return Results.Ok(new
        {
            totalDocuments = stats.TotalDocuments,
            totalChunks = stats.TotalChunks,
            chunksByType = (stats.ByContentType ?? new Dictionary<RagContentType, int>()).ToDictionary(
                kv => kv.Key.ToString(),
                kv => kv.Value
            )
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message
        });
    }
});

// Remove content from index
app.MapDelete("/api/rag/{contentId}", async (
    string contentId,
    IRagService ragService,
    CancellationToken cancellationToken) =>
{
    try
    {
        // URL decode the contentId (it might be a file path)
        var decodedId = Uri.UnescapeDataString(contentId);
        var removed = await ragService.RemoveAsync(decodedId, cancellationToken);

        if (!removed)
        {
            return Results.NotFound(new
            {
                success = false,
                error = "Content not found: " + decodedId
            });
        }

        return Results.Ok(new
        {
            success = true,
            contentId = decodedId,
            message = "Content removed from index"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = ex.Message
        });
    }
});


// ==== Graph RAG Endpoints ====

// Get code graph statistics
app.MapGet("/api/graph/stats", async (
    string? repositoryPath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var stats = await graphService.GetStatsAsync(repositoryPath, ct);

        return Results.Ok(new
        {
            totalNodes = stats.TotalNodes,
            totalEdges = stats.TotalEdges,
            nodesByType = stats.NodesByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            edgesByType = stats.EdgesByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            repositoryPath = stats.RepositoryPath
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Find implementations of an interface
app.MapGet("/api/graph/implementations/{interfaceName}", async (
    string interfaceName,
    string? repositoryPath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var implementations = await graphService.FindImplementationsAsync(
            Uri.UnescapeDataString(interfaceName),
            repositoryPath,
            ct);

        return Results.Ok(new
        {
            interfaceName = interfaceName,
            count = implementations.Count,
            implementations = implementations.Select(n => new
            {
                name = n.Name,
                fullName = n.FullName,
                filePath = n.FilePath,
                lineNumber = n.LineNumber
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Find callers of a method
app.MapGet("/api/graph/callers/{methodName}", async (
    string methodName,
    string? containingType,
    string? repositoryPath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var callers = await graphService.FindCallersAsync(
            Uri.UnescapeDataString(methodName),
            containingType,
            repositoryPath,
            ct);

        return Results.Ok(new
        {
            methodName = methodName,
            containingType = containingType,
            count = callers.Count,
            callers = callers.Select(n => new
            {
                name = n.Name,
                fullName = n.FullName,
                signature = n.Signature,
                filePath = n.FilePath,
                lineNumber = n.LineNumber
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Get members of a type
app.MapGet("/api/graph/members/{typeName}", async (
    string typeName,
    string? repositoryPath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var members = await graphService.GetTypeMembersAsync(
            Uri.UnescapeDataString(typeName),
            repositoryPath,
            ct);

        return Results.Ok(new
        {
            typeName = typeName,
            count = members.Count,
            members = members.Select(n => new
            {
                name = n.Name,
                fullName = n.FullName,
                nodeType = n.NodeType.ToString(),
                signature = n.Signature,
                modifiers = n.Modifiers,
                filePath = n.FilePath,
                lineNumber = n.LineNumber
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Find types in a namespace
app.MapGet("/api/graph/namespace/{namespaceName}", async (
    string namespaceName,
    string? repositoryPath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var types = await graphService.GetTypesInNamespaceAsync(
            Uri.UnescapeDataString(namespaceName),
            repositoryPath,
            ct);

        return Results.Ok(new
        {
            namespaceName = namespaceName,
            count = types.Count,
            types = types.Select(n => new
            {
                name = n.Name,
                fullName = n.FullName,
                nodeType = n.NodeType.ToString(),
                filePath = n.FilePath,
                lineNumber = n.LineNumber
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Find nodes by name
app.MapGet("/api/graph/find/{name}", async (
    string name,
    string? nodeType,
    string? repositoryPath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        Aura.Foundation.Data.Entities.CodeNodeType? parsedNodeType = null;
        if (!string.IsNullOrEmpty(nodeType) &&
            Enum.TryParse<Aura.Foundation.Data.Entities.CodeNodeType>(nodeType, true, out var parsed))
        {
            parsedNodeType = parsed;
        }

        var nodes = await graphService.FindNodesAsync(
            Uri.UnescapeDataString(name),
            parsedNodeType,
            repositoryPath,
            ct);

        return Results.Ok(new
        {
            name = name,
            nodeType = nodeType,
            count = nodes.Count,
            nodes = nodes.Select(n => new
            {
                id = n.Id,
                name = n.Name,
                fullName = n.FullName,
                nodeType = n.NodeType.ToString(),
                filePath = n.FilePath,
                lineNumber = n.LineNumber,
                signature = n.Signature,
                modifiers = n.Modifiers
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// ==== Background Indexing Endpoints ====

// Get background indexer status
app.MapGet("/api/index/status", (Aura.Foundation.Rag.IBackgroundIndexer backgroundIndexer) =>
{
    var status = backgroundIndexer.GetStatus();
    return Results.Ok(new
    {
        queuedItems = status.QueuedItems,
        processedItems = status.ProcessedItems,
        failedItems = status.FailedItems,
        isProcessing = status.IsProcessing,
        activeJobs = status.ActiveJobs
    });
});

// Get index health for a workspace (freshness, staleness)
app.MapGet("/api/index/health", async (
    [FromQuery] string? workspacePath,
    Aura.Foundation.Data.AuraDbContext db,
    Aura.Foundation.Git.IGitService gitService,
    CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(workspacePath))
    {
        return Results.BadRequest(new { error = "workspacePath query parameter is required" });
    }

    // Normalize path
    var normalizedPath = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // Get index metadata for this workspace
    var ragIndex = await db.IndexMetadata
        .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == Aura.Foundation.Data.Entities.IndexTypes.Rag)
        .FirstOrDefaultAsync(ct);

    var graphIndex = await db.IndexMetadata
        .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == Aura.Foundation.Data.Entities.IndexTypes.Graph)
        .FirstOrDefaultAsync(ct);

    // Get current HEAD commit info
    string? currentCommitSha = null;
    DateTimeOffset? currentCommitAt = null;
    var isGitRepo = await gitService.IsRepositoryAsync(normalizedPath, ct);
    if (isGitRepo)
    {
        var headResult = await gitService.GetHeadCommitAsync(normalizedPath, ct);
        if (headResult.Success)
        {
            currentCommitSha = headResult.Value;
            var timestampResult = await gitService.GetCommitTimestampAsync(normalizedPath, currentCommitSha!, ct);
            if (timestampResult.Success)
            {
                currentCommitAt = timestampResult.Value;
            }
        }
    }

    // Calculate freshness for each index
    async Task<IndexHealthInfo> GetHealthInfo(Aura.Foundation.Data.Entities.IndexMetadata? index, string indexType)
    {
        if (index == null)
        {
            return new IndexHealthInfo
            {
                IndexType = indexType,
                Status = "not-indexed",
                IndexedAt = null,
                IndexedCommitSha = null,
                CommitsBehind = null,
                IsStale = true,
                ItemCount = 0
            };
        }

        int? commitsBehind = null;
        bool isStale = false;

        if (isGitRepo && !string.IsNullOrEmpty(index.CommitSha) && !string.IsNullOrEmpty(currentCommitSha))
        {
            if (index.CommitSha != currentCommitSha)
            {
                var countResult = await gitService.CountCommitsSinceAsync(normalizedPath, index.CommitSha, ct);
                if (countResult.Success && countResult.Value >= 0)
                {
                    commitsBehind = countResult.Value;
                    isStale = commitsBehind > 0;
                }
                else
                {
                    // Commit SHA not found (history rewritten?) - mark as stale
                    isStale = true;
                }
            }
        }
        else if (!isGitRepo)
        {
            // For non-git repos, compare timestamps (stale if older than 24 hours)
            isStale = index.IndexedAt < DateTimeOffset.UtcNow.AddHours(-24);
        }

        var status = isStale ? "stale" : "fresh";

        return new IndexHealthInfo
        {
            IndexType = indexType,
            Status = status,
            IndexedAt = index.IndexedAt,
            IndexedCommitSha = index.CommitSha,
            CommitsBehind = commitsBehind,
            IsStale = isStale,
            ItemCount = index.ItemsCreated
        };
    }

    var ragHealth = await GetHealthInfo(ragIndex, "rag");
    var graphHealth = await GetHealthInfo(graphIndex, "graph");

    // Overall status: fresh if all are fresh, stale if any stale, not-indexed if none indexed
    string overallStatus;
    if (ragHealth.Status == "not-indexed" && graphHealth.Status == "not-indexed")
    {
        overallStatus = "not-indexed";
    }
    else if (ragHealth.IsStale || graphHealth.IsStale)
    {
        overallStatus = "stale";
    }
    else
    {
        overallStatus = "fresh";
    }

    return Results.Ok(new
    {
        workspacePath = normalizedPath,
        isGitRepository = isGitRepo,
        currentCommitSha = currentCommitSha?[..Math.Min(7, currentCommitSha?.Length ?? 0)],
        currentCommitAt,
        overallStatus,
        rag = ragHealth,
        graph = graphHealth
    });
});

// Get specific job status
app.MapGet("/api/index/jobs/{jobId:guid}", (
    Guid jobId,
    Aura.Foundation.Rag.IBackgroundIndexer backgroundIndexer) =>
{
    var status = backgroundIndexer.GetJobStatus(jobId);
    Console.WriteLine($"DEBUG GetJobStatus: jobId={jobId}, status={status}, Source={status?.Source}");
    if (status is null)
    {
        return Results.NotFound(new { error = $"Job {jobId} not found" });
    }

    return Results.Ok(new
    {
        jobId = status.JobId,
        source = status.Source,
        state = status.State.ToString(),
        totalItems = status.TotalItems,
        processedItems = status.ProcessedItems,
        failedItems = status.FailedItems,
        progressPercent = status.ProgressPercent,
        startedAt = status.StartedAt,
        completedAt = status.CompletedAt,
        error = status.Error
    });
});


// ==== Workspaces API ====
// Resource-oriented workspace management
// See: .project/features/design/api-harmonization-phase1-audit.md

// List all workspaces
app.MapGet("/api/workspaces", async (
    AuraDbContext db,
    [FromQuery] int? limit,
    CancellationToken ct) =>
{
    var query = db.Workspaces.OrderByDescending(w => w.LastAccessedAt);
    var workspaces = limit.HasValue
        ? await query.Take(limit.Value).ToListAsync(ct)
        : await query.ToListAsync(ct);

    return Results.Ok(new
    {
        count = workspaces.Count,
        workspaces = workspaces.Select(w => new
        {
            id = w.Id,
            name = w.Name,
            path = w.CanonicalPath,
            status = w.Status.ToString().ToLowerInvariant(),
            createdAt = w.CreatedAt,
            lastAccessedAt = w.LastAccessedAt,
            gitRemoteUrl = w.GitRemoteUrl,
            defaultBranch = w.DefaultBranch
        })
    });
});

// Get workspace by ID
// Get workspace by ID or path
// - If idOrPath is 16 hex chars, treat as workspace ID
// - Otherwise, treat as a filesystem path and derive the ID
app.MapGet("/api/workspaces/{idOrPath}", async (
    string idOrPath,
    AuraDbContext db,
    IRagService ragService,
    ICodeGraphService codeGraphService,
    Aura.Foundation.Rag.IBackgroundIndexer backgroundIndexer,
    CancellationToken ct) =>
{
    // Determine if this is an ID or a path
    string workspaceId;
    if (WorkspaceIdGenerator.IsValidId(idOrPath))
    {
        workspaceId = idOrPath;
    }
    else
    {
        // Treat as path - URL decode and generate ID
        var decodedPath = Uri.UnescapeDataString(idOrPath);
        workspaceId = WorkspaceIdGenerator.GenerateId(decodedPath);
    }

    var workspace = await db.Workspaces.FindAsync([workspaceId], ct);
    if (workspace is null)
    {
        return Results.NotFound(new { error = $"Workspace not found: {idOrPath}", suggestedId = workspaceId });
    }

    // Update last accessed timestamp
    workspace.LastAccessedAt = DateTimeOffset.UtcNow;

    // Get stats
    var ragStats = await ragService.GetDirectoryStatsAsync(workspace.CanonicalPath, ct);
    var graphStats = await codeGraphService.GetStatsAsync(workspace.CanonicalPath, ct);

    // Check for active indexing job for this workspace
    var activeJob = backgroundIndexer.GetActiveJobs()
        .FirstOrDefault(j =>
            (j.State == IndexJobState.Queued || j.State == IndexJobState.Processing) &&
            string.Equals(
                Path.GetFullPath(j.Source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(workspace.CanonicalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase));

    // Auto-fix status: if marked as indexing but no active job, transition to ready
    if (workspace.Status == WorkspaceStatus.Indexing && activeJob is null)
    {
        workspace.Status = WorkspaceStatus.Ready;
    }

    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        id = workspace.Id,
        name = workspace.Name,
        path = workspace.CanonicalPath,
        status = workspace.Status.ToString().ToLowerInvariant(),
        errorMessage = workspace.ErrorMessage,
        createdAt = workspace.CreatedAt,
        lastAccessedAt = workspace.LastAccessedAt,
        gitRemoteUrl = workspace.GitRemoteUrl,
        defaultBranch = workspace.DefaultBranch,
        stats = new
        {
            files = ragStats?.FileCount ?? 0,
            chunks = ragStats?.ChunkCount ?? 0,
            graphNodes = graphStats.TotalNodes,
            graphEdges = graphStats.TotalEdges
        },
        indexingJob = activeJob is null ? null : new
        {
            jobId = activeJob.JobId,
            state = activeJob.State.ToString(),
            processedItems = activeJob.ProcessedItems,
            totalItems = activeJob.TotalItems,
            progressPercent = activeJob.ProgressPercent
        }
    });
});

// Onboard a new workspace (or get existing one)
app.MapPost("/api/workspaces", async (
    CreateWorkspaceRequest request,
    AuraDbContext db,
    Aura.Foundation.Rag.IBackgroundIndexer backgroundIndexer,
    Aura.Foundation.Git.IGitService gitService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.BadRequest(new { error = "path is required" });
    }

    if (!Directory.Exists(request.Path))
    {
        return Results.NotFound(new { error = $"Directory not found: {request.Path}" });
    }

    var normalizedPath = PathNormalizer.Normalize(Path.GetFullPath(request.Path));
    var workspaceId = WorkspaceIdGenerator.GenerateId(request.Path);
    var directoryName = Path.GetFileName(Path.GetFullPath(request.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "Workspace";

    // Check if workspace already exists
    var existing = await db.Workspaces.FindAsync([workspaceId], ct);
    if (existing is not null)
    {
        existing.LastAccessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id = existing.Id,
            name = existing.Name,
            path = existing.CanonicalPath,
            status = existing.Status.ToString().ToLowerInvariant(),
            isNew = false,
            message = "Workspace already exists"
        });
    }

    // Get git info if available
    string? gitRemoteUrl = null;
    string? defaultBranch = null;
    var isRepo = await gitService.IsRepositoryAsync(request.Path, ct);
    if (isRepo)
    {
        try
        {
            var gitResult = await gitService.GetStatusAsync(request.Path, ct);
            if (gitResult.Success && gitResult.Value is not null)
            {
                defaultBranch = gitResult.Value.CurrentBranch;
            }
            // TODO: Get remote URL
        }
        catch
        {
            // Ignore git errors - it's optional info
        }
    }

    // Create workspace
    var workspace = new Workspace
    {
        Id = workspaceId,
        CanonicalPath = normalizedPath,
        Name = request.Name ?? directoryName,
        Status = WorkspaceStatus.Pending,
        GitRemoteUrl = gitRemoteUrl,
        DefaultBranch = defaultBranch
    };

    db.Workspaces.Add(workspace);
    await db.SaveChangesAsync(ct);

    // Start indexing if requested
    Guid? jobId = null;
    if (request.StartIndexing ?? true)
    {
        var originalPath = Path.GetFullPath(request.Path);
        var options = new RagIndexOptions
        {
            IncludePatterns = request.Options?.IncludePatterns,
            ExcludePatterns = request.Options?.ExcludePatterns,
            Recursive = true,
            PreferGitTrackedFiles = true
        };

        var (id, _) = backgroundIndexer.QueueDirectory(originalPath, options);
        jobId = id;

        workspace.Status = WorkspaceStatus.Indexing;
        await db.SaveChangesAsync(ct);
    }

    return Results.Created($"/api/workspaces/{workspaceId}", new
    {
        id = workspace.Id,
        name = workspace.Name,
        path = workspace.CanonicalPath,
        status = workspace.Status.ToString().ToLowerInvariant(),
        isNew = true,
        jobId,
        message = jobId.HasValue ? "Workspace created and indexing started" : "Workspace created"
    });
});

// Trigger re-indexing for an existing workspace
app.MapPost("/api/workspaces/{id}/reindex", async (
    string id,
    AuraDbContext db,
    Aura.Foundation.Rag.IBackgroundIndexer backgroundIndexer,
    CancellationToken ct) =>
{
    if (!WorkspaceIdGenerator.IsValidId(id))
    {
        return Results.BadRequest(new { error = "Invalid workspace ID format" });
    }

    var workspace = await db.Workspaces.FindAsync([id], ct);
    if (workspace is null)
    {
        return Results.NotFound(new { error = $"Workspace not found: {id}" });
    }

    // Queue for background indexing
    var options = new Aura.Foundation.Rag.RagIndexOptions { Recursive = true };
    var (jobId, isNew) = backgroundIndexer.QueueDirectory(workspace.CanonicalPath, options);

    // Update workspace status
    workspace.Status = WorkspaceStatus.Indexing;
    workspace.LastAccessedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);

    return Results.Accepted($"/api/index/jobs/{jobId}", new
    {
        id = workspace.Id,
        path = workspace.CanonicalPath,
        status = "indexing",
        jobId,
        isNewJob = isNew,
        message = isNew ? "Re-indexing started" : "Indexing already in progress"
    });
});

// Delete workspace (remove all indexed data)
app.MapDelete("/api/workspaces/{id}", async (
    string id,
    AuraDbContext db,
    ICodeGraphService codeGraphService,
    CancellationToken ct) =>
{
    if (!WorkspaceIdGenerator.IsValidId(id))
    {
        return Results.BadRequest(new { error = "Invalid workspace ID format" });
    }

    var workspace = await db.Workspaces.FindAsync([id], ct);
    if (workspace is null)
    {
        return Results.NotFound(new { error = $"Workspace not found: {id}" });
    }

    var originalPath = workspace.CanonicalPath;

    // Delete RAG chunks for this workspace
    var chunksToDelete = await db.RagChunks
        .Where(c => c.SourcePath != null && c.SourcePath.StartsWith(originalPath))
        .ToListAsync(ct);
    db.RagChunks.RemoveRange(chunksToDelete);

    // Delete code graph data
    await codeGraphService.ClearRepositoryGraphAsync(originalPath, ct);

    // Delete index metadata
    var metadataToDelete = await db.IndexMetadata
        .Where(i => i.WorkspacePath == originalPath)
        .ToListAsync(ct);
    db.IndexMetadata.RemoveRange(metadataToDelete);

    // Delete the workspace itself
    db.Workspaces.Remove(workspace);

    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        success = true,
        message = "Workspace deleted",
        deletedChunks = chunksToDelete.Count,
        deletedMetadata = metadataToDelete.Count
    });
});


// ==== Tool Endpoints ====

// List all tools
app.MapGet("/api/tools", (Aura.Foundation.Tools.IToolRegistry toolRegistry) =>
{
    var tools = toolRegistry.GetAllTools();
    return Results.Ok(new
    {
        count = tools.Count,
        tools = tools.Select(t => new
        {
            toolId = t.ToolId,
            name = t.Name,
            description = t.Description,
            categories = t.Categories,
            requiresConfirmation = t.RequiresConfirmation,
            inputSchema = t.InputSchema
        })
    });
});

// Execute a tool
app.MapPost("/api/tools/{toolId}/execute", async (
    string toolId,
    ExecuteToolRequest request,
    Aura.Foundation.Tools.IToolRegistry toolRegistry,
    CancellationToken ct) =>
{
    var input = new Aura.Foundation.Tools.ToolInput
    {
        ToolId = toolId,
        WorkingDirectory = request.WorkingDirectory,
        Parameters = request.Parameters ?? new Dictionary<string, object?>()
    };

    var result = await toolRegistry.ExecuteAsync(input, ct);

    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            output = result.Output,
            duration = result.Duration.TotalMilliseconds
        });
    }

    return Results.BadRequest(new
    {
        success = false,
        error = result.Error,
        duration = result.Duration.TotalMilliseconds
    });
});

// Execute a ReAct-based task with tools
app.MapPost("/api/tools/react", async (
    ReActExecuteRequest request,
    Aura.Foundation.Tools.IToolRegistry toolRegistry,
    Aura.Foundation.Tools.IReActExecutor reactExecutor,
    Aura.Foundation.Llm.ILlmProviderRegistry llmRegistry,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    logger.LogInformation("ReAct execution starting: {Task}", request.Task);

    // Get the LLM provider
    var llm = llmRegistry.GetProvider(request.Provider ?? "ollama");
    if (llm is null)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = $"LLM provider '{request.Provider ?? "ollama"}' not found"
        });
    }

    // Get available tools
    var tools = request.ToolIds is not null && request.ToolIds.Count > 0
        ? toolRegistry.GetAllTools().Where(t => request.ToolIds.Contains(t.ToolId)).ToList()
        : toolRegistry.GetAllTools();

    if (tools.Count == 0)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "No tools available for execution"
        });
    }

    var options = new Aura.Foundation.Tools.ReActOptions
    {
        MaxSteps = request.MaxSteps ?? 10,
        Model = request.Model, // null = use provider's default from config
        Temperature = request.Temperature ?? 0.2,
        WorkingDirectory = request.WorkingDirectory,
        AdditionalContext = request.Context,
        RequireConfirmation = false, // API mode doesn't support confirmation
    };

    try
    {
        var result = await reactExecutor.ExecuteAsync(
            request.Task,
            tools,
            llm,
            options,
            ct);

        return Results.Ok(new
        {
            success = result.Success,
            finalAnswer = result.FinalAnswer,
            error = result.Error,
            totalSteps = result.Steps.Count,
            totalTokensUsed = result.TotalTokensUsed,
            durationMs = result.TotalDuration.TotalMilliseconds,
            steps = result.Steps.Select(s => new
            {
                stepNumber = s.StepNumber,
                thought = s.Thought,
                action = s.Action,
                actionInput = s.ActionInput,
                observation = s.Observation.Length > 2000
                    ? s.Observation[..2000] + "... (truncated)"
                    : s.Observation,
                durationMs = s.Duration.TotalMilliseconds
            })
        });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499); // Client Closed Request
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "ReAct execution failed for task: {Task}", request.Task);
        return Results.BadRequest(new
        {
            success = false,
            error = ex.Message
        });
    }
});

// ==== Git Endpoints ====

// Get git status
app.MapGet("/api/git/status", async (
    string path,
    Aura.Foundation.Git.IGitService gitService,
    CancellationToken ct) =>
{
    var result = await gitService.GetStatusAsync(path, ct);

    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            branch = result.Value!.CurrentBranch,
            isDirty = result.Value.IsDirty,
            modifiedFiles = result.Value.ModifiedFiles,
            untrackedFiles = result.Value.UntrackedFiles,
            stagedFiles = result.Value.StagedFiles
        });
    }

    return Results.BadRequest(new { success = false, error = result.Error });
});

// Create a branch
app.MapPost("/api/git/branch", async (
    CreateBranchRequest request,
    Aura.Foundation.Git.IGitService gitService,
    CancellationToken ct) =>
{
    var result = await gitService.CreateBranchAsync(
        request.RepoPath,
        request.BranchName,
        request.BaseBranch,
        ct);

    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            branch = result.Value!.Name,
            isCurrent = result.Value.IsCurrent
        });
    }

    return Results.BadRequest(new { success = false, error = result.Error });
});

// Commit changes
app.MapPost("/api/git/commit", async (
    CommitRequest request,
    Aura.Foundation.Git.IGitService gitService,
    CancellationToken ct) =>
{
    // Manual API commits respect hooks (skipHooks: false)
    var result = await gitService.CommitAsync(request.RepoPath, request.Message, skipHooks: false, ct);

    if (result.Success)
    {
        return Results.Ok(new { success = true, sha = result.Value });
    }

    return Results.BadRequest(new { success = false, error = result.Error });
});

// ==== Worktree Endpoints ====

// List worktrees
app.MapGet("/api/git/worktrees", async (
    string repoPath,
    Aura.Foundation.Git.IGitWorktreeService worktreeService,
    CancellationToken ct) =>
{
    var result = await worktreeService.ListAsync(repoPath, ct);

    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            worktrees = result.Value!.Select(w => new
            {
                path = w.Path,
                branch = w.Branch,
                commitSha = w.CommitSha,
                isMainWorktree = w.IsMainWorktree,
                isLocked = w.IsLocked,
                lockReason = w.LockReason
            })
        });
    }

    return Results.BadRequest(new { success = false, error = result.Error });
});

// Create worktree
app.MapPost("/api/git/worktrees", async (
    CreateWorktreeRequest request,
    Aura.Foundation.Git.IGitWorktreeService worktreeService,
    CancellationToken ct) =>
{
    var result = await worktreeService.CreateAsync(
        request.RepoPath,
        request.BranchName,
        request.WorktreePath,
        request.BaseBranch,
        ct);

    if (result.Success)
    {
        return Results.Ok(new
        {
            success = true,
            path = result.Value!.Path,
            branch = result.Value.Branch,
            commitSha = result.Value.CommitSha
        });
    }

    return Results.BadRequest(new { success = false, error = result.Error });
});

// Remove worktree
app.MapDelete("/api/git/worktrees", async (
    string path,
    bool? force,
    Aura.Foundation.Git.IGitWorktreeService worktreeService,
    CancellationToken ct) =>
{
    var result = await worktreeService.RemoveAsync(path, force ?? false, ct);

    if (result.Success)
    {
        return Results.Ok(new { success = true, message = "Worktree removed" });
    }

    return Results.BadRequest(new { success = false, error = result.Error });
});

// =============================================================================
// Developer Module Endpoints
// =============================================================================

// Workflow endpoints
app.MapPost("/api/developer/workflows", async (
    CreateWorkflowRequest request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    // Validate required fields
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Title is required. Expected: { title: string, description?: string, repositoryPath?: string }" });
    }

    try
    {
        var workflow = await workflowService.CreateAsync(
            request.Title,
            request.Description,
            request.RepositoryPath,
            ct);

        return Results.Created($"/api/developer/workflows/{workflow.Id}", new
        {
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            createdAt = workflow.CreatedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/developer/workflows", async (
    IWorkflowService workflowService,
    string? status,
    string? repositoryPath,
    CancellationToken ct) =>
{
    WorkflowStatus? statusFilter = null;
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<WorkflowStatus>(status, true, out var s))
    {
        statusFilter = s;
    }

    var workflows = await workflowService.ListAsync(statusFilter, repositoryPath, ct);

    return Results.Ok(new
    {
        count = workflows.Count,
        workflows = workflows.Select(w => new
        {
            id = w.Id,
            title = w.Title,
            description = w.Description,
            status = w.Status.ToString(),
            gitBranch = w.GitBranch,
            repositoryPath = w.RepositoryPath,
            worktreePath = w.WorktreePath,
            stepCount = w.Steps.Count,
            completedSteps = w.Steps.Count(s => s.Status == StepStatus.Completed),
            createdAt = w.CreatedAt,
            updatedAt = w.UpdatedAt
        })
    });
});

app.MapGet("/api/developer/workflows/{id:guid}", async (
    Guid id,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    var workflow = await workflowService.GetByIdWithStepsAsync(id, ct);
    if (workflow is null)
    {
        return Results.NotFound(new { error = $"Workflow {id} not found" });
    }

    return Results.Ok(new
    {
        id = workflow.Id,
        title = workflow.Title,
        description = workflow.Description,
        status = workflow.Status.ToString(),
        gitBranch = workflow.GitBranch,
        worktreePath = workflow.WorktreePath,
        repositoryPath = workflow.RepositoryPath,
        analyzedContext = workflow.AnalyzedContext,
        executionPlan = workflow.ExecutionPlan,
        steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
        {
            id = s.Id,
            order = s.Order,
            name = s.Name,
            capability = s.Capability,
            language = s.Language,
            description = s.Description,
            status = s.Status.ToString(),
            assignedAgentId = s.AssignedAgentId,
            attempts = s.Attempts,
            output = s.Output,
            error = s.Error,
            startedAt = s.StartedAt,
            completedAt = s.CompletedAt,
            needsRework = s.NeedsRework,
            previousOutput = s.PreviousOutput,
            approval = s.Approval?.ToString()
        }),
        createdAt = workflow.CreatedAt,
        updatedAt = workflow.UpdatedAt,
        completedAt = workflow.CompletedAt,
        pullRequestUrl = workflow.PullRequestUrl
    });
});

app.MapDelete("/api/developer/workflows/{id:guid}", async (
    Guid id,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    await workflowService.DeleteAsync(id, ct);
    return Results.NoContent();
});

app.MapPost("/api/developer/workflows/{id:guid}/analyze", async (
    Guid id,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var workflow = await workflowService.AnalyzeAsync(id, ct);
        return Results.Ok(new
        {
            id = workflow.Id,
            status = workflow.Status.ToString(),
            analyzedContext = workflow.AnalyzedContext,
            message = "Workflow analyzed successfully"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/developer/workflows/{id:guid}/plan", async (
    Guid id,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var workflow = await workflowService.PlanAsync(id, ct);
        return Results.Ok(new
        {
            id = workflow.Id,
            status = workflow.Status.ToString(),
            stepCount = workflow.Steps.Count,
            steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id,
                order = s.Order,
                name = s.Name,
                capability = s.Capability,
                language = s.Language,
                description = s.Description
            }),
            message = "Workflow planned successfully"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/execute", async (
    Guid workflowId,
    Guid stepId,
    ExecuteStepRequest? request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.ExecuteStepAsync(workflowId, stepId, request?.AgentId, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            status = step.Status.ToString(),
            assignedAgentId = step.AssignedAgentId,
            output = step.Output,
            attempts = step.Attempts,
            startedAt = step.StartedAt,
            completedAt = step.CompletedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/developer/workflows/{id:guid}/steps", async (
    Guid id,
    AddStepRequest request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.AddStepAsync(
            id,
            request.Name,
            request.Capability,
            request.Description,
            request.AfterOrder,
            ct);

        return Results.Created($"/api/developer/workflows/{id}/steps/{step.Id}", new
        {
            id = step.Id,
            order = step.Order,
            name = step.Name,
            capability = step.Capability,
            description = step.Description,
            status = step.Status.ToString()
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}", async (
    Guid workflowId,
    Guid stepId,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    await workflowService.RemoveStepAsync(workflowId, stepId, ct);
    return Results.NoContent();
});

// Approve step output
app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/approve", async (
    Guid workflowId,
    Guid stepId,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.ApproveStepAsync(workflowId, stepId, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            approval = step.Approval?.ToString()
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Reject step output (request revision)
app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/reject", async (
    Guid workflowId,
    Guid stepId,
    RejectStepRequest? request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.RejectStepAsync(workflowId, stepId, request?.Feedback, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            approval = step.Approval?.ToString(),
            approvalFeedback = step.ApprovalFeedback
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Skip step
app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/skip", async (
    Guid workflowId,
    Guid stepId,
    SkipStepRequest? request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.SkipStepAsync(workflowId, stepId, request?.Reason, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            status = step.Status.ToString(),
            skipReason = step.SkipReason
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Reset step - reset any step back to pending for re-execution
app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/reset", async (
    Guid workflowId,
    Guid stepId,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.ResetStepAsync(workflowId, stepId, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            status = step.Status.ToString()
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Step chat - interact with agent before/after execution
app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/chat", async (
    Guid workflowId,
    Guid stepId,
    StepChatRequest request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var (step, response) = await workflowService.ChatWithStepAsync(workflowId, stepId, request.Message, ct);
        return Results.Ok(new
        {
            stepId = step.Id,
            response = response,
            updatedDescription = step.Description
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Reassign step to different agent
app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/reassign", async (
    Guid workflowId,
    Guid stepId,
    ReassignStepRequest request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.ReassignStepAsync(workflowId, stepId, request.AgentId, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            agentId = step.AssignedAgentId,
            needsRework = step.NeedsRework
        });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Update step description
app.MapPut("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/description", async (
    Guid workflowId,
    Guid stepId,
    UpdateStepDescriptionRequest request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var step = await workflowService.UpdateStepDescriptionAsync(workflowId, stepId, request.Description, ct);
        return Results.Ok(new
        {
            id = step.Id,
            name = step.Name,
            description = step.Description,
            needsRework = step.NeedsRework
        });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/developer/workflows/{id:guid}/complete", async (
    Guid id,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var workflow = await workflowService.CompleteAsync(id, ct);
        return Results.Ok(new
        {
            id = workflow.Id,
            status = workflow.Status.ToString(),
            completedAt = workflow.CompletedAt,
            pullRequestUrl = workflow.PullRequestUrl,
            message = "Workflow completed successfully"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/developer/workflows/{id:guid}/cancel", async (
    Guid id,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var workflow = await workflowService.CancelAsync(id, ct);
        return Results.Ok(new
        {
            id = workflow.Id,
            status = workflow.Status.ToString(),
            message = "Workflow cancelled"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Finalize workflow - commit changes, push branch, and create PR
app.MapPost("/api/developer/workflows/{id:guid}/finalize", async (
    Guid id,
    FinalizeWorkflowRequest request,
    IWorkflowService workflowService,
    IGitService gitService,
    CancellationToken ct) =>
{
    try
    {
        // Get workflow to find worktree path
        var workflow = await workflowService.GetByIdWithStepsAsync(id, ct);
        if (workflow is null)
            return Results.NotFound(new { error = "Workflow not found" });

        if (string.IsNullOrEmpty(workflow.WorktreePath))
            return Results.BadRequest(new { error = "Workflow has no worktree path" });

        string? commitSha = null;
        string? prUrl = null;
        int? prNumber = null;

        // 1. Check for changes and commit if needed
        var statusResult = await gitService.GetStatusAsync(workflow.WorktreePath, ct);
        if (statusResult.Success && statusResult.Value?.IsDirty == true)
        {
            var commitMessage = request.CommitMessage ?? $"feat: {workflow.Title}";
            // Skip hooks for automated workflow commits
            var commitResult = await gitService.CommitAsync(workflow.WorktreePath, commitMessage, skipHooks: true, ct);
            if (!commitResult.Success)
                return Results.BadRequest(new { error = $"Commit failed: {commitResult.Error}" });

            commitSha = commitResult.Value;
        }

        // 2. Push the branch
        var pushResult = await gitService.PushAsync(workflow.WorktreePath, setUpstream: true, ct);
        if (!pushResult.Success)
            return Results.BadRequest(new { error = $"Push failed: {pushResult.Error}" });

        // 3. Create PR if requested
        if (request.CreatePullRequest)
        {
            var prTitle = request.PrTitle ?? workflow.Title;
            var prBody = request.PrBody ?? BuildPrBody(workflow);

            var prResult = await gitService.CreatePullRequestAsync(
                workflow.WorktreePath,
                prTitle,
                prBody,
                request.BaseBranch,
                request.Draft,
                ct);

            if (!prResult.Success)
                return Results.BadRequest(new { error = $"PR creation failed: {prResult.Error}" });

            prUrl = prResult.Value?.Url;
            prNumber = prResult.Value?.Number;
        }

        // 4. Mark workflow as completed if not already
        if (workflow.Status != WorkflowStatus.Completed)
        {
            await workflowService.CompleteAsync(id, ct);
        }

        return Results.Ok(new
        {
            workflowId = workflow.Id,
            commitSha,
            pushed = true,
            prNumber,
            prUrl,
            message = prUrl is not null
                ? $"Workflow finalized. PR created: {prUrl}"
                : "Workflow finalized and pushed."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

static string BuildPrBody(Workflow workflow)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"## {workflow.Title}");
    sb.AppendLine();
    if (!string.IsNullOrEmpty(workflow.Description))
    {
        sb.AppendLine(workflow.Description);
        sb.AppendLine();
    }
    sb.AppendLine("### Workflow Steps");
    sb.AppendLine();
    foreach (var step in workflow.Steps.OrderBy(s => s.Order))
    {
        var status = step.Status switch
        {
            StepStatus.Completed => "✅",
            StepStatus.Skipped => "⏭",
            StepStatus.Failed => "❌",
            _ => "⬜"
        };
        sb.AppendLine($"- {status} {step.Name}");
    }
    sb.AppendLine();
    sb.AppendLine("---");
    sb.AppendLine("*Created by [Aura](https://github.com/johnazariah/aura)*");
    return sb.ToString();
}

app.MapPost("/api/developer/workflows/{id:guid}/chat", async (
    Guid id,
    WorkflowChatRequest request,
    IWorkflowService workflowService,
    CancellationToken ct) =>
{
    try
    {
        var response = await workflowService.ChatAsync(id, request.Message, ct);
        return Results.Ok(new
        {
            response = response.Response,
            planModified = response.PlanModified,
            stepsAdded = response.StepsAdded.Select(s => new
            {
                id = s.Id,
                order = s.Order,
                name = s.Name,
                capability = s.Capability
            }),
            stepsRemoved = response.StepsRemoved,
            analysisUpdated = response.AnalysisUpdated
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Run startup tasks before starting the app
var startupRunner = app.Services.GetRequiredService<Aura.Foundation.Startup.StartupTaskRunner>();
await startupRunner.RunAsync();

app.Run();

// Request models
record ExecuteAgentRequest(string Prompt, string? WorkspacePath = null);
record ExecuteWithRagRequest(
    string Prompt,
    string? WorkspacePath = null,
    bool? UseRag = null,
    bool? UseCodeGraph = null,
    int? TopK = null);
record ExecuteAgenticRequest(
    string Prompt,
    string? WorkspacePath = null,
    bool? UseRag = null,
    bool? UseCodeGraph = null,
    int? MaxSteps = null);
record CreateConversationRequest(string AgentId, string? Title = null, string? WorkspacePath = null);
record AddMessageRequest(string Content);

// RAG request models
record IndexContentRequest(
    string ContentId,
    string Text,
    string? ContentType = null,
    string? SourcePath = null,
    string? Language = null);

record RagQueryRequest(
    string Query,
    int? TopK = null,
    double? MinScore = null,
    string? SourcePathPrefix = null);

// Tool request models
record ExecuteToolRequest(
    string? WorkingDirectory = null,
    Dictionary<string, object?>? Parameters = null);

record ReActExecuteRequest(
    string Task,
    string? WorkingDirectory = null,
    string? Provider = null,
    string? Model = null,
    double? Temperature = null,
    int? MaxSteps = null,
    string? Context = null,
    IReadOnlyList<string>? ToolIds = null);

// Git request models
record CreateBranchRequest(
    string RepoPath,
    string BranchName,
    string? BaseBranch = null);

record CommitRequest(
    string RepoPath,
    string Message);

record CreateWorktreeRequest(
    string RepoPath,
    string BranchName,
    string? WorktreePath = null,
    string? BaseBranch = null);

// Developer Module request models
record CreateWorkflowRequest(
    string? Title = null,
    string? Description = null,
    string? RepositoryPath = null);

record ExecuteStepRequest(
    string? AgentId = null);

record AddStepRequest(
    string Name,
    string Capability,
    string? Description = null,
    int? AfterOrder = null);

record RejectStepRequest(
    string? Feedback = null);

record SkipStepRequest(
    string? Reason = null);

record StepChatRequest(
    string Message);

record ReassignStepRequest(
    string AgentId);

record UpdateStepDescriptionRequest(
    string Description);

record WorkflowChatRequest(
    string Message);

record FinalizeWorkflowRequest(
    string? CommitMessage = null,
    bool CreatePullRequest = true,
    string? PrTitle = null,
    string? PrBody = null,
    string? BaseBranch = null,
    bool Draft = true);

// Create workspace request
record CreateWorkspaceRequest(
    string Path,
    string? Name = null,
    bool? StartIndexing = true,
    WorkspaceIndexingOptions? Options = null);

record WorkspaceIndexingOptions(
    IReadOnlyList<string>? IncludePatterns = null,
    IReadOnlyList<string>? ExcludePatterns = null);

// Index health response models
record IndexHealthInfo
{
    public required string IndexType { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? IndexedAt { get; init; }
    public string? IndexedCommitSha { get; init; }
    public int? CommitsBehind { get; init; }
    public bool IsStale { get; init; }
    public int ItemCount { get; init; }
}

public partial class Program
{
    /// <summary>
    /// Applies database migrations on startup.
    /// For v1, we use simple migrations - clean installs are required.
    /// </summary>
    private static async Task ApplyMigrationsAsync(DbContext db, string moduleName, Microsoft.Extensions.Logging.ILogger logger)
    {
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count == 0)
        {
            logger.LogInformation("{Module} database is up to date", moduleName);
            return;
        }

        logger.LogInformation("Applying {Count} {Module} migrations: {Migrations}",
            pendingMigrations.Count, moduleName, string.Join(", ", pendingMigrations));

        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("{Module} migrations complete", moduleName);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07" || ex.SqlState == "42701")
        {
            // Table or column already exists - database is in inconsistent state
            logger.LogError(ex,
                "{Module} migration failed: {Message}. Database may need to be reset. " +
                "Stop services, drop database with: psql -h 127.0.0.1 -p 5433 -U postgres -c \"DROP DATABASE auradb; CREATE DATABASE auradb;\" " +
                "then: psql -h 127.0.0.1 -p 5433 -U postgres -d auradb -c \"CREATE EXTENSION vector;\" and restart.",
                moduleName, ex.MessageText);
            throw;
        }
    }
}

