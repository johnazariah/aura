# Aura Demo Script (40 minutes)

> **Audience**: Developers unfamiliar with Aura
> **Goal**: Show how Aura makes AI-assisted development practical and safe

---

## Pre-Demo Checklist (5 min before)

```powershell
# 1. Start API (if not running)
.\scripts\Start-Api.ps1

# 2. Verify health
curl http://localhost:5300/health

# 3. Index the workspace (takes ~2-3 min for Aura repo)
curl -X POST "http://localhost:5300/api/workspaces" `
  -H "Content-Type: application/json" `
  -d '{"path": "c:/work/aura", "name": "Aura"}'

# 4. Check indexing status
curl "http://localhost:5300/api/workspaces"

# 5. Open VS Code with Aura extension
code c:\work\aura
```

---

## Part 1: The Problem (3 min)

**Talking points:**
- "AI coding assistants are powerful but lack context"
- "They don't know YOUR codebase, YOUR patterns, YOUR architecture"
- "Aura bridges that gap - it indexes and understands your code"

**Visual**: Show the Aura sidebar in VS Code
- Status view (healthy API, Ollama running)
- Agent tree (specialized agents for different tasks)

---

## Part 2: Semantic Code Search (5 min)

**Script:**
> "Let's say I want to understand how Aura handles LLM providers. Instead of grepping for keywords, I can ask semantically."

**Demo command:**
```powershell
curl -s -X POST "http://localhost:5300/api/rag/search" `
  -H "Content-Type: application/json" `
  -d '{"query": "how do we switch between different LLM providers", "limit": 5}'
```

**Expected result**: Returns relevant code from `ILlmProviderRegistry`, `LlmProviderRegistry`, provider implementations.

**Then in Copilot Chat (if MCP configured):**
```
@aura how does the LLM provider registry work?
```

**Key point**: "It found the right code based on intent, not exact keywords."

---

## Part 3: Code Navigation (5 min)

**Script:**
> "Now I want to understand the relationships - who uses this code?"

**Demo commands:**
```powershell
# Find implementations of an interface
curl -s "http://localhost:5300/api/graph/implementations/ILlmProvider"

# Find who calls a method
curl -s "http://localhost:5300/api/graph/callers/ExecuteAsync"

# Explore type members
curl -s "http://localhost:5300/api/graph/members/ReActExecutor"
```

**Or in Copilot Chat:**
```
@aura show me all implementations of ILlmProvider
@aura who calls the ExecuteAsync method in ReActExecutor?
@aura what methods does TokenTracker have?
```

**Key point**: "10 seconds vs. 10 minutes of manual exploration."

---

## Part 4: Workflow Demo (12 min) ⭐

**Script:**
> "This is the main event. Let's take a real task and watch Aura help us implement it."

### 4a. Create a Workflow (3 min)

**In VS Code**: Open the Workflow panel (Aura icon in sidebar)

**Click "Create Workflow" and use this sample issue:**
```
Title: Add request timeout configuration to LLM providers

Description:
Currently LLM calls can hang indefinitely. We need:
1. Add a Timeout property to LlmProviderConfig
2. Apply the timeout in AzureOpenAIProvider.ChatAsync
3. Default to 30 seconds if not specified
```

**Or via API:**
```powershell
curl -s -X POST "http://localhost:5300/api/developer/workflows" `
  -H "Content-Type: application/json" `
  -d '{
    "title": "Add request timeout configuration to LLM providers",
    "description": "Currently LLM calls can hang indefinitely. Add Timeout property to LlmProviderConfig, apply in AzureOpenAIProvider.ChatAsync, default 30 seconds.",
    "repositoryPath": "c:/work/aura"
  }'
```

### 4b. Enrich with Context (2 min)

**Click "Analyze"** or:
```powershell
# Get workflow ID from previous response
curl -s -X POST "http://localhost:5300/api/developer/workflows/{id}/analyze"
```

**Show the enriched context**: Aura found LlmProviderConfig, AzureOpenAIProvider, relevant patterns.

### 4c. Generate Plan (3 min)

**Click "Plan"** or:
```powershell
curl -s -X POST "http://localhost:5300/api/developer/workflows/{id}/plan"
```

**Show the generated steps:**
- Step 1: Add Timeout property to LlmProviderConfig
- Step 2: Update AzureOpenAIProvider to use timeout
- Step 3: Add unit tests
- etc.

**Key point**: "The AI understands the codebase structure and creates a realistic plan."

### 4d. Execute with Human-in-the-Loop (4 min)

**Execute Step 1** - show the proposed code change

**Options:**
- ✅ Approve - apply the change
- ❌ Reject - try again with feedback
- 💬 Chat - ask questions or refine
- ⏭️ Skip - move on

**Key point**: "You're always in control. The AI proposes, you decide."

---

## Part 5: Refactoring Tools (7 min)

**Script:**
> "What about refactoring? This is where understanding the whole codebase matters."

### Safe Rename Demo

**In Copilot Chat:**
```
@aura analyze renaming TokenTracker to ContextBudgetTracker
```

**Show the blast radius:**
- X files affected
- Y references found
- Related symbols discovered

**Then execute (or just show the analysis):**
```
@aura rename TokenTracker to ContextBudgetTracker
```

**Key point**: "Semantic refactoring, not find-replace. It knows the difference between a class name and a comment."

### Show Other Refactorings (briefly):
```
@aura extract the validation logic from line 45-60 in UserService.cs into a new method
@aura show me all callers of ValidateEmail
```

---

## Part 6: Agent Architecture (5 min)

**Script:**
> "Under the hood, Aura uses specialized agents."

**Show the agent list in VS Code** or:
```powershell
curl -s "http://localhost:5300/api/agents" | ConvertFrom-Json | 
  Select-Object -ExpandProperty agents | 
  Select-Object id, name | Format-Table
```

**Highlight:**
- `roslyn-coding` - C# specialist with Roslyn tools
- `coding-agent` - Polyglot fallback
- `issue-enrichment-agent` - Gathers context for workflows
- Ingesters - Parse 30+ languages

**New capability (just merged):**
> "Agents can now spawn sub-agents for complex tasks and track their token budget."

```powershell
# Show the new tools
curl -s "http://localhost:5300/api/tools" | ConvertFrom-Json | 
  Select-Object -ExpandProperty tools | 
  Where-Object { $_.toolId -match "spawn|budget" } |
  Select-Object toolId, description
```

---

## Part 7: Wrap-Up (3 min)

**Key messages:**

1. **Local-first, privacy-safe**
   - "Everything runs on your machine"
   - "Your code never leaves your network"
   - "Works with Ollama (local) or Azure OpenAI (cloud)"

2. **Developer-in-control**
   - "AI proposes, you decide"
   - "Every change is reviewable before applying"
   - "No magic - transparent reasoning (ReAct)"

3. **Extensible**
   - "Add your own agents via markdown files"
   - "Add tools for your specific workflows"
   - "MCP integration with GitHub Copilot"

**Call to action:**
> "Try it on your own codebase. Index, search, navigate, and see what Aura finds."

---

## Backup Commands (if something fails)

```powershell
# Check API health
curl http://localhost:5300/health

# Restart if needed
.\scripts\Start-Api.ps1

# List workflows
curl "http://localhost:5300/api/developer/workflows"

# Get specific workflow
curl "http://localhost:5300/api/developer/workflows/{id}"

# Simple agent chat (always works)
curl -X POST "http://localhost:5300/api/agents/chat-agent/chat" `
  -H "Content-Type: application/json" `
  -d '{"message": "What is Aura and what can it do?"}'
```

---

## Timing Guide

| Section | Duration | Cumulative |
|---------|----------|------------|
| The Problem | 3 min | 3 min |
| Semantic Search | 5 min | 8 min |
| Code Navigation | 5 min | 13 min |
| Workflow Demo | 12 min | 25 min |
| Refactoring | 7 min | 32 min |
| Agent Architecture | 5 min | 37 min |
| Wrap-Up | 3 min | 40 min |

**Buffer**: If running short, cut Agent Architecture section.
**If running long**: Skip the execute step in workflow (just show plan).
