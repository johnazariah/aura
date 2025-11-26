using Aura.Foundation;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;

var builder = WebApplication.CreateBuilder(args);

// Add Aura Foundation services
builder.Services.AddAuraFoundation(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks();

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

app.MapGet("/health/db", async (IServiceProvider sp) =>
{
    // TODO: Add actual database health check in Phase 3
    return new
    {
        healthy = true,
        details = "Database check not yet implemented",
        timestamp = DateTime.UtcNow
    };
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
app.MapGet("/api/agents", (IAgentRegistry registry) =>
{
    return registry.Agents.Select(a => new
    {
        id = a.AgentId,
        name = a.Metadata.Name,
        description = a.Metadata.Description,
        provider = a.Metadata.Provider,
        model = a.Metadata.Model,
        tags = a.Metadata.Tags
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
    CancellationToken cancellationToken) =>
{
    var agent = registry.GetAgent(agentId);
    if (agent is null)
    {
        return Results.NotFound(new { error = $"Agent '{agentId}' not found" });
    }

    var context = new AgentContext(
        Prompt: request.Prompt,
        WorkspacePath: request.WorkspacePath);

    var result = await agent.ExecuteAsync(context, cancellationToken);

    if (result.IsFailure)
    {
        return Results.BadRequest(new
        {
            error = result.Error.Message,
            code = result.Error.Code.ToString()
        });
    }

    return Results.Ok(new
    {
        content = result.Value.Content,
        tokensUsed = result.Value.TokensUsed,
        artifacts = result.Value.Artifacts
    });
});

app.Run();

// Request models
record ExecuteAgentRequest(string Prompt, string? WorkspacePath = null);
