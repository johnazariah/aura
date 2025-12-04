# Testing Strategy Specification

**Version:** 1.0  
**Status:** Draft  
**Last Updated:** 2025-11-26

## Overview

Testing strategy for Aura focuses on fast, reliable tests that don't depend on LLM output quality. We test the infrastructure, not the AI.

## Testing Pyramid

```
                    ┌───────────┐
                    │   E2E     │  Manual + golden path
                    │  (few)    │  Real LLM, real git
                    ├───────────┤
                    │Integration│  WebApplicationFactory
                    │  (some)   │  Mock LLM, real DB
               ┌────┴───────────┴────┐
               │      Unit Tests      │  Pure logic
               │       (many)         │  No dependencies
               └──────────────────────┘
```

## Test Categories

### 1. Unit Tests (Fast, Many)

Test pure logic with no external dependencies.

**What to test:**
- Agent registry (registration, capability matching, priority)
- Markdown agent parsing
- Prompt template rendering
- API request/response mapping
- Worktree path generation

**Example:**

```csharp
public class AgentRegistryTests
{
    [Fact]
    public void GetByCapability_ReturnsAgentsSortedByPriority()
    {
        // Arrange
        var registry = new AgentRegistry();
        registry.Register(CreateAgent("A", ["coding"], priority: 90));
        registry.Register(CreateAgent("B", ["coding"], priority: 50));
        registry.Register(CreateAgent("C", ["coding"], priority: 70));
        
        // Act
        var result = registry.GetByCapability("coding").ToList();
        
        // Assert
        result.Should().HaveCount(3);
        result[0].Metadata.Name.Should().Be("B"); // priority 50
        result[1].Metadata.Name.Should().Be("C"); // priority 70
        result[2].Metadata.Name.Should().Be("A"); // priority 90
    }
    
    [Fact]
    public void GetByCapability_NoMatch_ReturnsEmpty()
    {
        var registry = new AgentRegistry();
        registry.Register(CreateAgent("A", ["coding"]));
        
        var result = registry.GetByCapability("testing");
        
        result.Should().BeEmpty();
    }
}
```

**Run:** Always in CI, < 1 second total

### 2. Integration Tests (Medium Speed)

Test API endpoints with real database but mocked LLM.

**What to test:**
- Endpoint contracts (request/response shapes)
- Database operations (CRUD, queries)
- Workflow state transitions
- Error handling and status codes

**Setup:**

```csharp
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly MockLlmProvider _mockLlm;
    
    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _mockLlm = new MockLlmProvider();
        
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real LLM with mock
                services.RemoveAll<ILlmProvider>();
                services.AddSingleton<ILlmProvider>(_mockLlm);
                
                // Use in-memory database
                services.RemoveAll<DbContextOptions<AuraDbContext>>();
                services.AddDbContext<AuraDbContext>(options =>
                    options.UseInMemoryDatabase($"Test-{Guid.NewGuid()}"));
            });
        }).CreateClient();
    }
    
    [Fact]
    public async Task CreateWorkflow_ReturnsCreatedWithId()
    {
        // Arrange
        var request = new { workItemId = "test-123", workItemTitle = "Test" };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/workflows", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        workflow.Id.Should().NotBeEmpty();
        workflow.Status.Should().Be("Created");
    }
    
    [Fact]
    public async Task ExecuteStep_CallsAgentAndStoresResult()
    {
        // Arrange
        _mockLlm.SetResponse("Generate code", "public class Foo {}");
        var workflowId = await CreateWorkflowWithSteps();
        
        // Act
        var response = await _client.PostAsync(
            $"/api/workflows/{workflowId}/steps/{stepId}/execute", null);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StepResultResponse>();
        result.Status.Should().Be("Completed");
        result.Output.Should().Contain("public class Foo");
    }
}
```

**Run:** In CI, < 30 seconds total

### 3. Git Integration Tests

Test git/worktree operations with real git repos.

```csharp
public class WorktreeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WorktreeService _service;
    
    public WorktreeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aura-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        
        // Initialize a real git repo
        RunGit(_tempDir, "init");
        RunGit(_tempDir, "commit --allow-empty -m 'init'");
        
        _service = new WorktreeService(_tempDir);
    }
    
    [Fact]
    public async Task CreateAsync_CreatesWorktreeAndBranch()
    {
        // Act
        var info = await _service.CreateAsync(_tempDir, Guid.NewGuid());
        
        // Assert
        Directory.Exists(info.WorktreePath).Should().BeTrue();
        info.IsClean.Should().BeTrue();
        
        // Branch should exist
        var branches = RunGit(_tempDir, "branch --list");
        branches.Should().Contain(info.BranchName);
    }
    
    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
```

**Run:** In CI, < 10 seconds total

### 4. E2E Tests (Manual + Golden Path)

Full system tests with real LLM.

**When to run:**
- Pre-release validation
- Major feature development
- Not in CI (slow, non-deterministic)

**Golden path test:**

```csharp
[Trait("Category", "E2E")]
[Trait("Skip", "CI")]  // Skip in CI
public class E2EWorkflowTests
{
    [Fact]
    public async Task CompleteWorkflow_ENRICHPlanExecute()
    {
        // This test requires:
        // - Running Aura API
        // - Running Ollama with qwen2.5-coder:7b
        // - A real git repository
        
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:5258") };
        
        // 1. Create workflow
        var workflow = await client.PostAsJsonAsync("/api/workflows", new
        {
            workItemId = "test-e2e",
            workItemTitle = "Create a hello world function",
            workspacePath = "/path/to/test/repo"
        });
        var workflowId = (await workflow.Content.ReadFromJsonAsync<dynamic>()).id;
        
        // 2. ENRICH
        await client.PostAsync($"/api/workflows/{workflowId}/enrich", null);
        
        // 3. Plan
        var planResponse = await client.PostAsync($"/api/workflows/{workflowId}/plan", null);
        var plan = await planResponse.Content.ReadFromJsonAsync<dynamic>();
        
        // Verify plan has steps
        Assert.True(plan.steps.Count > 0);
        
        // 4. Execute first step
        var stepId = plan.steps[0].id;
        var execResponse = await client.PostAsync(
            $"/api/workflows/{workflowId}/steps/{stepId}/execute", null);
        
        Assert.Equal(HttpStatusCode.OK, execResponse.StatusCode);
    }
}
```

### 5. Extension Tests

Test VS Code extension with mock API.

```typescript
// extension/src/test/auraService.test.ts
import { AuraService } from '../auraService';
import nock from 'nock';

describe('AuraService', () => {
  let service: AuraService;
  
  beforeEach(() => {
    service = new AuraService('http://localhost:5258');
  });
  
  afterEach(() => {
    nock.cleanAll();
  });
  
  it('getWorkflows returns workflow list', async () => {
    nock('http://localhost:5258')
      .get('/api/workflows')
      .reply(200, {
        workflows: [
          { id: '1', workItemTitle: 'Test', status: 'Created' }
        ]
      });
    
    const workflows = await service.getWorkflows();
    
    expect(workflows).toHaveLength(1);
    expect(workflows[0].workItemTitle).toBe('Test');
  });
  
  it('executeStep with agent override sends agentId', async () => {
    nock('http://localhost:5258')
      .post('/api/workflows/wf-1/steps/step-1/execute', { agentId: 'roslyn' })
      .reply(200, { status: 'Completed' });
    
    const result = await service.executeStep('wf-1', 'step-1', 'roslyn');
    
    expect(result.status).toBe('Completed');
  });
});
```

## What NOT to Test

| Don't Test | Why |
|------------|-----|
| LLM output quality | Non-deterministic, changes with model |
| Exact prompt wording | Implementation detail |
| External API responses | Mock them instead |
| UI rendering | Manual testing, snapshot if needed |

## Test Configuration

```json
// Directory.Build.Test.props
{
  "TestEnvironment": {
    "SKIP_LLM_TESTS": "true",      // Skip real LLM tests in CI
    "SKIP_GIT_TESTS": "false",     // Git tests are fast and reliable
    "TEST_DB": "InMemory"          // Use in-memory DB
  }
}
```

## CI Pipeline

```yaml
# .github/workflows/test.yml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'
      
      - name: Run unit tests
        run: dotnet test --filter "Category!=E2E&Category!=Integration"
      
      - name: Run integration tests
        run: dotnet test --filter "Category=Integration"
        env:
          SKIP_LLM_TESTS: true
      
      - name: Run extension tests
        working-directory: extension
        run: npm test
```

## Mock LLM Provider

```csharp
public class MockLlmProvider : ILlmProvider
{
    public string Name => "mock";
    
    private readonly Dictionary<string, string> _responses = new();
    private readonly List<(string Prompt, string Model)> _calls = new();
    
    public void SetResponse(string promptContains, string response)
    {
        _responses[promptContains] = response;
    }
    
    public Task<string> GenerateAsync(string prompt, string model, ...)
    {
        _calls.Add((prompt, model));
        
        var match = _responses.FirstOrDefault(kv => prompt.Contains(kv.Key));
        return Task.FromResult(match.Value ?? "Mock response");
    }
    
    public void VerifyCalled(string promptContains)
    {
        _calls.Should().Contain(c => c.Prompt.Contains(promptContains));
    }
    
    public void VerifyCalledWithModel(string model)
    {
        _calls.Should().Contain(c => c.Model == model);
    }
}
```

## Coverage Goals

| Area | Target | Rationale |
|------|--------|-----------|
| Agent infrastructure | 90%+ | Core logic, must be solid |
| API endpoints | 80%+ | Contract tests |
| Git operations | 70%+ | Has edge cases |
| LLM providers | 50%+ | Mostly pass-through |
| Extension | 60%+ | UI logic |

## Open Questions

1. **Snapshot testing** - For API response shapes?
2. **Contract testing** - Pact for extension ↔ API?
3. **Performance tests** - Benchmark agent execution?
4. **Chaos testing** - Test recovery from failures?
