# Aura Developer Presentation - January 2025

**Duration**: 45 minutes  
**Audience**: Developers interested in AI-assisted development  
**Format**: Two parts with live demos

---

## Part 1: What is Aura? (20 min)

### Opening Hook (2 min)

> "What if you could have 5 AI agents working on your codebase simultaneously, each in its own git branch, while you continue coding on main?"

**Demo**: Show VS Code with Aura sidebar showing 3 concurrent workflows

### The Problem (5 min)

1. **Context Switching** - Every time you ask AI for help, you lose context
2. **The Copilot Ceiling** - Single-turn completions can't handle multi-step tasks
3. **Privacy vs Quality Tradeoff** - Local models = safe but weak. Cloud = powerful but risky

**Slide**: "AI is fast at writing wrong code"

### The Solution: Aura (5 min)

> "Index locally, generate with the best model"

**Architecture** (show diagram):

- Foundation: Agents, LLM abstraction, RAG, Tools
- Modules: Developer (now), Research (future), Personal (future)

**Key Insight**: Your code stays local. Only queries go to cloud.

### Live Demo: Developer Module (8 min)

#### Demo 1: RAG Search

```powershell
# Show semantic search
curl "http://localhost:5300/api/rag/search?query=workflow+execution&limit=5" | jq
```

**Show**: Results from 2,691 indexed chunks, 30+ languages

**Files to show**:

- `src/Aura.Module.Developer/Agents/TreeSitterIngesterAgent.cs` (lines 26-75)
  - The `SupportedLanguages` dictionary
  - "30+ languages with one parser"

#### Demo 2: Create a Workflow

```powershell
# Create a new workflow
$body = @{
    repositoryPath = "C:/work/aura"
    issue = @{
        source = "custom"
        title = "Add health check endpoint"
        body = "Add a /health endpoint that returns service status"
    }
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows" -Method POST -Body $body -ContentType "application/json"
```

**Show**: VS Code Aura sidebar updates with new workflow

#### Demo 3: Git Worktrees

**Show in terminal**:

```powershell
# Show worktrees
git worktree list
```

**Explain**: Each workflow gets isolated branch, no merge conflicts

---

## Part 2: How We Built Aura (20 min)

### The Meta Challenge (3 min)

> "We're building an AI system... using AI. How do you avoid the infinite recursion?"

**The trap**: AI writes code fast → Wrong code accumulates → Rewrite everything

### What Didn't Work (3 min)

1. **"Just let AI code"** - Got 38,000 lines of inconsistent code
2. **"Review everything"** - Couldn't keep up with AI output

**Reference origin-story**: hve-hack grew to 17 projects, 38k lines

### What Worked: Spec-Driven Development (5 min)

**The workflow**:

1. Human writes spec (requirements, constraints)
2. AI reads spec, proposes implementation
3. Human approves or adjusts
4. AI implements
5. Human verifies

**Files to show**:

- `.project/features/completed/concurrent-workflows.md` - Example spec
- `.github/prompts/aura.complete-feature.prompt.md` - The ceremony

**Key insight**: AI doesn't decide architecture, only implementation

### Extensibility Through Conversation (6 min)

> "A lot of the flexibility came from conversation between the human and the Agent"

Show 5 examples of "human-AI negotiation":

#### 1. Hot-Reload Agents (Human suggested)

**Conversation**:

- Human: "I want to edit agents without restarting"
- AI: "Let's use markdown files with FileSystemWatcher"

**Files**:

```
agents/coding-agent.md              # Just a markdown file!
```

**Code** (`src/Aura.Foundation/Agents/MarkdownAgentLoader.cs` lines 37-55):

```csharp
public async Task<IAgent?> LoadAsync(string filePath)
{
    var content = await _fileSystem.File.ReadAllTextAsync(filePath);
    var agentId = _fileSystem.Path.GetFileNameWithoutExtension(filePath);
    var definition = Parse(agentId, content);
    return _agentFactory.CreateAgent(definition);
}
```

#### 2. Pluggable LLM Providers (AI suggested registry pattern)

**Conversation**:

- Human: "I want to switch between Ollama and OpenAI easily"
- AI: "Let's create an ILlmProviderRegistry that resolves by config"

**Files**:

```
src/Aura.Foundation/Llm/ILlmProvider.cs
src/Aura.Foundation/Llm/OllamaProvider.cs
src/Aura.Foundation/Llm/OpenAiProvider.cs
src/Aura.Foundation/Llm/AzureOpenAiProvider.cs
```

**Config** (`src/Aura.Api/appsettings.json` lines 9-32):

```json
"Llm": {
  "DefaultProvider": "AzureOpenAI",
  "Providers": {
    "Ollama": { "BaseUrl": "http://localhost:11434", "DefaultModel": "qwen2.5-coder:7b" },
    "OpenAI": { "DefaultModel": "gpt-4o" },
    "AzureOpenAI": { "DefaultDeployment": "gpt-4o" }
  }
}
```

#### 3. Externalized Prompts (Joint decision)

**Conversation**:

- Human: "Prompts in code = hard to iterate"
- AI: "Handlebars templates? Hot-reload like agents?"
- Human: "Yes, with front-matter for metadata"

**Files**:

```
prompts/workflow-plan.prompt
prompts/step-execute.prompt
prompts/step-review.prompt
```

**Example** (`prompts/workflow-plan.prompt` lines 1-25):

```handlebars
---
description: Creates a focused execution plan
---
Create an implementation plan for this development task.

## Issue Title
{{title}}

## Issue Description
{{description}}
```

#### 4. Module System (AI proposed, human refined)

**Conversation**:

- Human: "I want Developer now, Research later, Personal eventually"
- AI: "IAuraModule interface - each module registers its own services"
- Human: "But no cross-module dependencies!"

**Files**:

```
src/Aura.Foundation/Modules/IAuraModule.cs
src/Aura.Module.Developer/DeveloperModule.cs
```

**Code** (`DeveloperModule.cs` lines 24-35):

```csharp
public sealed class DeveloperModule : IAuraModule
{
    public string ModuleId => "developer";
    public string Name => "Developer Workflow";
    public IReadOnlyList<string> Dependencies => []; // Only depends on Foundation
}
```

#### 5. TreeSitter for Multi-Language (Human researched, AI implemented)

**Conversation**:

- Human: "Roslyn is great for C# but I need Python, TypeScript, Rust..."
- AI: "TreeSitter has bindings for 30+ languages"
- Human: "Can you make a generic agent that handles all of them?"

**File** (`TreeSitterIngesterAgent.cs` lines 26-75):

```csharp
private static readonly Dictionary<string, LanguageConfig> SupportedLanguages = new()
{
    ["py"] = new("python", ["function_definition", "class_definition"]),
    ["ts"] = new("typescript", ["function_declaration", "interface_declaration"]),
    ["rs"] = new("rust", ["function_item", "impl_item", "struct_item"]),
    ["go"] = new("go", ["function_declaration", "method_declaration"]),
    // ... 30+ more
};
```

### The Pivot: Four Iterations (3 min)

**Slide: "The Graveyard of Ambition"**

| Iteration | Codename | Projects | Lines | What Happened |
|-----------|----------|----------|-------|---------------|
| 1 | **bird-constellation** (Owlet) | 6 | ~12k | Document indexing Windows service. Clean structure, but Windows-only focus |
| 2 | **birdlet** | 8 | ~18k | RAG-focused platform with A2A protocol. 779-line Program.cs! Mixed Python + C# |
| 3 | **hve-hack** (Agent Orchestrator) | 17 | ~38k | Full orchestration system. GitHub issue → AI → PR. Too complex |
| 4 | **Aura** | 4 | ~8k | Local-first foundation. Composable modules. You're looking at it |

**What each taught us**:

1. **bird-constellation**: ADRs are valuable, keep project count low
2. **birdlet**: RAG is a foundation service, don't mix languages
3. **hve-hack**: Markdown agents work, provider registry works, but don't build IExecutionPlanner

**Show ADR-009**: `.project/adr/009-lessons-from-previous-attempts.md`

**The November 25, 2025 decision**:
> "We've built this three times. Each time bigger, more complex.  
> What if we kept the *patterns* and deleted the *code*?"

**Philosophy shift**:
> From "How do we orchestrate everything?"  
> To "How do we make each piece excellent?"

**Numbers**:

- Before: 17 projects, ~38,000 lines (hve-hack)
- After: 4 projects, ~8,000 lines (Aura)
- Kept: Markdown agents, provider registry, coding standards, ADRs
- Deleted: IExecutionPlanner, AgentOutputValidator, A2A protocol, plugin system

---

## Wrap-Up (5 min)

### What I Learned

1. **AI is a power tool, not autopilot** - You still steer
2. **Specs are the leverage point** - Control input, not output
3. **Extensibility emerges from dialogue** - Talk to your AI
4. **Local-first is about data, not models** - Index locally, generate anywhere

### What's Next

- Research module (document exploration)
- Personal module (notes, calendar integration)
- Self-improvement (agents that improve agents)

### Resources

- GitHub: `github.com/your-org/aura`
- Documentation: `.project/` folder in repo
- This talk's notes: `.project/presentations/developer-talk-2025-01.md`

---

## Demo Checklist

Before the talk:

- [ ] Start API: `Start-Api` (user must run)
- [ ] Open VS Code with Aura extension
- [ ] Have 1-2 existing workflows visible
- [ ] Index some code: `curl http://localhost:5300/api/rag/index -X POST`
- [ ] Have terminal ready for curl commands

Files to have open:

1. `agents/coding-agent.md`
2. `src/Aura.Api/appsettings.json`
3. `prompts/workflow-plan.prompt`
4. `src/Aura.Foundation/Modules/IAuraModule.cs`
5. `src/Aura.Module.Developer/Agents/TreeSitterIngesterAgent.cs`

---

## Quick Reference: File Locations

| Concept | File | Lines |
|---------|------|-------|
| Agent definition | `agents/coding-agent.md` | All |
| Agent loader | `src/Aura.Foundation/Agents/MarkdownAgentLoader.cs` | 37-55 |
| LLM interface | `src/Aura.Foundation/Llm/ILlmProvider.cs` | 1-60 |
| Provider config | `src/Aura.Api/appsettings.json` | 9-32 |
| Prompt template | `prompts/workflow-plan.prompt` | 1-60 |
| Module interface | `src/Aura.Foundation/Modules/IAuraModule.cs` | 1-44 |
| Developer module | `src/Aura.Module.Developer/DeveloperModule.cs` | 24-80 |
| TreeSitter languages | `src/Aura.Module.Developer/Agents/TreeSitterIngesterAgent.cs` | 26-75 |
| Origin story | `.project/archive/origin-story.md` | All |

---

## Backup Slides

### If asked "Why not just use Cursor/Copilot Workspace?"

> "Those are great for single-developer, single-task work. Aura is for:
>
> - Concurrent workflows (multiple tasks in parallel)
> - Local indexing (your code never leaves your machine)
> - Extensible (add your own agents, modules, tools)
> - Self-hosted (run on your infrastructure)"

### If asked "What about security?"

> "Your code is indexed locally into PostgreSQL + pgvector.  
> Only the query + relevant chunks go to the LLM.  
> You can swap to local models (Ollama) anytime.  
> The architecture is provider-agnostic by design."

### If asked "How long did this take?"

> "The rewrite from hve-hack to Aura: ~3 weeks.  
> But that was after learning from 2+ months of the original.  
> The spec-driven approach made the rewrite 10x faster."
