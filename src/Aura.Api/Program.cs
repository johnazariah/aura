using System.Globalization;
using Serilog;
using Serilog.Events;
using Aura.Foundation;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Aura.Module.Developer;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;
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
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            formatProvider: CultureInfo.InvariantCulture);
});

// Add Aspire service defaults (telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Add PostgreSQL with EF Core
// Connection string comes from Aspire AppHost via configuration
var connectionString = builder.Configuration.GetConnectionString("auradb");
builder.Services.AddDbContext<AuraDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

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
    var db = scope.ServiceProvider.GetRequiredService<AuraDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying database migrations...");
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");

        // Ensure Developer module tables exist (EnsureCreated doesn't work after Migrate)
        // Use raw SQL to create tables if they don't exist
        var developerDb = scope.ServiceProvider.GetRequiredService<DeveloperDbContext>();
        await developerDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS workflows (
                id UUID PRIMARY KEY,
                title VARCHAR(1000) NOT NULL,
                description TEXT,
                repository_path VARCHAR(1000),
                status VARCHAR(20) NOT NULL,
                workspace_path VARCHAR(1000),
                git_branch VARCHAR(500),
                analyzed_context JSONB,
                execution_plan JSONB,
                created_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL,
                completed_at TIMESTAMPTZ
            );
            
            CREATE INDEX IF NOT EXISTS ix_workflows_status ON workflows(status);
            CREATE INDEX IF NOT EXISTS ix_workflows_created_at ON workflows(created_at);
            
            CREATE TABLE IF NOT EXISTS workflow_steps (
                id UUID PRIMARY KEY,
                workflow_id UUID NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
                "order" INTEGER NOT NULL,
                name VARCHAR(500) NOT NULL,
                capability VARCHAR(100) NOT NULL,
                language VARCHAR(50),
                description TEXT,
                status VARCHAR(20) NOT NULL,
                assigned_agent_id VARCHAR(100),
                input JSONB,
                output JSONB,
                error TEXT,
                attempts INTEGER NOT NULL DEFAULT 0,
                started_at TIMESTAMPTZ,
                completed_at TIMESTAMPTZ
            );
            
            CREATE INDEX IF NOT EXISTS ix_workflow_steps_workflow_order ON workflow_steps(workflow_id, "order");
            CREATE INDEX IF NOT EXISTS ix_workflow_steps_status ON workflow_steps(status);
            
            -- Add new columns for assisted workflow UI (idempotent)
            ALTER TABLE workflow_steps ADD COLUMN IF NOT EXISTS approval INTEGER;
            ALTER TABLE workflow_steps ADD COLUMN IF NOT EXISTS approval_feedback TEXT;
            ALTER TABLE workflow_steps ADD COLUMN IF NOT EXISTS skip_reason TEXT;
            ALTER TABLE workflow_steps ADD COLUMN IF NOT EXISTS chat_history JSONB;
            ALTER TABLE workflow_steps ADD COLUMN IF NOT EXISTS needs_rework BOOLEAN NOT NULL DEFAULT false;
            ALTER TABLE workflow_steps ADD COLUMN IF NOT EXISTS previous_output JSONB;
            """);
        logger.LogInformation("Developer module tables ensured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations");
        throw;
    }

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
            request.TopK.HasValue ? new RagQueryOptions { TopK = request.TopK.Value } : null,
            cancellationToken);

        stopwatch.Stop();

        return Results.Ok(new
        {
            content = output.Content,
            tokensUsed = output.TokensUsed,
            artifacts = output.Artifacts,
            ragEnriched = true,
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
        workspacePath = conversation.WorkspacePath,
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
        WorkspacePath = request.WorkspacePath,
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
        WorkspacePath: conversation.WorkspacePath);

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

// Index a directory
app.MapPost("/api/rag/index/directory", async (
    IndexDirectoryRequest request,
    IRagService ragService,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (!Directory.Exists(request.Path))
        {
            return Results.NotFound(new
            {
                success = false,
                error = "Directory not found: " + request.Path
            });
        }

        var options = new RagIndexOptions
        {
            IncludePatterns = request.IncludePatterns,
            ExcludePatterns = request.ExcludePatterns,
            Recursive = request.Recursive ?? true,
        };

        var count = await ragService.IndexDirectoryAsync(request.Path, options, cancellationToken);

        return Results.Ok(new
        {
            success = true,
            path = request.Path,
            filesIndexed = count,
            message = count + " files indexed successfully"
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

// Get directory-specific RAG stats
app.MapGet("/api/rag/stats/directory", async (
    string path,
    IRagService ragService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var stats = await ragService.GetDirectoryStatsAsync(path, cancellationToken);

        if (stats is null)
        {
            return Results.Ok(new
            {
                isIndexed = false,
                directoryPath = path,
                chunkCount = 0,
                fileCount = 0,
                lastIndexedAt = (DateTimeOffset?)null
            });
        }

        return Results.Ok(new
        {
            isIndexed = stats.IsIndexed,
            directoryPath = stats.DirectoryPath,
            chunkCount = stats.ChunkCount,
            fileCount = stats.FileCount,
            lastIndexedAt = stats.LastIndexedAt
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

// Clear entire RAG index
app.MapDelete("/api/rag", async (IRagService ragService, CancellationToken cancellationToken) =>
{
    try
    {
        await ragService.ClearAsync(cancellationToken);

        return Results.Ok(new
        {
            success = true,
            message = "RAG index cleared"
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

// Index a solution/project into the code graph
app.MapPost("/api/graph/index", async (
    IndexCodeGraphRequest request,
    IServiceScopeFactory scopeFactory,
    CancellationToken ct) =>
{
    try
    {
        using var scope = scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<Aura.Module.Developer.Services.ICodeGraphIndexer>();

        Aura.Module.Developer.Services.CodeGraphIndexResult result;
        if (request.Reindex)
        {
            result = await indexer.ReindexAsync(request.SolutionPath, request.WorkspacePath, ct);
        }
        else
        {
            result = await indexer.IndexAsync(request.SolutionPath, request.WorkspacePath, ct);
        }

        if (!result.Success)
        {
            return Results.BadRequest(new
            {
                success = false,
                error = result.ErrorMessage
            });
        }

        return Results.Ok(new
        {
            success = true,
            nodesCreated = result.NodesCreated,
            edgesCreated = result.EdgesCreated,
            projectsIndexed = result.ProjectsIndexed,
            filesIndexed = result.FilesIndexed,
            typesIndexed = result.TypesIndexed,
            durationMs = (int)result.Duration.TotalMilliseconds,
            warnings = result.Warnings
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

// Find implementations of an interface
app.MapGet("/api/graph/implementations/{interfaceName}", async (
    string interfaceName,
    string? workspacePath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var implementations = await graphService.FindImplementationsAsync(
            Uri.UnescapeDataString(interfaceName),
            workspacePath,
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
    string? workspacePath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var callers = await graphService.FindCallersAsync(
            Uri.UnescapeDataString(methodName),
            containingType,
            workspacePath,
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
    string? workspacePath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var members = await graphService.GetTypeMembersAsync(
            Uri.UnescapeDataString(typeName),
            workspacePath,
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
    string? workspacePath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        var types = await graphService.GetTypesInNamespaceAsync(
            Uri.UnescapeDataString(namespaceName),
            workspacePath,
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
    string? workspacePath,
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
            workspacePath,
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

// Clear graph for a workspace
app.MapDelete("/api/graph/{workspacePath}", async (
    string workspacePath,
    Aura.Foundation.Rag.ICodeGraphService graphService,
    CancellationToken ct) =>
{
    try
    {
        await graphService.ClearWorkspaceGraphAsync(Uri.UnescapeDataString(workspacePath), ct);

        return Results.Ok(new
        {
            success = true,
            message = $"Graph cleared for workspace: {workspacePath}"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Semantic index a directory (code graph + selective embeddings)
app.MapPost("/api/semantic/index", async (
    SemanticIndexRequest request,
    IServiceScopeFactory scopeFactory,
    CancellationToken ct) =>
{
    try
    {
        using var scope = scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<Aura.Foundation.Rag.ISemanticIndexer>();

        var options = new Aura.Foundation.Rag.SemanticIndexOptions
        {
            Recursive = request.Recursive ?? true,
            Parallel = request.Parallel ?? true,
        };

        var result = await indexer.IndexDirectoryAsync(request.DirectoryPath, options, ct);

        if (!result.Success)
        {
            return Results.BadRequest(new
            {
                success = false,
                errors = result.Errors
            });
        }

        return Results.Ok(new
        {
            success = true,
            filesIndexed = result.FilesIndexed,
            chunksCreated = result.ChunksCreated,
            durationMs = (int)result.Duration.TotalMilliseconds,
            filesByLanguage = result.FilesByLanguage,
            warnings = result.Warnings
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

// ==== Background Indexing Endpoints ====

// Queue a directory for background indexing (non-blocking)
app.MapPost("/api/index/background", (
    BackgroundIndexRequest request,
    Aura.Foundation.Rag.IBackgroundIndexer backgroundIndexer) =>
{
    if (string.IsNullOrWhiteSpace(request.Directory))
    {
        return Results.BadRequest(new
        {
            error = "'directory' is required",
            hint = "POST body should be: { \"directory\": \"C:\\path\\to\\folder\" }"
        });
    }

    if (!System.IO.Directory.Exists(request.Directory))
    {
        return Results.BadRequest(new
        {
            error = $"Directory does not exist: {request.Directory}"
        });
    }

    var options = new Aura.Foundation.Rag.RagIndexOptions
    {
        Recursive = request.Recursive ?? true,
        IncludePatterns = request.IncludePatterns?.ToArray(),
        ExcludePatterns = request.ExcludePatterns?.ToArray(),
    };

    var jobId = backgroundIndexer.QueueDirectory(request.Directory, options);

    return Results.Accepted($"/api/index/jobs/{jobId}", new
    {
        jobId = jobId,
        message = "Indexing queued. Check status at /api/index/jobs/{jobId}",
        directory = request.Directory
    });
});

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
        Model = request.Model ?? "qwen2.5-coder:7b",
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
    var result = await gitService.CommitAsync(request.RepoPath, request.Message, ct);
    
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
            workspacePath = workflow.WorkspacePath,
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
            workspacePath = w.WorkspacePath,
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
        workspacePath = workflow.WorkspacePath,
        repositoryPath = workflow.RepositoryPath,
        analyzedContext = workflow.AnalyzedContext,
        executionPlan = workflow.ExecutionPlan,
        steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
        {
            id = s.Id,
            order = s.Order,
            name = s.Name,
            capability = s.Capability,
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
        completedAt = workflow.CompletedAt
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

app.Run();

// Request models
record ExecuteAgentRequest(string Prompt, string? WorkspacePath = null);
record ExecuteWithRagRequest(
    string Prompt,
    string? WorkspacePath = null,
    bool? UseRag = null,
    int? TopK = null);
record CreateConversationRequest(string AgentId, string? Title = null, string? WorkspacePath = null);
record AddMessageRequest(string Content);

// RAG request models
record IndexContentRequest(
    string ContentId,
    string Text,
    string? ContentType = null,
    string? SourcePath = null,
    string? Language = null);

record IndexDirectoryRequest(
    string Path,
    IReadOnlyList<string>? IncludePatterns = null,
    IReadOnlyList<string>? ExcludePatterns = null,
    bool? Recursive = null);

record RagQueryRequest(
    string Query,
    int? TopK = null,
    double? MinScore = null,
    string? SourcePathPrefix = null);

// Graph RAG request models
record IndexCodeGraphRequest(
    string SolutionPath,
    string WorkspacePath,
    bool Reindex = false);

// Semantic indexing request models
record SemanticIndexRequest(
    string DirectoryPath,
    bool? Recursive = null,
    bool? Parallel = null);

// Background indexing request models
record BackgroundIndexRequest(
    string Directory,
    bool? Recursive = null,
    IReadOnlyList<string>? IncludePatterns = null,
    IReadOnlyList<string>? ExcludePatterns = null);

// Make Program accessible for WebApplicationFactory

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
    string Title,
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

public partial class Program { }

