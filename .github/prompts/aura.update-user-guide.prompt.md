# AURA User Guide Documentation - Comprehensive Guide

You are tasked with creating and maintaining comprehensive user-facing documentation for **Aura** , focusing on helping end users effectively use the platform to accelerate their development workflows.

## 🎯 Your Mission

Create detailed, practical documentation in the `docs/user-guide/` directory that teaches users how to use Aura effectively. The documentation must cover both **local-first workflows** (the primary use case) and **optional GitHub integration**.

## 🔑 Critical Context: Local-First Design

**IMPORTANT**: Aura is **local-first** by design:
- Users work entirely on their local machine using local LLM (OLLAMA)
- No cloud dependencies or API keys required for core functionality
- Git workspace-based (all workflows execute in a Git directory)
- **GitHub integration is OPTIONAL** - users can create workflows manually
- When GitHub IS used, it syncs issues to local workflows, then pushes results back

**This is not a GitHub-dependent tool. This is a local development automation tool with optional GitHub sync.**

## 📋 Required Documentation Structure

Create the following files in `docs/user-guide/`:

### 1. Overview (`index.md`)
- What is Aura and who is it for?
- Key concepts: workflows, agents, orchestration
- Local-first vs GitHub-integrated workflows
- When to use which approach
- Navigation guide to rest of user documentation

### 2. VS Code Extension Documentation (`extension/`)

#### `extension/overview.md`
- Extension capabilities and features
- How the extension connects to the Aura API
- Overview of the four main views (Workflow Panel, Agent Hub, Task Monitor, Insights)
- When to use the extension vs direct API calls

#### `extension/agent-hub.md`
- What is Agent Hub and why use it?
- Creating conversations with agents
- Sending messages and getting responses
- Multi-agent collaboration
- Use cases: code review, architecture discussions, debugging help
- **Screenshots/examples**: Show actual agent conversations

#### `extension/task-monitor.md`
- Real-time workflow tracking
- Understanding task states (pending, running, completed, failed)
- Progress indicators and what they mean
- How to drill down into task details
- **Screenshots/examples**: Show task monitor in action

#### `extension/workflows.md`
- Managing workflows from the Workflow Panel
- Creating new workflows (local vs GitHub sync)
- Executing workflows
- Viewing workflow history
- Filtering and organizing workflows
- **Screenshots/examples**: Show workflow panel UI

#### `extension/insights.md`
- What are Agent Insights?
- Understanding insight types (suggestions, patterns, optimizations, warnings)
- Using insights to improve workflows
- Applying actionable recommendations
- **Screenshots/examples**: Show insight cards

### 3. Workflow Types (`workflows/`)

#### `workflows/local-first.md` ⭐ **CRITICAL**
- **Emphasis**: This is the PRIMARY workflow mode
- Creating a local workflow without GitHub
- Workflow structure: title, description, workspacePath, repository
- Auto-formalization of free-form descriptions
- When to use local workflows (experimentation, private code, offline work)
- **Complete example**: From creating workflow to getting code/tests/docs
- **Command-line examples AND VS Code extension examples**

#### `workflows/github-integration.md`
- **Emphasis**: This is OPTIONAL enhancement
- Prerequisites: GitHub PAT, repository configuration
- How GitHub integration works (sync issues → local workflow → PR back to GitHub)
- Authenticating with GitHub
- Syncing issues from repositories
- Creating workflows from GitHub issues
- Automatic PR creation after workflow completion
- Monitoring PR status and feedback
- **Complete example**: GitHub issue → Aura workflow → merged PR

#### `workflows/issue-to-pr.md`
- End-to-end walkthrough: GitHub issue to merged pull request
- Step-by-step with screenshots/commands
- What happens at each stage (BA analysis, code gen, tests, docs, PR)
- Reviewing and iterating on generated code
- Handling validation failures
- Merging the PR

### 4. Workflow Patterns (`workflows/`)

Each of these should include:
- Use case description
- Example workflow description
- What agents are involved
- Expected output
- Common pitfalls and how to avoid them
- Real code examples

#### `workflows/code-generation.md`
- Generating new code from requirements
- Best practices for describing what you want
- How the Coding Agent works
- Review and iteration
- Examples: REST API endpoint, domain model, service layer

#### `workflows/test-creation.md`
- Generating tests for existing code
- How the Testing Agent works
- xUnit patterns Aura uses
- Achieving high coverage
- Examples: Unit tests, integration tests

#### `workflows/documentation.md`
- Auto-generating documentation
- What the Documentation Agent produces
- Customizing documentation output
- Examples: README sections, API docs, inline comments

#### `workflows/refactoring.md`
- Using Aura for code refactoring
- How Roslyn integration enables semantic refactoring
- Validation loop ensures refactoring doesn't break code
- Examples: Extract method, rename symbols, simplify code

### 5. Best Practices (`best-practices.md`)

- **Writing effective workflow descriptions**
  - Be specific vs vague
  - Include acceptance criteria
  - Mention constraints and requirements
  - Examples of good vs bad descriptions

- **Getting the best results from agents**
  - How to iterate on output
  - When to split workflows into smaller pieces
  - Using Agent Hub for clarification
  - Leveraging RAG knowledge

- **Workflow organization**
  - Naming conventions
  - When to use local vs GitHub workflows
  - Managing multiple concurrent workflows

- **Troubleshooting common issues**
  - LLM timeout/errors
  - Compilation failures
  - Test failures
  - Git conflicts
  - Links to detailed troubleshooting guide

## 🔍 What to Review

Before writing, thoroughly examine:

### Source Code
- `extension/src/` - VS Code extension implementation
- `extension/README.md`, `extension/QUICKSTART.md` - Current extension docs
- `src/AgentOrchestrator.Api/Endpoints/` - API endpoints to understand capabilities
- `agents/*.md` - Agent definitions to understand what each agent does

### Existing Documentation
- `docs/USAGE.md` - Current usage guide (migrate/update content)
- `docs/AURA-DEVELOPER-GUIDE.md` - Developer-focused guide
- `docs/CONFIGURATION.md` - Configuration options
- Current README.md - Quick start examples

### Extension Code for UI Components
- Agent Hub implementation
- Task Monitor implementation
- Workflow Panel implementation
- Insights implementation

## ✍️ Writing Guidelines

### Tone and Style
1. **User-friendly**: Write for developers who are new to Aura
2. **Practical**: Every concept backed by a concrete example
3. **Visual**: Describe UI elements clearly (screenshots ideal but describe thoroughly if no screenshots)
4. **Progressive**: Start simple, add complexity gradually
5. **Encouraging**: Make users feel confident they can succeed

### Structure Every Page
```markdown
# Page Title

> **Quick Summary**: One sentence about what this page covers

[Brief introduction paragraph]

## What You'll Learn
- Bullet point 1
- Bullet point 2

## Prerequisites (if any)
- What users need before following this guide

## Main Content
[Organized with clear headings]

## Examples
[At least one complete, realistic example]

## Common Issues
[Address predictable problems]

## Next Steps
- Link to related guides
```

### Code Examples
- Use **both** VS Code extension UI and direct API curl commands
- Show complete request/response bodies
- Include expected output
- Use realistic data (not "foo", "bar", "example")
- Highlight the local-first approach first, GitHub integration as optional

### Local-First Emphasis
When documenting workflows:
1. **Always show local workflow first**
2. Then show GitHub integration as "You can also..."
3. Never imply GitHub is required
4. Make it clear local workflows are fully functional

Example structure:
```markdown
## Creating a Workflow

### Local Workflow (Recommended for Getting Started)

[Local workflow example]

This approach requires no external accounts or setup beyond OLLAMA.

### GitHub Integration (Optional)

If you want to sync with GitHub issues:

[GitHub workflow example]

This requires GitHub authentication and repository configuration.
```

## 📊 Quality Criteria

Your user guide is successful if:

- ✅ A new user can complete their first local workflow in 10 minutes
- ✅ Local-first workflow is clearly the primary, fully-featured approach
- ✅ GitHub integration is presented as optional enhancement, not requirement
- ✅ Every major feature has a practical example
- ✅ Extension UI components are clearly explained
- ✅ Common questions are proactively answered
- ✅ Users know when to use Agent Hub vs Workflow Panel vs Task Monitor
- ✅ Troubleshooting guidance prevents frustration
- ✅ Examples use realistic scenarios (not toy examples)
- ✅ Navigation between guides is clear and helpful

## 🎯 Output Format

Create markdown files in `docs/user-guide/` following standard GitHub markdown structure.

Each file should:
- Start with a clear title (# heading)
- Have clear headings and subheadings
- Include navigation hints (links to related docs)
- Link to related documentation
- Include at least one complete example

Example frontmatter:
```markdown
---
title: Local-First Workflows
description: Learn how to create and execute workflows entirely on your local machine
---
```

## 🚀 Execution Priority

Focus on these files first (highest value):
1. `workflows/local-first.md` - Critical for new users
2. `extension/overview.md` - Understanding the extension
3. `index.md` - Navigation and orientation
4. `extension/workflows.md` - Primary extension interaction
5. `workflows/github-integration.md` - Optional enhancement
6. Rest of extension docs and workflow patterns

## 🎓 Remember

You're writing for developers who want to accelerate their work with AI but may be skeptical or new to AI-powered tools. Your job is to make them successful quickly, build their confidence, and show them the power of local-first AI automation.

**The local-first nature is Aura's superpower** - no data leaves their machine, no cloud dependencies, no vendor lock-in. Make this clear and compelling.

Now go create documentation that turns new users into power users! 🚀
