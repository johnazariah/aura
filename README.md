# Aura

[![CI](https://github.com/johnazariah/aura/actions/workflows/ci.yml/badge.svg)](https://github.com/johnazariah/aura/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/johnazariah/aura/graph/badge.svg)](https://codecov.io/gh/johnazariah/aura)
[![License: MIT](https://img.shields.io/badge/License/MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Aura is a local-first indexing and MCP server for code, documents, PDFs, and structured files. It keeps the index on your machine, exposes semantic and structural tools to GitHub Copilot via MCP, and can use either local Ollama embeddings or hosted OpenAI embeddings.

## What Aura Is Now

- Local semantic indexing for mixed-content workspaces
- Structural code intelligence for C# via Roslyn and polyglot parsing via Tree-sitter
- MCP tools for search, navigation, inspection, refactoring, validation, indexing, and workspace management
- Windows service deployment with a cross-platform tray app
- PostgreSQL + pgvector for local storage

Aura is no longer a VS Code extension product, internal agent runtime, or story/workflow orchestrator. Copilot is the UI; Aura is the local intelligence layer behind it.

## Core Capabilities

### Index local content

Aura can index:

- C# with Roslyn-backed chunks and code graph data
- TypeScript, JavaScript, Python, Go, Rust, Java, C/C++, and more via Tree-sitter
- PDF documents via `pdftotext`
- Markdown
- JSON, YAML, XML, TOML, and `.env`-style structured files
- Plain text fallback for everything else

### Serve GitHub Copilot via MCP

Aura exposes these MCP tools:

- `aura_search`
- `aura_navigate`
- `aura_inspect`
- `aura_refactor`
- `aura_generate`
- `aura_validate`
- `aura_index`
- `aura_workspace`
- `aura_tree`
- `aura_architect`

### Use local or hosted embeddings

Aura supports:

- Ollama for local embeddings
- OpenAI for hosted embeddings
- Auto mode with OpenAI-first configuration and local fallback when dimensions are compatible

## Architecture

```text
Copilot Chat / Copilot CLI
  -> MCP
     -> Aura.Api
        -> Ingestor pipeline
           -> RoslynCodeIngestor
           -> TreeSitterCodeIngestor
           -> StructuredDataIngestor
           -> PdfIngestor
           -> MarkdownIngestor
           -> CodeIngestor
           -> PlainTextIngestor
        -> Embedding providers
           -> OllamaProvider
           -> OpenAiEmbeddingProvider
        -> PostgreSQL + pgvector
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL with `pgvector`
- [Ollama](https://ollama.ai/) or an OpenAI-compatible embedding endpoint

### Build and test

```powershell
dotnet build
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

### Run locally

```powershell
dotnet run --project src/Aura.Api
```

### Connect Copilot to Aura

Repo-local MCP configuration:

```json
{
  "mcp": {
    "servers": {
      "aura": {
        "url": "http://localhost:5300/mcp"
      }
    }
  }
}
```

Machine-wide configuration can also be written to `~/.copilot/mcp-config.json`.

## Repository Layout

```text
src/
├── Aura.Foundation/
├── Aura.Module.Developer/
├── Aura.Module.Researcher/
├── Aura.Api/
├── Aura.Tray/
└── Aura.ServiceDefaults/

tests/
├── Aura.Foundation.Tests/
├── Aura.Module.Developer.Tests/
├── Aura.Module.Researcher.Tests/
└── Aura.Api.Tests/
```

See `.project/STATUS.md` for the current project state and `.project/adr/025-personal-knowledge-mcp-pivot.md` for the architectural pivot.

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

## Key Features

### Stories from GitHub Issues

Start work directly from a GitHub issue:

```
Command: "Aura: Start Story from Issue"
→ Paste: https://github.com/org/repo/issues/42
→ Aura creates a git worktree and branch
→ Opens VS Code in the isolated worktree
```

### Operational Patterns

Step-by-step playbooks for complex operations:

- **comprehensive-rename** - Rename domain concepts across the codebase
- **generate-tests** - Comprehensive test generation with language-specific guidance

### Workflow Verification

Before completing a workflow, Aura runs verification checks:

- Build verification (dotnet build, npm build, cargo build, etc.)
- Test verification (runs your test suite)
- Reports pass/fail before finalizing

### Agent Reflection

Agents can self-critique their responses for higher quality output:

```markdown
## Metadata
- **Reflection**: true
```

---

## Project Structure

```
src/
├── Aura.Foundation/       # Core: embeddings, RAG, data, shell, git
├── Aura.Module.Developer/ # Roslyn and multi-language code intelligence
├── Aura.Module.Researcher/# PDF and document ingestion
├── Aura.Api/              # HTTP API + MCP host
├── Aura.ServiceDefaults/  # Shared service configuration
└── Aura.Tray/             # Cross-platform tray app

patterns/                  # Operational patterns for complex tasks
```

---

## Documentation

**Getting Started:**
- [Installation Guide](docs/getting-started/installation.md)
- [First Run](docs/getting-started/first-run.md)
- [Quick Start](docs/getting-started/quick-start.md)

**User Guide:**
- [Operational Patterns](docs/user-guide/patterns.md)
- [MCP Tools](docs/user-guide/mcp-tools.md)
- [Code Indexing](docs/user-guide/indexing.md)
- [Use Cases](docs/user-guide/use-cases.md)
- [Cheat Sheet](docs/user-guide/cheat-sheet.md)

**Configuration:**
- [LLM Providers](docs/configuration/llm-providers.md)
- [Settings Reference](docs/configuration/settings.md)

## License

MIT - see [LICENSE](LICENSE)
