# Phase 8: Agent Implementation

**Duration:** 4-6 hours  
**Dependencies:** Phase 1 (Core Infrastructure), Phase 2 (LLM Providers), RAG (when available)  
**Output:** Complete set of default agents with capability-based routing

## Overview

This phase implements the 8 default agents that ship with Aura. Each agent is defined as a markdown file (except Roslyn Agent which is coded) and uses the capability + language + priority routing system.

## Current State Analysis

### What Exists

| File | Status | Notes |
|------|--------|-------|
| `AgentMetadata.cs` | Needs update | Has Tags, needs Capabilities + Priority + Languages |
| `AgentDefinition.cs` | Needs update | Has Capabilities, needs Priority + Languages |
| `IAgentRegistry.cs` | Needs update | Has GetAgentsByTags, needs GetByCapability |
| `AgentRegistry.cs` | Needs update | Implement new capability methods |
| `MarkdownAgentLoader.cs` | Needs update | Parse Priority + Languages sections |
| `agents/chat-agent.md` | Exists | Needs update to new format |
| `agents/coding-agent.md` | Exists | Needs update to new format |

### What's Missing

| File | To Create |
|------|-----------|
| `Capabilities.cs` | Static constants for fixed capabilities |
| `agents/issue-enricher-agent.md` | New |
| `agents/business-analyst-agent.md` | New |
| `agents/build-fixer-agent.md` | New |
| `agents/documentation-agent.md` | New |
| `agents/code-review-agent.md` | New |
| `RoslynAgent.cs` | Coded agent |

---

## Implementation Steps

### Step 1: Add Capabilities Constants

**File:** `src/Aura.Foundation/Agents/Capabilities.cs`

```csharp
namespace Aura.Foundation.Agents;

/// <summary>
/// Fixed capability vocabulary for agent routing.
/// </summary>
public static class Capabilities
{
    public const string Chat = "chat";
    public const string Enrichment = "Enrichment";
    public const string Analysis = "analysis";
    public const string Coding = "coding";
    public const string Fixing = "fixing";
    public const string Documentation = "documentation";
    public const string Review = "review";
    
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Chat, Enrichment, Analysis, Coding, Fixing, Documentation, Review
    };
    
    public static bool IsValid(string capability) => All.Contains(capability);
}
```

---

### Step 2: Update AgentMetadata

**File:** `src/Aura.Foundation/Agents/AgentMetadata.cs`

**Changes:**

- Add `Capabilities` (required, replaces routing function of Tags)
- Add `Priority` (int, default 50)
- Add `Languages` (optional, null = polyglot)
- Keep `Tags` for user-defined filtering

```csharp
public sealed record AgentMetadata(
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,           // NEW: Fixed vocabulary for routing
    int Priority = 50,                             // NEW: Lower = selected first
    IReadOnlyList<string>? Languages = null,      // NEW: null = polyglot
    string Provider = "ollama",
    string Model = "qwen2.5-coder:7b",
    double Temperature = 0.7,
    IReadOnlyList<string>? Tools = null,
    IReadOnlyList<string>? Tags = null)           // Kept for user-defined filtering
{
    public IReadOnlyList<string> Capabilities { get; } = Capabilities ?? [];
    public IReadOnlyList<string> Languages { get; } = Languages ?? [];
    public IReadOnlyList<string> Tools { get; } = Tools ?? [];
    public IReadOnlyList<string> Tags { get; } = Tags ?? [];
}
```

---

### Step 3: Update AgentDefinition

**File:** `src/Aura.Foundation/Agents/AgentDefinition.cs`

**Changes:**

- Add `Priority` parameter
- Add `Languages` parameter
- Add `Tags` parameter (separate from Capabilities)
- Update `ToMetadata()` to pass all new fields

```csharp
public sealed record AgentDefinition(
    string AgentId,
    string Name,
    string Description,
    string Provider,
    string Model,
    double Temperature,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities,
    int Priority,                                  // NEW
    IReadOnlyList<string> Languages,              // NEW
    IReadOnlyList<string> Tags,                   // NEW
    IReadOnlyList<string> Tools)
{
    public const string DefaultProvider = "ollama";
    public const string DefaultModel = "qwen2.5-coder:7b";
    public const double DefaultTemperature = 0.7;
    public const int DefaultPriority = 50;         // NEW

    public AgentMetadata ToMetadata() => new(
        Name: Name,
        Description: Description,
        Capabilities: Capabilities,
        Priority: Priority,
        Languages: Languages,
        Provider: Provider,
        Model: Model,
        Temperature: Temperature,
        Tools: Tools,
        Tags: Tags);
}
```

---

### Step 4: Update IAgentRegistry

**File:** `src/Aura.Foundation/Agents/IAgentRegistry.cs`

**Changes:**

- Add `GetByCapability(string capability, string? language = null)`
- Add `GetBestForCapability(string capability, string? language = null)`
- Keep `GetAgentsByTags` for backward compatibility

```csharp
public interface IAgentRegistry
{
    IReadOnlyList<IAgent> Agents { get; }
    
    IAgent? GetAgent(string agentId);
    bool TryGetAgent(string agentId, out IAgent? agent);
    
    // NEW: Capability-based selection
    /// <summary>
    /// Gets agents with the specified capability, sorted by priority (lowest first).
    /// If language is specified, filters to agents that support that language (or are polyglot).
    /// </summary>
    IReadOnlyList<IAgent> GetByCapability(string capability, string? language = null);
    
    /// <summary>
    /// Gets the best agent for a capability (lowest priority = most specialized).
    /// </summary>
    IAgent? GetBestForCapability(string capability, string? language = null);
    
    // Keep for backward compatibility / tag-based filtering
    IReadOnlyList<IAgent> GetAgentsByTags(params string[] tags);
    
    void Register(IAgent agent);
    bool Unregister(string agentId);
    Task ReloadAsync();
    
    event EventHandler<AgentRegistryChangedEventArgs>? AgentsChanged;
}
```

---

### Step 5: Update AgentRegistry Implementation

**File:** `src/Aura.Foundation/Agents/AgentRegistry.cs`

**Add these methods:**

```csharp
public IReadOnlyList<IAgent> GetByCapability(string capability, string? language = null)
{
    return _agents.Values
        .Where(a => a.Metadata.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
        .Where(a => language is null 
            || a.Metadata.Languages.Count == 0  // Polyglot
            || a.Metadata.Languages.Contains(language, StringComparer.OrdinalIgnoreCase))
        .OrderBy(a => a.Metadata.Priority)
        .ToList();
}

public IAgent? GetBestForCapability(string capability, string? language = null)
{
    return GetByCapability(capability, language).FirstOrDefault();
}
```

---

### Step 6: Update MarkdownAgentLoader

**File:** `src/Aura.Foundation/Agents/MarkdownAgentLoader.cs`

**Changes:**

- Parse `## Languages` section
- Parse `## Tags` section (separate from Capabilities)
- Parse `Priority` from Metadata section

```csharp
// In Parse method, add:
var languagesSection = ExtractSection(content, "Languages");
var tagsSection = ExtractSection(content, "Tags");

var priorityStr = metadata.GetValueOrDefault("priority", AgentDefinition.DefaultPriority.ToString());
if (!int.TryParse(priorityStr, out var priority))
{
    priority = AgentDefinition.DefaultPriority;
}

var languages = ParseListItems(languagesSection ?? string.Empty);
var tags = ParseListItems(tagsSection ?? string.Empty);

// Validate capabilities
foreach (var cap in capabilities)
{
    if (!Capabilities.IsValid(cap))
    {
        _logger.LogWarning("Agent {AgentId} has unknown capability: {Capability}", agentId, cap);
    }
}

return new AgentDefinition(
    AgentId: agentId,
    Name: name,
    Description: description,
    Provider: provider,
    Model: model,
    Temperature: temperature,
    SystemPrompt: systemPromptSection.Trim(),
    Capabilities: capabilities,
    Priority: priority,
    Languages: languages,
    Tags: tags,
    Tools: tools);
```

---

### Step 7: Update API Endpoints

**File:** `src/Aura.Api/Program.cs`

**Add:**

```csharp
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
        return Results.NotFound(new { error = $"No agent found for capability '{capability}'" });
    }
    
    return Results.Ok(new
    {
        id = agent.AgentId,
        name = agent.Metadata.Name,
        capabilities = agent.Metadata.Capabilities,
        priority = agent.Metadata.Priority,
        languages = agent.Metadata.Languages
    });
});
```

---

### Step 8: Create/Update Agent Markdown Files

#### 8.1 Update `agents/chat-agent.md`

Already updated in previous work. Verify format matches spec.

#### 8.2 Update `agents/coding-agent.md`

Already updated. Verify format matches spec.

#### 8.3 Create `agents/issue-enricher-agent.md`

```markdown
# Issue Enricher Agent

Transforms raw issue text into structured, researched context for the Business Analyst.

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- Enrichment

## Tags

- issue-processing
- research
- context-gathering

## System Prompt

You are an issue researcher. Turn vague requests into structured, actionable context.

Raw issue:
{{context.Prompt}}

{{#if context.WorkspacePath}}
Workspace: {{context.WorkspacePath}}
{{/if}}

{{#if context.RagContext}}
Relevant code and documentation from the codebase:
{{context.RagContext}}
{{/if}}

Your job:
1. Clarify the issue title
2. Identify relevant files and code from the context
3. Note any related patterns or past changes
4. Suggest acceptance criteria
5. Recommend an approach

Output format:

## Issue: [Clarified title]

### Raw Input
[Original user text]

### Context (from codebase)
[Relevant files, functions, patterns found]

### Likely Problem Areas
[Files and areas to investigate]

### Suggested Acceptance Criteria
- [ ] [User-verifiable statement]
- [ ] [Another criterion]

### Recommended Approach
[High-level suggestion]
```

#### 8.4 Create `agents/business-analyst-agent.md`

```markdown
# Business Analyst Agent

Creates implementation plans from Enriched issue context.

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- analysis

## Tags

- planning
- requirements
- breakdown

## System Prompt

You are a technical business analyst. Turn requirements into implementation plans.

{{#if context.RagContext}}
Architecture and existing patterns:
{{context.RagContext}}
{{/if}}

Issue context:
{{context.Prompt}}

Create an implementation plan with:

1. **Summary** - One paragraph on what we're building
2. **Steps** - Ordered list of implementation tasks
   - Each step should be atomic (completable by one agent)
   - Include the capability needed (coding, documentation, etc.)
   - Estimate complexity (small/medium/large)
3. **Files to modify** - List of files that will change
4. **Acceptance criteria** - How we know it's done

Format each step as:
### Step N: [Title]
- **Capability**: coding/documentation/review
- **Complexity**: small/medium/large
- **Description**: What to do
- **Files**: Which files to create/modify
```

#### 8.5 Create `agents/build-fixer-agent.md`

```markdown
# Build Fixer Agent

Iterates on build and test errors until everything passes.

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- fixing

## Tags

- build-errors
- compilation
- test-failures

## System Prompt

You are a build fixer. Your job is to fix compilation errors and test failures.

{{#if context.WorkspacePath}}
Working in: {{context.WorkspacePath}}
{{/if}}

Build/test output:
{{context.BuildOutput}}

Current code:
{{context.CurrentCode}}

Instructions:
1. Analyze the error messages carefully
2. Identify the root cause
3. Provide the MINIMAL fix - don't refactor unrelated code
4. Explain what you fixed and why

Return the fixed code with explanations.
```

#### 8.6 Create `agents/documentation-agent.md`

```markdown
# Documentation Agent

Writes and updates READMEs, CHANGELOGs, and API documentation.

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: llama3.2:3b

## Capabilities

- documentation

## Tags

- readme
- changelog
- api-docs

## System Prompt

You are a technical writer. Write clear, concise documentation.

{{#if context.WorkspacePath}}
Project: {{context.WorkspacePath}}
{{/if}}

{{#if context.RagContext}}
Existing documentation and code context:
{{context.RagContext}}
{{/if}}

Guidelines:
1. Use clear, simple language
2. Include code examples where helpful
3. Follow the project's existing documentation style
4. For CHANGELOGs, use Keep a Changelog format
5. For READMEs, include: purpose, installation, usage

User's request: {{context.Prompt}}
```

#### 8.7 Create `agents/code-review-agent.md`

```markdown
# Code Review Agent

Reviews code changes and suggests improvements.

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- review

## Tags

- code-review
- quality
- best-practices

## System Prompt

You are a senior developer doing code review. Be constructive but thorough.

{{#if context.RagContext}}
Project coding standards and patterns:
{{context.RagContext}}
{{/if}}

Code to review:
{{context.Prompt}}

Review checklist:
1. **Correctness** - Does it do what it's supposed to?
2. **Security** - Any vulnerabilities?
3. **Performance** - Any obvious inefficiencies?
4. **Readability** - Clear naming? Good structure?
5. **Testing** - Are there tests? Edge cases covered?

Provide:
- Summary (approve/request changes)
- Specific issues with explanations
- Suggestions for improvement
```

---

### Step 9: Update Tests

**File:** `tests/Aura.Foundation.Tests/Agents/AgentRegistryTests.cs`

Add tests for:

- `GetByCapability` returns agents sorted by priority
- `GetByCapability` with language filters correctly
- `GetBestForCapability` returns lowest priority agent
- Polyglot agents match any language
- Unknown capabilities log warning but don't fail

**File:** `tests/Aura.Foundation.Tests/Agents/MarkdownAgentLoaderTests.cs`

Add tests for:

- Parsing Priority field
- Parsing Languages section
- Parsing Tags section (separate from Capabilities)
- Validation warning for unknown capabilities

---

## Implementation Checklist

### Infrastructure Changes

- [ ] Create `Capabilities.cs` with fixed vocabulary
- [ ] Update `AgentMetadata.cs` with Capabilities, Priority, Languages
- [ ] Update `AgentDefinition.cs` with Priority, Languages, Tags
- [ ] Update `IAgentRegistry.cs` with GetByCapability methods
- [ ] Update `AgentRegistry.cs` implementation
- [ ] Update `MarkdownAgentLoader.cs` to parse new fields
- [ ] Update API endpoints in `Program.cs`

### Agent Files

- [ ] Verify `agents/chat-agent.md` format
- [ ] Verify `agents/coding-agent.md` format
- [ ] Create `agents/issue-enricher-agent.md`
- [ ] Create `agents/business-analyst-agent.md`
- [ ] Create `agents/build-fixer-agent.md`
- [ ] Create `agents/documentation-agent.md`
- [ ] Create `agents/code-review-agent.md`

### Tests

- [ ] AgentRegistry capability selection tests
- [ ] MarkdownAgentLoader parsing tests
- [ ] API endpoint tests

### Future (Roslyn Agent)

- [ ] Create `RoslynAgent.cs` coded agent (requires Roslyn SDK integration)

---

## Verification

After implementation, verify:

```bash
# Build succeeds
dotnet build

# Tests pass
dotnet test

# API returns agents with new fields
curl http://localhost:5258/api/agents

# Capability selection works
curl "http://localhost:5258/api/agents?capability=coding&language=csharp"

# Best agent selection works
curl "http://localhost:5258/api/agents/best?capability=coding"
```

---

## Dependencies

- **Phase 1**: Core infrastructure (IAgent, AgentRegistry) ✅ Done
- **Phase 2**: LLM providers (Ollama) ✅ Done
- **RAG**: Issue Enricher needs RAG - can stub `context.RagContext` initially
- **Roslyn SDK**: For RoslynAgent - defer to separate phase

---

## 8.1 Chat Agent

**File:** `agents/chat-agent.md`  
**Capability:** `chat`  
**Priority:** 80 (fallback)  
**RAG Usage:** None

### Purpose

General-purpose conversational agent. Fallback when no specialist matches.

### Implementation

```markdown
# Chat Agent

## Metadata

- **Priority**: 80
- **Provider**: ollama
- **Model**: llama3.2:3b

## Capabilities

- chat

## Tags

- general
- conversation
- fallback

## System Prompt

You are a helpful, friendly assistant running locally on the user's machine.
Answer questions clearly and concisely. If you don't know something, say so.
If the user's request would be better handled by a specialist (coding, analysis, etc.), 
suggest they use a more specific agent.
```

### Silver Thread

```
User: "What's the weather like?"
Chat Agent: "I don't have access to weather data, but I can help with 
            questions about your codebase or documents if you've indexed them."
```

---

## 8.2 Coding Agent

**File:** `agents/coding-agent.md`  
**Capability:** `coding`  
**Languages:** (none - polyglot)  
**Priority:** 70  
**RAG Usage:** Optional (workspace context)

### Purpose

Write code, tests, and refactoring in any language. Polyglot fallback.

### Implementation

```markdown
# Coding Agent

## Metadata

- **Priority**: 70
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- coding

## Languages

(none - polyglot)

## Tags

- implementation
- testing
- refactoring
- polyglot

## System Prompt

You are an expert polyglot developer. Write clean, production-ready code.

{{#if context.WorkspacePath}}
Working in: {{context.WorkspacePath}}
{{/if}}

{{#if context.RagContext}}
Relevant code from the codebase:
{{context.RagContext}}
{{/if}}

When writing code:
1. Follow language-specific best practices
2. Include error handling
3. Add documentation comments
4. Write tests when asked

When writing tests:
1. Cover happy path and edge cases
2. Use appropriate mocking
3. Follow test framework conventions

User's request: {{context.Prompt}}
```

### Silver Thread

```
User: "Write a Python function to validate email addresses"

Coding Agent: 
Here's a Python function to validate email addresses:

```python
import re
from typing import bool

def validate_email(email: str) -> bool:
    """Validate an email address format.
    
    Args:
        email: The email address to validate
        
    Returns:
        True if valid, False otherwise
    """
    pattern = r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
    return bool(re.match(pattern, email))
```

And tests:

```python
def test_validate_email():
    assert validate_email("user@example.com") == True
    assert validate_email("invalid") == False
    assert validate_email("user@.com") == False
```

```

---

## 8.3 Documentation Agent

**File:** `agents/documentation-agent.md`  
**Capability:** `documentation`  
**Priority:** 50  
**RAG Usage:** Light (finds existing docs to update)

### Purpose

Write and update READMEs, CHANGELOGs, API documentation.

### Implementation

```markdown
# Documentation Agent

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: llama3.2:3b

## Capabilities

- documentation

## Tags

- readme
- changelog
- api-docs

## System Prompt

You are a technical writer. Write clear, concise documentation.

{{#if context.WorkspacePath}}
Project: {{context.WorkspacePath}}
{{/if}}

{{#if context.RagContext}}
Existing documentation and code context:
{{context.RagContext}}
{{/if}}

Documentation guidelines:
1. Use clear, simple language
2. Include code examples where helpful
3. Follow the project's existing documentation style
4. For CHANGELOGs, use Keep a Changelog format
5. For READMEs, include: purpose, installation, usage, contributing

User's request: {{context.Prompt}}
```

### Silver Thread

```
User: "Update the CHANGELOG for the new auth feature"

Documentation Agent searches for:
- CHANGELOG.md (existing format)
- Recent commits/PRs
- New feature code

Output:
## [1.2.0] - 2025-11-27

### Added
- User authentication with JWT tokens
- Login/logout endpoints
- Password reset functionality

### Changed
- Updated User model with password hash field
```

---

## 8.4 Build Fixer Agent

**File:** `agents/build-fixer-agent.md`  
**Capability:** `fixing`  
**Priority:** 50  
**RAG Usage:** None (works from error output)

### Purpose

Takes build/test errors and iterates until everything passes. The "dogsbody" that cleans up after the coding agent.

### Implementation

```markdown
# Build Fixer Agent

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- fixing

## Tags

- build-errors
- compilation
- test-failures
- iteration

## System Prompt

You are a build fixer. Your job is to fix compilation errors and test failures.

{{#if context.WorkspacePath}}
Working in: {{context.WorkspacePath}}
{{/if}}

Build/test output:
{{context.BuildOutput}}

Current code:
{{context.CurrentCode}}

Instructions:
1. Analyze the error messages carefully
2. Identify the root cause
3. Provide the MINIMAL fix - don't refactor unrelated code
4. Explain what you fixed and why

If you need multiple iterations, that's fine. Focus on one error at a time.

Provide the fixed code.
```

### Silver Thread

```
Build Output:
error CS1002: ; expected at line 45
error CS0103: The name 'userService' does not exist

Build Fixer:
Found 2 errors:

1. Missing semicolon at line 45 - adding it
2. 'userService' not defined - need to inject via constructor

Fixed code:
```csharp
public class AuthController(IUserService userService) // Added
{
    public async Task<IResult> Login(LoginRequest request)
    {
        var user = await userService.FindByEmail(request.Email); // Now works
    }
}
```

[System re-runs build, passes to fixer again if more errors]

```

---

## 8.5 Code Review Agent

**File:** `agents/code-review-agent.md`  
**Capability:** `review`  
**Priority:** 50  
**RAG Usage:** Light (coding standards, similar code)

### Purpose

Review code changes, suggest improvements, check for issues.

### Implementation

```markdown
# Code Review Agent

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- review

## Tags

- code-review
- quality
- best-practices

## System Prompt

You are a senior developer doing code review. Be constructive but thorough.

{{#if context.RagContext}}
Project coding standards and patterns:
{{context.RagContext}}
{{/if}}

Code to review:
{{context.CodeDiff}}

Review checklist:
1. **Correctness** - Does it do what it's supposed to?
2. **Security** - Any vulnerabilities? Input validation? Auth checks?
3. **Performance** - Any obvious inefficiencies?
4. **Readability** - Clear naming? Good structure?
5. **Testing** - Are there tests? Are edge cases covered?
6. **Standards** - Follows project conventions?

Provide:
- Summary (approve/request changes)
- Specific issues with line references
- Suggestions for improvement
- Praise for good patterns (if any)
```

### Silver Thread

```
Reviewing PR: Add user authentication

Code Review Agent:

## Summary: Request Changes

### Issues

1. **Security (High)**: Password stored in plain text at line 34
   ```csharp
   user.Password = request.Password; // ❌ Must hash!
   ```

2. **Missing validation (Medium)**: No null check on email at line 28

### Suggestions

- Consider adding rate limiting to prevent brute force
- The JWT expiry of 30 days seems long - recommend 24 hours

### Good Patterns ✓

- Clean separation of AuthService from controller
- Proper async/await usage throughout

```

---

## 8.6 Business Analyst Agent

**File:** `agents/business-analyst-agent.md`  
**Capability:** `analysis`  
**Priority:** 50  
**RAG Usage:** Medium (architecture, existing patterns)

### Purpose

Turn Enriched issue context into a concrete implementation plan with steps.

### Implementation

```markdown
# Business Analyst Agent

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- analysis

## Tags

- planning
- requirements
- breakdown

## System Prompt

You are a technical business analyst. Turn requirements into implementation plans.

{{#if context.RagContext}}
Architecture and existing patterns:
{{context.RagContext}}
{{/if}}

Enriched issue context:
{{context.EnrichedIssue}}

Create an implementation plan with:

1. **Summary** - One paragraph on what we're building
2. **Steps** - Ordered list of implementation tasks
   - Each step should be atomic (one agent can do it)
   - Include the capability needed (coding, documentation, etc.)
   - Estimate complexity (small/medium/large)
3. **Files to modify** - List of files that will change
4. **Dependencies** - What must exist before we start
5. **Acceptance criteria** - How we know it's done

Format each step as:
### Step N: [Title]
- **Capability**: coding/documentation/etc
- **Complexity**: small/medium/large
- **Description**: What to do
- **Files**: Which files to create/modify
```

### Silver Thread

```
Input (from Issue Enricher):
## Issue: Add password reset functionality
### Context: AuthService exists, uses JWT, SendGrid for email
### Acceptance: User can reset password via email link

Business Analyst Output:

## Summary
Add password reset flow: user requests reset, gets email with token, clicks link, sets new password.

## Steps

### Step 1: Add reset token to User model
- **Capability**: coding
- **Complexity**: small
- **Files**: src/Models/User.cs, migrations/

### Step 2: Create password reset endpoints
- **Capability**: coding
- **Complexity**: medium
- **Files**: src/Controllers/AuthController.cs, src/Services/AuthService.cs

### Step 3: Implement email sending
- **Capability**: coding
- **Complexity**: medium
- **Files**: src/Services/EmailService.cs

### Step 4: Add reset password UI
- **Capability**: coding
- **Complexity**: medium
- **Files**: src/Pages/ResetPassword.razor

### Step 5: Write tests
- **Capability**: coding
- **Complexity**: medium
- **Files**: tests/AuthServiceTests.cs

### Step 6: Update documentation
- **Capability**: documentation
- **Complexity**: small
- **Files**: README.md, docs/auth.md

## Acceptance Criteria
- [ ] User can request reset with email
- [ ] Reset email is sent within 30 seconds
- [ ] Token expires after 1 hour
- [ ] User can set new password
- [ ] Old password no longer works
```

---

## 8.7 Issue Enricher Agent

**File:** `agents/issue-enricher-agent.md`  
**Capability:** `Enrichment`  
**Priority:** 50  
**RAG Usage:** Heavy (this is the RAG showcase)

### Purpose

Turn raw issue text ("fix the login bug") into structured, researched context that the Business Analyst can work with.

### Implementation

```markdown
# Issue Enricher Agent

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- Enrichment

## Tags

- issue-processing
- research
- context-gathering

## Tools

- rag_search: Search indexed codebase
- git_history: Find related commits/PRs

## System Prompt

You are an issue researcher. Turn vague requests into structured, actionable context.

Raw issue:
{{context.RawIssue}}

{{#if context.WorkspacePath}}
Workspace: {{context.WorkspacePath}}
{{/if}}

Your job:
1. **Search** the codebase for relevant code (use rag_search tool)
2. **Find** related PRs, commits, issues (use git_history tool)
3. **Identify** likely problem areas
4. **Structure** the output for the Business Analyst

Output format:

## Issue: [Clarified title]

### Raw Input
[Original user text]

### Context (from codebase search)
[Relevant files, functions, classes found via RAG]

### Related History
[Past PRs, commits, issues that touch similar areas]

### Likely Problem Areas
[Files and line ranges to investigate]

### Suggested Acceptance Criteria
[What "done" looks like - user-verifiable statements]

### Recommended Approach
[High-level suggestion based on codebase patterns]
```

### Silver Thread

```
User input: "fix the login bug"

Issue Enricher:
1. Searches RAG: "login", "authentication", "auth"
2. Finds: AuthService.cs, LoginController.cs, recent changes
3. Searches git: recent commits to auth files
4. Finds: PR #45 modified token validation last week

Output:

## Issue: Fix login failure after token validation change

### Raw Input
"fix the login bug"

### Context (from codebase search)
- `src/Services/AuthService.cs` - Main auth logic
  - `ValidateToken()` method at line 45-67
  - Uses JWT with 24-hour expiry
- `src/Controllers/LoginController.cs` - Handles POST /login
- `src/Models/User.cs` - User entity

### Related History
- **PR #45** (merged 3 days ago): "Update token validation"
  - Changed JWT signing algorithm from HS256 to RS256
  - Modified by @developer
- **Issue #23** (6 months ago): Similar login failure, was token expiry

### Likely Problem Areas
- `src/Services/AuthService.cs:45-67` - ValidateToken changed in PR #45
- `appsettings.json` - JWT configuration may be mismatched

### Suggested Acceptance Criteria
- [ ] User can log in with valid credentials
- [ ] Invalid credentials return 401 with clear message
- [ ] JWT token works for authenticated endpoints
- [ ] Token refresh works correctly

### Recommended Approach
Check if RS256 public key is correctly configured after PR #45 changes.
The asymmetric signing requires both public and private keys to be set.
```

---

## 8.8 Roslyn Agent (Coded)

**File:** `src/Aura.Foundation/Agents/RoslynAgent.cs`  
**Capability:** `coding`  
**Languages:** `csharp`  
**Priority:** 30 (specialist)  
**RAG Usage:** Optional (existing code patterns)

### Purpose

C# code generation with Roslyn compilation validation. Iterates until code actually compiles.

### Why Coded (Not Markdown)?

- Needs Roslyn SDK for compilation
- Complex iteration logic
- Tool integration for dotnet build

### Implementation

```csharp
public class RoslynAgent : IAgent
{
    private readonly ILlmProvider _llm;
    private readonly IRoslynValidator _validator;
    private readonly ILogger<RoslynAgent> _logger;
    
    public string AgentId => "roslyn-agent";
    
    public AgentMetadata Metadata => new(
        Name: "Roslyn Agent",
        Description: "C# code generation with compilation validation",
        Capabilities: ["coding"],
        Priority: 30,
        Languages: ["csharp"],
        Provider: "ollama",
        Model: "qwen2.5-coder:7b");
    
    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        var prompt = BuildPrompt(context);
        var code = await _llm.GenerateAsync(prompt, ct);
        
        // Iterate until it compiles
        var iteration = 0;
        const int maxIterations = 5;
        
        while (iteration < maxIterations)
        {
            var validation = await _validator.ValidateAsync(code, context.WorkspacePath, ct);
            
            if (validation.Success)
            {
                _logger.LogInformation("Code compiled successfully after {Iterations} iterations", iteration + 1);
                return new AgentOutput(Content: code, Artifacts: new Dictionary<string, object>
                {
                    ["compiled"] = true,
                    ["iterations"] = iteration + 1
                });
            }
            
            // Fix errors
            var fixPrompt = BuildFixPrompt(code, validation.Errors);
            code = await _llm.GenerateAsync(fixPrompt, ct);
            iteration++;
        }
        
        throw new AgentException(
            AgentErrorCode.MaxIterationsExceeded,
            $"Code did not compile after {maxIterations} iterations");
    }
    
    private string BuildPrompt(AgentContext context) => $"""
        You are an expert C# developer. Write clean, production-ready C# code.
        
        Project: {context.WorkspacePath}
        
        {(context.RagContext is not null ? $"Existing code patterns:\n{context.RagContext}" : "")}
        
        Request: {context.Prompt}
        
        Requirements:
        - Follow C# conventions and .NET best practices
        - Include XML documentation comments
        - Handle errors appropriately
        - Code must compile with dotnet build
        """;
    
    private string BuildFixPrompt(string code, IReadOnlyList<CompilationError> errors) => $"""
        The following C# code has compilation errors:
        
        ```csharp
        {code}
        ```
        
        Errors:
        {string.Join("\n", errors.Select(e => $"- {e.Id}: {e.Message} at line {e.Line}"))}
        
        Fix the errors and return the corrected code. Make minimal changes.
        """;
}
```

### Silver Thread

```
User: "Add a method to validate JWT tokens to AuthService"

Roslyn Agent:
1. Generates initial code
2. Runs dotnet build
3. Error: CS0246 'JwtSecurityToken' not found
4. Adds using statement, regenerates
5. Runs dotnet build
6. Success!

Output:
```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

public class AuthService
{
    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, _validationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
```

✓ Compiled successfully (2 iterations)

```

---

## Testing Strategy

Each agent should have:

1. **Unit tests** - Mock LLM, verify prompt construction
2. **Integration tests** - Real LLM (Ollama), verify output quality
3. **Silver thread test** - End-to-end scenario with expected outcome

Example test structure:

```csharp
public class CodingAgentTests
{
    [Fact]
    public async Task GeneratesValidPythonCode()
    {
        // Arrange
        var agent = CreateAgent();
        var context = new AgentContext(
            Prompt: "Write a function to add two numbers",
            WorkspacePath: null);
        
        // Act
        var result = await agent.ExecuteAsync(context, CancellationToken.None);
        
        // Assert
        result.Content.Should().Contain("def ");
        result.Content.Should().Contain("return");
    }
}
```

---

## Implementation Checklist

- [ ] 8.1 Chat Agent (`agents/chat-agent.md`)
- [ ] 8.2 Coding Agent (`agents/coding-agent.md`)
- [ ] 8.3 Documentation Agent (`agents/documentation-agent.md`)
- [ ] 8.4 Build Fixer Agent (`agents/build-fixer-agent.md`)
- [ ] 8.5 Code Review Agent (`agents/code-review-agent.md`)
- [ ] 8.6 Business Analyst Agent (`agents/business-analyst-agent.md`)
- [ ] 8.7 Issue Enricher Agent (`agents/issue-enricher-agent.md`)
- [ ] 8.8 Roslyn Agent (`src/Aura.Foundation/Agents/RoslynAgent.cs`)
- [ ] Update MarkdownAgentLoader to parse Languages field
- [ ] Update AgentRegistry with GetByCapability(capability, language)
- [ ] Add agent tests
- [ ] Silver thread end-to-end test

## Dependencies

- **Phase 1**: Core infrastructure (IAgent, AgentRegistry)
- **Phase 2**: LLM providers (Ollama)
- **RAG**: Issue Enricher needs RAG to be useful (can stub initially)
- **Roslyn SDK**: For RoslynAgent compilation validation
