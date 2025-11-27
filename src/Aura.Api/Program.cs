using Aura.Foundation;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Llm;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Add PostgreSQL with EF Core
// Connection string comes from Aspire AppHost via configuration
var connectionString = builder.Configuration.GetConnectionString("auradb");
builder.Services.AddDbContext<AuraDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Aura Foundation services
builder.Services.AddAuraFoundation(builder.Configuration);

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

app.MapGet("/health/rag", async (IServiceProvider sp) =>
{
    // TODO: Add actual RAG health check in Phase 3
    return new
    {
        healthy = true,
        details = "RAG check not yet implemented",
        timestamp = DateTime.UtcNow
    };
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
            details = $"{models.Count} models available",
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
        return Results.NotFound(new { error = $"No agent found for capability '{capability}'" + (language is not null ? $" with language '{language}'" : "") });
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
        return Results.NotFound(new { error = $"Agent '{agentId}' not found" });
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
        return Results.NotFound(new { error = $"Agent '{agentId}' not found" });
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
        db.AgentExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

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
        db.AgentExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

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
        db.AgentExecutions.Add(execution);
        await db.SaveChangesAsync(CancellationToken.None);

        return Results.StatusCode(499); // Client Closed Request
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

    return Results.Created($"/api/conversations/{conversation.Id}", new
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
        return Results.BadRequest(new { error = $"Agent '{conversation.AgentId}' not found" });
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

app.Run();

// Request models
record ExecuteAgentRequest(string Prompt, string? WorkspacePath = null);
record CreateConversationRequest(string AgentId, string? Title = null, string? WorkspacePath = null);
record AddMessageRequest(string Content);
