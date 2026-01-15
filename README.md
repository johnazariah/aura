# Aura

[![CI](https://github.com/johnazariah/aura/actions/workflows/ci.yml/badge.svg)](https://github.com/johnazariah/aura/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/johnazariah/aura/graph/badge.svg)](https://codecov.io/gh/johnazariah/aura)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Aura is an AI coding assistant that runs on your machine. It indexes your codebase, understands your code structure, and helps you implement features through a VS Code extension.

## What Can You Do With It?

### Start a coding task from a GitHub issue

```
Command: "Aura: Start Story from Issue"
→ Paste: https://github.com/org/repo/issues/42
→ Aura creates a git worktree and branch
→ Opens a new VS Code window in that worktree
→ Shows the issue context and lets you chat with the agent
```

### Chat to implement features

The workflow panel has a chat interface. You describe what you want:

> "Add a Redis cache to the UserService"

The agent reads your codebase, writes the code, and shows you what changed. You can review, ask for modifications, or continue with the next task.

### Index your codebase for context

Aura indexes your code two ways:

1. **Text search** - Finds relevant code snippets when you ask questions
2. **Code graph** - Understands types, methods, and relationships (C# via Roslyn, other languages via Tree-sitter)

When you ask the agent to implement something, it automatically searches your codebase for relevant context.

### Use local or cloud LLMs

Works with:

- **Ollama** - Run models locally (llama3, codellama, mistral, etc.)
- **OpenAI** - GPT-4, GPT-4o
- **Azure OpenAI** - Your own Azure deployment

Configure in `appsettings.json`:

```json
{
  "Llm": {
    "Provider": "ollama",
    "Model": "llama3.2"
  }
}
```

### Define custom agents

Agents are Markdown files. Create a new `.md` file in `agents/` and it's immediately available:

```markdown
# My Custom Agent

## System Prompt
You are an expert in...

## Capabilities
- code-review
- documentation

## Provider
openai/gpt-4o
```

No restart required - agents hot-reload.

### Work on multiple tasks in parallel

Each workflow runs in its own **git worktree** - a separate directory with its own branch, sharing the same `.git` history. This means:

- Start a feature, get blocked, start another feature - no stashing, no branch switching
- Each VS Code window is isolated - changes in one don't affect the other
- No risk of git corruption - worktrees are a native git feature
- When done, merge the branch and delete the worktree

```
main repo:     ~/projects/myapp/           (main branch)
workflow 1:    ~/projects/myapp-worktrees/add-caching/    (feature/add-caching)
workflow 2:    ~/projects/myapp-worktrees/fix-auth-bug/   (feature/fix-auth-bug)
```

You can have any number of workflows in progress, each with its own chat history and file changes, without conflicts.

---

## Developer Module Features

The Developer Module provides workflow automation for coding tasks:

| Feature | Description |
|---------|-------------|
| **Workflows** | Track a coding task from start to PR |
| **Git worktrees** | Each workflow gets an isolated branch and directory |
| **GitHub integration** | Create workflows from issues, sync status |
| **Step execution** | Break work into steps, execute with different agents |
| **Assisted mode** | Review each step before proceeding |
| **Autonomous mode** | Let the agent run multiple steps automatically |
| **Chat history** | Conversation persists across sessions |

### Supported Languages

Code indexing and agents work with:

| Language | Indexing | Coding Agent |
|----------|----------|--------------|
| C# | Roslyn (full semantic) | ✅ |
| TypeScript | Tree-sitter | ✅ |
| Python | Tree-sitter | ✅ |
| Rust | Tree-sitter | ✅ |
| Go | Tree-sitter | ✅ |
| F# | Tree-sitter | ✅ |
| PowerShell | Tree-sitter | ✅ |

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/) (for PostgreSQL)
- [Ollama](https://ollama.ai/) (for local LLM) or OpenAI API key

### Install

```powershell
# Clone the repository
git clone https://github.com/johnazariah/aura.git
cd aura

# Build everything
dotnet build

# Start the services (PostgreSQL + API)
dotnet run --project src/Aura.AppHost
```

### VS Code Extension

1. Open the `extension/` folder in VS Code
2. Press F5 to launch with the extension
3. Or build and install: `.\scripts\Build-Extension.ps1`

### First Run

1. Start Aura services: `.\scripts\Start-Api.ps1`
2. Open VS Code with the extension installed
3. Open a folder with code you want to index
4. Run command: "Aura: Index Workspace"
5. Run command: "Aura: Create Workflow" to start a coding task

---

## Configuration

### LLM Providers

Edit `appsettings.json` or set environment variables:

**Ollama (local, default):**

```json
{ "Llm": { "Provider": "ollama", "Model": "llama3.2", "BaseUrl": "http://localhost:11434" } }
```

**OpenAI:**

```json
{ "Llm": { "Provider": "openai", "Model": "gpt-4o", "ApiKey": "sk-..." } }
```

**Azure OpenAI:**

```json
{ "Llm": { "Provider": "azure", "Endpoint": "https://xxx.openai.azure.com", "DeploymentName": "gpt-4o", "ApiKey": "..." } }
```

### GitHub Integration

To create workflows from GitHub issues and create PRs:

```powershell
$env:GITHUB_TOKEN = "ghp_..."
```

---

## Project Structure

```
src/
├── Aura.Foundation/       # Core: LLM, RAG, agents, database
├── Aura.Module.Developer/ # Workflows, git, code analysis
├── Aura.Api/              # HTTP API
└── Aura.AppHost/          # .NET Aspire orchestration

extension/                 # VS Code extension
agents/                    # Agent definitions (Markdown)
prompts/                   # Prompt templates (Handlebars)
```

---

## Documentation

- [Installation Guide](docs/getting-started/installation.md)
- [User Guide](docs/user-guide/workflows.md)
- [LLM Configuration](docs/configuration/llm-providers.md)
- [Tool Prerequisites](docs/TOOL-PREREQUISITES.md) - External tools for language-specific agents

## License

MIT - see [LICENSE](LICENSE)
