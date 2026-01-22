# AURA Examples & Tutorials Documentation

You are tasked with creating practical, hands-on examples and tutorials that show Aura in action with real-world scenarios.

## 🎯 Your Mission

Create two types of documentation:
1. **Examples** (`docs/examples/`) - Complete, copy-paste-ready examples showing Aura capabilities
2. **Tutorials** (`docs/tutorials/`) - Step-by-step guided learning experiences

## 📋 Examples (`examples/`)

### Purpose
Show Aura solving real development tasks. Each example is a complete, working scenario that users can copy and run.

### Required Example Files

#### 1. `index.md` - Examples Overview
- What examples are available
- How to use examples
- Prerequisites for running examples
- Quick navigation

#### 2. `calculator-app.md` - Simple Application
- **Scenario**: Generate a command-line calculator app
- **What's generated**: Calculator.cs, CalculatorTests.cs, README.md
- **Agents involved**: BA, Coder, Tester, Documentation
- **Complexity**: Beginner
- **Complete workflow description**
- **Full code output shown**
- **How to run and test**

#### 3. `rest-api.md` - REST API Generation
- **Scenario**: Create a REST API for managing books (CRUD)
- **What's generated**: 
  - Controllers/BooksController.cs
  - Models/Book.cs
  - Services/BookService.cs
  - Tests/BooksControllerTests.cs
  - README with API documentation
- **Agents involved**: BA, Coder, Tester, Documentation
- **Complexity**: Intermediate
- **Shows**: Clean architecture, dependency injection, API design

#### 4. `blazor-component.md` - Frontend Component
- **Scenario**: Generate a Blazor data table component with sorting/filtering
- **What's generated**:
  - Components/DataTable.razor
  - Components/DataTable.razor.cs
  - wwwroot/css/datatable.css
  - Tests/DataTableTests.cs
- **Complexity**: Intermediate
- **Shows**: Blazor patterns, component architecture, CSS generation

#### 5. `custom-ba-agent.md` - Custom Agent Creation
- **Scenario**: Create a specialized BA agent for microservices architecture
- **What's created**: Custom markdown agent definition
- **Shows**: 
  - Agent frontmatter configuration
  - Custom prompt engineering
  - Testing the custom agent
  - Using it in workflows
- **Complexity**: Advanced

#### 6. `multi-agent-workflow.md` - Complex Orchestration
- **Scenario**: Microservices API with multiple agents collaborating
- **What's generated**: Multiple services, shared models, API gateway
- **Agents involved**: Custom Architecture agent, Coder, Tester, Documentation, Security agent
- **Shows**: 
  - Complex orchestration
  - Custom agent integration
  - Multi-step workflows
  - Agent collaboration patterns
- **Complexity**: Advanced

### Example Template Structure
```markdown
# Example Title

> **Scenario**: Brief description of what we're building

## What You'll Learn
- Concept 1
- Concept 2

## Prerequisites
- Aura installed and running
- Workspace path: /path/to/workspace

## Workflow Description

**Goal**: [Clear statement]

**Input**:
```json
{
  "title": "...",
  "description": "..."
}
```

**Expected Output**: [What files are generated]

## Step-by-Step Execution

### 1. Create the Workflow
[Command or VS Code steps]

### 2. Execute and Monitor
[How to watch progress]

### 3. Review Generated Code
[What to look for in each file]

### 4. Test the Output
[How to run/verify]

## Generated Code

### File: src/Calculator.cs
```csharp
[Full generated code]
```

### File: tests/CalculatorTests.cs
```csharp
[Full generated test code]
```

## Understanding the Output

[Explain design decisions in generated code]

## Variations

[How to modify for different scenarios]

## Next Steps

[What to try next]
```

## 📋 Tutorials (`tutorials/`)

### Purpose
Guided learning experiences that teach Aura concepts through hands-on practice. Progressive difficulty.

### Required Tutorial Files

#### 1. `index.md` - Tutorials Overview
- Learning path recommendation
- Estimated time for each tutorial
- Prerequisites

#### 2. `01-hello-aura.md` - Tutorial 1: Your First Workflow
- **Goal**: Successfully run a local workflow start to finish
- **Duration**: 15 minutes
- **Topics**: Workflow creation, execution, monitoring, result review
- **Steps**:
  1. Create a simple workflow (Calculator class)
  2. Execute via VS Code extension
  3. Monitor in Task Monitor
  4. Review generated code
  5. Run the tests
  6. Understand what happened
- **Learning outcomes**: 
  - Understand workflow lifecycle
  - Know how to create local workflows
  - Can monitor progress
  - Can review outputs

#### 3. `02-custom-agent.md` - Tutorial 2: Create a Prompt-Based Agent
- **Goal**: Create a custom markdown agent
- **Duration**: 30 minutes
- **Topics**: Agent definitions, frontmatter, prompts, testing
- **Steps**:
  1. Understand agent anatomy
  2. Create `agents/my-custom-agent.md`
  3. Write frontmatter (id, name, provider, model)
  4. Craft the prompt
  5. Test with a workflow
  6. Iterate on the prompt
  7. Hot-reload in action
- **Learning outcomes**:
  - Can create markdown agents
  - Understand frontmatter options
  - Can test custom agents
  - Know prompt engineering basics

#### 4. `03-coded-agent.md` - Tutorial 3: Implement a Code-Based Agent
- **Goal**: Create a C# agent implementation
- **Duration**: 45 minutes
- **Topics**: IAgentExecutor, dependency injection, testing
- **Steps**:
  1. Create agent class (SecurityAnalyzerAgent)
  2. Implement IAgentExecutor
  3. Use ILlmProvider for LLM calls
  4. Handle context and results
  5. Write unit tests
  6. Register in DI container
  7. Use in a workflow
- **Learning outcomes**:
  - Can implement code-based agents
  - Understand IAgentExecutor interface
  - Can test agents properly
  - Know when to use code vs markdown

#### 5. `04-rag-integration.md` - Tutorial 4: Leverage RAG Knowledge
- **Goal**: Use RAG to generate code consistent with existing codebase
- **Duration**: 30 minutes
- **Topics**: Knowledge ingestion, RAG search, context-aware generation
- **Steps**:
  1. Understand what RAG knowledge is available
  2. Create workflow: "Add a new controller similar to existing ones"
  3. See how agent queries knowledge base
  4. Review generated code (should match patterns)
  5. Compare with/without RAG
- **Learning outcomes**:
  - Understand RAG benefits
  - Know how to leverage existing code
  - Can request pattern-aware generation

#### 6. `05-end-to-end.md` - Tutorial 5: GitHub Issue → Merged PR
- **Goal**: Complete workflow from GitHub issue to merged pull request
- **Duration**: 60 minutes
- **Topics**: GitHub integration, PR workflow, code review, merging
- **Steps**:
  1. Set up GitHub integration (PAT, repo config)
  2. Sync a GitHub issue
  3. Create workflow from issue
  4. Execute and monitor
  5. Review generated PR
  6. Handle PR feedback (optional: manual iteration)
  7. Merge the PR
  8. Verify issue auto-closes
- **Learning outcomes**:
  - End-to-end GitHub workflow
  - PR review process
  - Integration with team workflow
  - When to use GitHub vs local-first

### Tutorial Template Structure
```markdown
# Tutorial N: Title

> **What You'll Build**: Brief description

## Learning Objectives
- Objective 1
- Objective 2

## Prerequisites
- Completed: Tutorial X
- Installed: Y
- Knowledge: Z

## Estimated Time
30 minutes

## Step 1: [Action]

**Goal**: [What we're achieving in this step]

[Instructions]

```bash
# Commands
```

**Checkpoint**: [How to verify this step worked]

## Step 2: [Next Action]

[Continue pattern]

## What You Learned

[Summary of concepts covered]

## Troubleshooting

**Problem**: [Common issue]
**Solution**: [How to fix]

## Next Steps

- Try: [Suggested variation]
- Learn more: [Link to concept doc]
- Next tutorial: [Link]
```

## ✍️ Writing Guidelines

### Examples
- **Complete code**: Show full generated files
- **Realistic scenarios**: No toy examples
- **Copy-paste ready**: Users should be able to run exactly as written
- **Annotated**: Explain interesting parts of generated code

### Tutorials
- **Conversational tone**: "Now you'll create...", "Let's test this..."
- **Checkpoints**: Verify success before moving on
- **Troubleshooting**: Anticipate and address common issues
- **Celebrate wins**: "Great! You've created your first agent!"
- **Build confidence**: Progressive difficulty

## 📊 Quality Criteria

Examples succeed if:
- ✅ Every example runs without modification
- ✅ Generated code is production-quality
- ✅ Covers common real-world scenarios
- ✅ Code is fully shown and explained
- ✅ Variations/extensions are suggested

Tutorials succeed if:
- ✅ Student can complete without getting stuck
- ✅ Each step has clear verification
- ✅ Learning is progressive
- ✅ Student feels accomplished after each tutorial
- ✅ Troubleshooting prevents frustration

## 🔍 What to Review

- `extension/MVP-DEMO.md` - Demo scenarios
- `docs/DEMO-001.md`, `docs/DEMO-002.md` - Existing demos
- Generated code from actual workflow runs
- Common user questions/issues

Now create examples and tutorials that make learning Aura fun and rewarding! 🚀
