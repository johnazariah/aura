# Agent Discovery and Capability-Based Selection

**Version:** 1.0  
**Status:** Draft  
**Last Updated:** 2025-11-27

## Overview

This specification defines how agents are discovered, registered, and selected for tasks. It covers the agent sources, capability-based selection with priority, and the end-user experience in VS Code.

## Agent Sources

Agents come from exactly two sources:

```text
┌─────────────────────────────────────────────────────────────┐
│                    Agent Discovery                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. MARKDOWN AGENTS (user-extensible)                        │
│     └── agents/*.md                                          │
│     └── Hot-reload on file change                            │
│     └── ConfigurableAgent wraps them                         │
│     └── Primary extensibility mechanism                      │
│                                                              │
│  2. CODED AGENTS (ship with Aura)                            │
│     └── IAgent implementations                               │
│     └── Registered via DI at startup                         │
│     └── For complex logic (Roslyn, APIs, RAG-aware)          │
│     └── Updated via Aura releases                            │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

**No external registration.** Agents don't "announce themselves" to Aura.

- Markdown agents = drop a file
- Coded agents = ship with Aura release

This keeps the system simple and predictable for end users (mum's bible study machine).

### Capability-Based Selection with Priority

Agents declare capabilities and priority. Lower priority = more specialized = selected first.

```csharp
public sealed record AgentMetadata(
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,   // Fixed: chat, coding, analysis
    int Priority = 50,                     // Lower = selected first
    IReadOnlyList<string>? Languages,     // null = polyglot, ["csharp"] = specialist
    string Provider = "ollama",
    string Model = "qwen2.5-coder:7b",
    double Temperature = 0.7,
    IReadOnlyList<string>? Tags = null,   // Open vocabulary, for display/filtering
    IReadOnlyList<string>? Tools = null);
```

### Two-Tier Model: Capabilities vs Tags

See [ADR-011](../adr/011-two-tier-capability-model.md) for decision rationale.

**Capabilities (fixed, for routing):**

| Capability | Description |
|------------|-------------|
| `chat` | General conversation (fallback) |
| `digestion` | Turn raw issue text into structured, researched context |
| `analysis` | Break down requirements into implementation plan |
| `coding` | Write/modify code (implementation, tests, refactoring) |
| `fixing` | Iterate on build/test errors until passing |
| `documentation` | Write/update READMEs, CHANGELOGs, API docs |
| `review` | Review code, suggest improvements |

**Languages (optional filter for coding):**

Agents can declare which languages they support. Null/empty means polyglot.

| Language | Examples |
|----------|----------|
| `csharp` | C#, .NET |
| `fsharp` | F# |
| `python` | Python |
| `javascript` | JavaScript |
| `typescript` | TypeScript |
| `java` | Java |
| `go` | Go |
| `rust` | Rust |

**Tags (open, for display/filtering):**

User-defined strings like `bible-study`, `theology`, `finance`, `tdd`, `refactoring`. Not used for routing.

### Priority Semantics

| Range | Meaning | Examples |
|-------|---------|----------|
| 10-30 | Specialist | RoslynAgent (C# only) |
| 40-60 | Domain expert | .NET Agent (C# + F#), BibleStudyAgent |
| 70-90 | Generalist | CodingAgent (polyglot), ChatAgent |

### Selection Algorithm

```csharp
// "I need a coding agent for C#"
var agents = registry.GetByCapability("coding", language: "csharp");
// Returns agents where:
//   1. Has "coding" capability, AND
//   2. Languages is null (polyglot) OR Languages contains "csharp"
// Sorted by priority

// Result: [RoslynAgent(30, csharp), .NETAgent(40, csharp+fsharp), CodingAgent(70, polyglot)]

var best = registry.GetBestForCapability("coding", language: "csharp");
// Returns: RoslynAgent

// For F#:
var fsharpAgent = registry.GetBestForCapability("coding", language: "fsharp");
// Returns: .NETAgent (RoslynAgent doesn't do F#)

// For Rust:
var rustAgent = registry.GetBestForCapability("coding", language: "rust");
// Returns: CodingAgent (polyglot fallback)
```

### LLM Providers: Local Default, Cloud Opt-In

Agents run locally but can use any LLM provider:

```text
┌─────────────────────────────────────────────────────────────┐
│  Agent (always local)                                        │
│  └── Specifies: Provider + Model                             │
│      └── Provider: "ollama" → local GPU/CPU (default)       │
│      └── Provider: "deepseek" → cloud API (opt-in)          │
│      └── Provider: "azure-openai" → cloud API (opt-in)      │
└─────────────────────────────────────────────────────────────┘
```

The agent logic stays local (privacy preserved). Only the LLM inference can optionally go to cloud.

```markdown
# Deep Analysis Agent

## Metadata

- **Provider**: deepseek
- **Model**: deepseek-coder-v3

## Capabilities

- deep-analysis
- complex-reasoning
```

### Default Chat Agent

Ships with Aura as `agents/chat-agent.md`. This is the fallback - always available, handles anything not matched by a specialist.

## Default Agent Set

Aura ships with these agents out of the box:

| Agent | Capability | Languages | Priority | Type | Description |
|-------|------------|-----------|----------|------|-------------|
| **Chat Agent** | `chat` | - | 80 | Markdown | General conversation fallback |
| **Issue Digester** | `digestion` | - | 50 | Markdown | Raw issue → structured context with RAG |
| **Business Analyst** | `analysis` | - | 50 | Markdown | Requirements → implementation plan |
| **Coding Agent** | `coding` | polyglot | 70 | Markdown | Write code, tests, refactoring |
| **Roslyn Agent** | `coding` | csharp | 30 | Coded | C# with compilation validation |
| **Build Fixer** | `fixing` | polyglot | 50 | Markdown | Iterate on errors until green |
| **Documentation Agent** | `documentation` | - | 50 | Markdown | READMEs, CHANGELOGs, API docs |
| **Code Review Agent** | `review` | - | 50 | Markdown | Review code, suggest improvements |

### Workflow Example

```text
┌─────────────────────────────────────────────────────────────┐
│  User: "fix the login bug"                                   │
│                                                              │
│  1. Issue Digester (digestion)                               │
│     → Researches codebase, adds context, acceptance criteria │
│                                                              │
│  2. Business Analyst (analysis)                              │
│     → Creates implementation plan with steps                 │
│                                                              │
│  3. Roslyn Agent (coding + csharp)                           │
│     → Writes the fix                                         │
│                                                              │
│  4. Build Fixer (fixing)                                     │
│     → Iterates until it compiles and tests pass              │
│                                                              │
│  5. Documentation Agent (documentation)                      │
│     → Updates CHANGELOG                                      │
│                                                              │
│  6. Code Review Agent (review)                               │
│     → Reviews the PR, suggests improvements                  │
└─────────────────────────────────────────────────────────────┘
```

## Silver Thread: What You See in VS Code

When this is complete, here's the end-to-end experience:

### 1. Agents Panel

```text
AGENTS
├── 📋 Issue Digester (digestion) [50]
├── 📊 Business Analyst (analysis) [50]
├── ⚙️ Roslyn Agent (coding) [csharp] [30]      ← Coded, C# specialist
├── 💻 Coding Agent (coding) [polyglot] [70]
├── 🔧 Build Fixer (fixing) [50]
├── 📝 Documentation Agent (documentation) [50]
├── 🔍 Code Review Agent (review) [50]
└── 💬 Chat Agent (chat) [80]                   ← Fallback
```

**Interactions:**

- Click agent → See details (capabilities, provider, model)
- See which agents are available for each capability
- Agents sorted by priority (specialists first)

### 2. Chat with Agent Selection

```text
┌─────────────────────────────────────────────────────────────┐
│ Chat                                            [Agent: ▼]  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ You: Write a C# authentication service                      │
│                                                              │
│ Aura [via Roslyn Agent]:                                    │
│ I'll create an authentication service. Here's the code:     │
│                                                              │
│ ```csharp                                                   │
│ public class AuthService : IAuthService                     │
│ {                                                           │
│     ...                                                     │
│ }                                                           │
│ ```                                                         │
│                                                              │
│ [Apply to workspace] [Copy]                                 │
│                                                              │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Type a message...                              [Send]   │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**What happened:**

1. User asked for C# code
2. System matched "csharp-coding" capability
3. Roslyn Agent (priority 30) was selected over C# Agent (50) and Coding Agent (70)
4. Response shows which agent handled it

**Agent override:**

- Dropdown lets user pick a different agent
- "Chat Agent" always available as fallback

### 3. Agent Details View

Click on an agent to see:

```text
┌─────────────────────────────────────────────────────────────┐
│ Roslyn Agent                                                 │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ Capabilities: csharp-coding, csharp-validation, refactoring │
│ Priority: 30 (Specialist)                                    │
│ Provider: ollama                                             │
│ Model: qwen2.5-coder:7b                                      │
│                                                              │
│ Description:                                                 │
│ Generates C# code with Roslyn-based compilation and         │
│ validation. Iterates until code compiles successfully.       │
│                                                              │
│ Source: Coded (ships with Aura)                              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 4. Status Bar

```text
┌─────────────────────────────────────────────────────────────┐
│ Aura: Connected | Ollama: 3 models | Agents: 5              │
└─────────────────────────────────────────────────────────────┘
```

Quick health check - is everything running?

## Implementation Changes Required

### 1. AgentMetadata: Tags → Capabilities + Priority

```csharp
// Before
public sealed record AgentMetadata(
    IReadOnlyList<string>? Tags = null);

// After  
public sealed record AgentMetadata(
    IReadOnlyList<string> Capabilities,
    int Priority = 50,
    ...);
```

### 2. IAgentRegistry: Add Capability Methods

```csharp
public interface IAgentRegistry
{
    // Existing
    IReadOnlyList<IAgent> Agents { get; }
    IAgent? GetAgent(string agentId);
    
    // New: capability-based selection
    IReadOnlyList<IAgent> GetByCapability(string capability);
    IAgent? GetBestForCapability(string capability);
    
    // Remove: GetAgentsByTags (replaced by GetByCapability)
}
```

### 3. MarkdownAgentLoader: Parse Capabilities + Priority

```markdown
## Metadata

- **Priority**: 30
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- csharp-coding
- validation
- refactoring
```

### 4. API Endpoints

```http
GET /api/agents
→ [{ id, name, capabilities, priority, provider, model }]

GET /api/agents?capability=csharp-coding
→ Agents matching capability, sorted by priority

GET /api/agents/best?capability=csharp-coding
→ Single best agent for capability
```

### 5. Create Default Chat Agent

`agents/chat-agent.md` ships with Aura.

## Design Constraints

- **Simple mental model** - drop a file or update Aura
- **Predictable selection** - capability + priority, deterministic
- **Local-first preserved** - agent logic always local
- **User-extensible** - markdown agents for customization
- **Single-machine** - no distributed agents (matches Aspire scope)

## Related Specifications

- [01-agents.md](01-agents.md) - Agent architecture and interfaces
- [02-llm-providers.md](02-llm-providers.md) - LLM provider abstraction
- [06-extension.md](06-extension.md) - VS Code extension UI
