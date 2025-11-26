# Phase 4: API Endpoints

**Duration:** 2-3 hours  
**Dependencies:** Phases 1-3 (Core, LLM, Data)  
**Output:** `Aura.Host/` project with REST API

## Objective

Create the Aspire host with REST API endpoints for agents, workflows, and step execution.

## Tasks

### 4.1 Create Host Project

```bash
dotnet new web -n Aura.Host -o src/Aura.Host
dotnet add src/Aura.Host reference src/Aura
```

**Project structure:**
```
src/Aura.Host/
├── Aura.Host.csproj
├── Program.cs
├── Endpoints/
│   ├── AgentEndpoints.cs
│   ├── WorkflowEndpoints.cs
│   └── HealthEndpoints.cs
├── appsettings.json
└── Properties/
    └── launchSettings.json
```

### 4.2 Configure Program.cs

**Program.cs:**
```csharp
using Aura.Agents;
using Aura.Data;
using Aura.LLM;
using Aura.Host.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Aura services
builder.Services.AddAuraLlm();
builder.Services.AddAuraData(
    builder.Configuration.GetConnectionString("aura-db") 
    ?? throw new InvalidOperationException("Database connection string required"));

// Add agent infrastructure
builder.Services.AddSingleton<MarkdownAgentLoader>();
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Initialize agents
var registry = app.Services.GetRequiredService<IAgentRegistry>();
var agentsPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "agents");
await registry.WatchFolderAsync(agentsPath);

// Map endpoints
app.MapAgentEndpoints();
app.MapWorkflowEndpoints();
app.MapHealthEndpoints();

app.Run();
```

### 4.3 Implement Agent Endpoints

**AgentEndpoints.cs:**
```csharp
namespace Aura.Host.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents").WithTags("Agents");
        
        group.MapGet("/", GetAgents);
        group.MapGet("/{id}", GetAgent);
        group.MapGet("/", GetAgentsByCapability).WithName("GetByCapability");
        group.MapPost("/", RegisterAgent);
        group.MapDelete("/{id}", UnregisterAgent);
    }
    
    private static IResult GetAgents(IAgentRegistry registry)
    {
        var agents = registry.GetAll().Select(a => new AgentResponse(a.Metadata));
        return Results.Ok(new { agents });
    }
    
    private static IResult GetAgent(string id, IAgentRegistry registry)
    {
        var agent = registry.GetById(id);
        return agent is null 
            ? Results.NotFound() 
            : Results.Ok(new AgentResponse(agent.Metadata));
    }
    
    private static IResult GetAgentsByCapability(
        [FromQuery] string capability, 
        IAgentRegistry registry)
    {
        var agents = registry.GetByCapability(capability)
            .Select(a => new AgentResponse(a.Metadata));
        return Results.Ok(new { agents });
    }
    
    private static async Task<IResult> RegisterAgent(
        RegisterAgentRequest request,
        IAgentRegistry registry,
        ILlmProviderRegistry providers)
    {
        var definition = new AgentDefinition
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = request.Name,
            Capabilities = request.Capabilities,
            Priority = request.Priority ?? 50,
            Provider = request.Provider ?? "ollama",
            Model = request.Model ?? "qwen2.5-coder:7b",
            Temperature = request.Temperature ?? 0.7f,
            SystemPrompt = request.SystemPrompt
        };
        
        var agent = new ConfigurableAgent(definition, providers);
        registry.Register(agent);
        
        return Results.Created($"/api/agents/{agent.Metadata.Id}", 
            new AgentResponse(agent.Metadata));
    }
    
    private static IResult UnregisterAgent(string id, IAgentRegistry registry)
    {
        var removed = registry.Unregister(id);
        return removed ? Results.NoContent() : Results.NotFound();
    }
}

// DTOs
public record AgentResponse(
    string Id,
    string Name,
    string[] Capabilities,
    int Priority,
    string Provider,
    string Model,
    string? Description)
{
    public AgentResponse(AgentMetadata m) : this(
        m.Id, m.Name, m.Capabilities, m.Priority, 
        m.Provider, m.Model, m.Description) { }
}

public record RegisterAgentRequest(
    string Name,
    string[] Capabilities,
    string SystemPrompt,
    int? Priority = null,
    string? Provider = null,
    string? Model = null,
    float? Temperature = null);
```

### 4.4 Implement Workflow Endpoints

**WorkflowEndpoints.cs:**
```csharp
namespace Aura.Host.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workflows").WithTags("Workflows");
        
        // CRUD
        group.MapGet("/", GetWorkflows);
        group.MapGet("/{id:guid}", GetWorkflow);
        group.MapPost("/", CreateWorkflow);
        group.MapDelete("/{id:guid}", DeleteWorkflow);
        
        // Phases
        group.MapPost("/{id:guid}/digest", DigestWorkflow);
        group.MapPost("/{id:guid}/plan", PlanWorkflow);
        group.MapPost("/{id:guid}/replan", ReplanWorkflow);
        
        // Steps
        group.MapPost("/{id:guid}/steps/{stepId:guid}/execute", ExecuteStep);
        group.MapGet("/{id:guid}/steps/{stepId:guid}/output", GetStepOutput);
        group.MapPost("/{id:guid}/steps/{stepId:guid}/retry", RetryStep);
        group.MapPost("/{id:guid}/steps/{stepId:guid}/skip", SkipStep);
        
        // Chat
        group.MapPost("/{id:guid}/chat", SendChatMessage);
        group.MapGet("/{id:guid}/chat", GetChatHistory);
        
        // SSE
        group.MapGet("/{id:guid}/events", SubscribeToEvents);
    }
    
    private static async Task<IResult> GetWorkflows(
        [FromQuery] string? status,
        IWorkflowRepository repo)
    {
        WorkflowStatus? statusFilter = null;
        if (Enum.TryParse<WorkflowStatus>(status, true, out var s))
            statusFilter = s;
        
        var workflows = await repo.GetAllAsync(statusFilter);
        return Results.Ok(new { workflows = workflows.Select(ToSummary) });
    }
    
    private static async Task<IResult> GetWorkflow(Guid id, IWorkflowRepository repo)
    {
        var workflow = await repo.GetWithStepsAsync(id);
        return workflow is null 
            ? Results.NotFound() 
            : Results.Ok(ToDetail(workflow));
    }
    
    private static async Task<IResult> CreateWorkflow(
        CreateWorkflowRequest request,
        IWorkflowRepository repo)
    {
        var workflow = new Workflow
        {
            WorkItemId = request.WorkItemId,
            WorkItemTitle = request.WorkItemTitle,
            WorkItemDescription = request.WorkItemDescription,
            WorkspacePath = request.WorkspacePath
        };
        
        await repo.CreateAsync(workflow);
        return Results.Created($"/api/workflows/{workflow.Id}", ToDetail(workflow));
    }
    
    private static async Task<IResult> DigestWorkflow(
        Guid id,
        IWorkflowRepository repo,
        IAgentRegistry agents)
    {
        var workflow = await repo.GetByIdAsync(id);
        if (workflow is null) return Results.NotFound();
        
        workflow.Status = WorkflowStatus.Digesting;
        await repo.UpdateAsync(workflow);
        
        // Get digestion agent
        var digestAgents = agents.GetByCapability("requirements-analysis");
        var agent = digestAgents.FirstOrDefault();
        
        if (agent is null)
        {
            workflow.Status = WorkflowStatus.Digested;
            await repo.UpdateAsync(workflow);
            return Results.Ok(new { status = "Digested", context = (object?)null });
        }
        
        // Execute digestion
        var context = new AgentContext
        {
            WorkflowId = workflow.Id,
            WorkItemId = workflow.WorkItemId,
            WorkItemTitle = workflow.WorkItemTitle,
            TaskDescription = workflow.WorkItemDescription,
            WorkspacePath = workflow.WorkspacePath
        };
        
        var result = await agent.ExecuteAsync(context);
        
        workflow.DigestedContext = result.Output;
        workflow.Status = WorkflowStatus.Digested;
        await repo.UpdateAsync(workflow);
        
        return Results.Ok(new { status = "Digested", context = result.Output });
    }
    
    private static async Task<IResult> PlanWorkflow(
        Guid id,
        IWorkflowRepository repo,
        IAgentRegistry agents,
        AuraDbContext db)
    {
        var workflow = await repo.GetWithStepsAsync(id);
        if (workflow is null) return Results.NotFound();
        
        workflow.Status = WorkflowStatus.Planning;
        await repo.UpdateAsync(workflow);
        
        // Get BA agent for planning
        var planAgents = agents.GetByCapability("requirements-analysis");
        var agent = planAgents.FirstOrDefault();
        
        if (agent is null)
            return Results.BadRequest(new { error = "No planning agent available" });
        
        // Execute planning
        var context = new AgentContext
        {
            WorkflowId = workflow.Id,
            WorkItemId = workflow.WorkItemId,
            WorkItemTitle = workflow.WorkItemTitle,
            TaskDescription = workflow.WorkItemDescription,
            WorkspacePath = workflow.WorkspacePath,
            Data = new Dictionary<string, object>
            {
                ["DigestedContext"] = workflow.DigestedContext ?? "",
                ["Mode"] = "Planning"
            }
        };
        
        var result = await agent.ExecuteAsync(context);
        
        if (!result.Success)
            return Results.BadRequest(new { error = result.Error });
        
        // Parse steps from result (expect JSON array)
        var steps = ParseSteps(result.Output ?? "[]");
        
        // Clear existing steps and add new ones
        workflow.Steps.Clear();
        foreach (var (step, index) in steps.Select((s, i) => (s, i)))
        {
            workflow.Steps.Add(new WorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                Order = index + 1,
                Name = step.Name,
                Capability = step.Capability,
                Description = step.Description
            });
        }
        
        workflow.Status = WorkflowStatus.Planned;
        await db.SaveChangesAsync();
        
        return Results.Ok(new 
        { 
            status = "Planned", 
            steps = workflow.Steps.Select(ToStepResponse) 
        });
    }
    
    private static async Task<IResult> ExecuteStep(
        Guid id,
        Guid stepId,
        ExecuteStepRequest? request,
        IWorkflowRepository repo,
        IAgentRegistry agents,
        AuraDbContext db)
    {
        var workflow = await repo.GetWithStepsAsync(id);
        if (workflow is null) return Results.NotFound();
        
        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return Results.NotFound();
        
        // Get agent (use override or find by capability)
        IAgent? agent;
        if (request?.AgentId is not null)
        {
            agent = agents.GetById(request.AgentId);
            if (agent is null)
                return Results.BadRequest(new { error = $"Agent '{request.AgentId}' not found" });
        }
        else
        {
            agent = agents.GetByCapability(step.Capability).FirstOrDefault();
            if (agent is null)
                return Results.BadRequest(new { error = $"No agent for capability '{step.Capability}'" });
        }
        
        // Execute
        step.Status = StepStatus.Running;
        step.StartedAt = DateTime.UtcNow;
        step.AssignedAgentId = agent.Metadata.Id;
        step.Attempts++;
        await db.SaveChangesAsync();
        
        var context = new AgentContext
        {
            WorkflowId = workflow.Id,
            WorkItemId = workflow.WorkItemId,
            WorkItemTitle = workflow.WorkItemTitle,
            TaskDescription = step.Description,
            WorkspacePath = workflow.WorkspacePath
        };
        
        var result = await agent.ExecuteAsync(context);
        
        step.Output = result.Output;
        step.Error = result.Error;
        step.Status = result.Success ? StepStatus.Completed : StepStatus.Failed;
        step.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        
        return Results.Ok(new StepResultResponse(
            step.Id,
            step.Status.ToString(),
            agent.Metadata.Id,
            step.Output,
            step.Error,
            (int)(step.CompletedAt!.Value - step.StartedAt!.Value).TotalMilliseconds));
    }
    
    // Helper methods
    private static WorkflowSummary ToSummary(Workflow w) => new(
        w.Id, w.WorkItemId, w.WorkItemTitle, w.Status.ToString(), 
        w.Steps.Count, w.CreatedAt);
    
    private static WorkflowDetail ToDetail(Workflow w) => new(
        w.Id, w.WorkItemId, w.WorkItemTitle, w.WorkItemDescription,
        w.Status.ToString(), w.WorkspacePath, w.GitBranch,
        w.DigestedContext, w.Steps.Select(ToStepResponse).ToList(),
        w.CreatedAt, w.UpdatedAt);
    
    private static StepResponse ToStepResponse(WorkflowStep s) => new(
        s.Id, s.Order, s.Name, s.Capability, s.Description,
        s.Status.ToString(), s.AssignedAgentId);
    
    private static List<ParsedStep> ParseSteps(string json)
    {
        // Parse JSON array of steps from agent output
        // Expected format: [{ "name": "...", "capability": "...", "description": "..." }]
        try
        {
            return JsonSerializer.Deserialize<List<ParsedStep>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

// DTOs
public record CreateWorkflowRequest(
    string WorkItemId,
    string WorkItemTitle,
    string? WorkItemDescription,
    string? WorkspacePath);

public record ExecuteStepRequest(string? AgentId);

public record ReplanRequest(string Feedback);

public record WorkflowSummary(
    Guid Id, string WorkItemId, string WorkItemTitle, 
    string Status, int StepCount, DateTime CreatedAt);

public record WorkflowDetail(
    Guid Id, string WorkItemId, string WorkItemTitle, string? WorkItemDescription,
    string Status, string? WorkspacePath, string? GitBranch,
    string? DigestedContext, List<StepResponse> Steps,
    DateTime CreatedAt, DateTime UpdatedAt);

public record StepResponse(
    Guid Id, int Order, string Name, string Capability, string? Description,
    string Status, string? AssignedAgentId);

public record StepResultResponse(
    Guid StepId, string Status, string AgentId, 
    string? Output, string? Error, int DurationMs);

public record ParsedStep(string Name, string Capability, string? Description);
```

### 4.5 Implement Health Endpoints

**HealthEndpoints.cs:**
```csharp
namespace Aura.Host.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        app.MapGet("/health/ready", async (AuraDbContext db, ILlmProviderRegistry providers) =>
        {
            var dbReady = await db.Database.CanConnectAsync();
            var llmReady = await providers.GetProviderOrDefault(null).IsAvailableAsync();
            
            return Results.Ok(new 
            { 
                status = dbReady && llmReady ? "ready" : "degraded",
                database = dbReady,
                llm = llmReady
            });
        });
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
    }
}
```

### 4.6 Configure Aspire

**Aura.Host.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Aura\Aura.csproj" />
    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
  </ItemGroup>
</Project>
```

### 4.7 Add Integration Tests

```csharp
public class WorkflowEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    [Fact]
    public async Task CreateWorkflow_ReturnsCreated() { ... }
    
    [Fact]
    public async Task GetWorkflow_NotFound_Returns404() { ... }
    
    [Fact]
    public async Task ExecuteStep_CallsAgent() { ... }
}
```

## Verification

1. ✅ `dotnet build src/Aura.Host` succeeds
2. ✅ `dotnet run --project src/Aura.Host` starts
3. ✅ `/swagger` shows API documentation
4. ✅ Create/Get/Delete workflow works
5. ✅ Agent registration works
6. ✅ Step execution calls agent

## Deliverables

- [ ] `Aura.Host/` project with Aspire integration
- [ ] Agent endpoints (list, get, register, unregister)
- [ ] Workflow endpoints (CRUD, phases, steps)
- [ ] Health endpoints
- [ ] OpenAPI documentation
- [ ] Integration tests
