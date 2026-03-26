# Aura Architecture Quick Reference

> **For Copilot/AI Assistants**: Read this first to understand project layout and key files.

## Project Layout

### Source Projects (6)

| Project | Purpose |
|---------|---------|
| `src/Aura.Api` | HTTP + MCP host (endpoints, MCP handlers) |
| `src/Aura.Foundation` | Core: embeddings, RAG, data, shell, git |
| `src/Aura.Module.Developer` | Roslyn + multi-language code intelligence |
| `src/Aura.Module.Researcher` | PDF and document ingestion |
| `src/Aura.ServiceDefaults` | Shared Aspire service configuration |
| `src/Aura.Tray` | Windows system tray app |

### Test Projects (5)

| Project | Mirrors |
|---------|---------|
| `tests/Aura.Api.Tests` | `Aura.Api` |
| `tests/Aura.Api.IntegrationTests` | `Aura.Api` (requires PostgreSQL) |
| `tests/Aura.Foundation.Tests` | `Aura.Foundation` |
| `tests/Aura.Module.Developer.Tests` | `Aura.Module.Developer` |
| `tests/Aura.Module.Researcher.Tests` | `Aura.Module.Researcher` |

## Key Files

| Path | Purpose |
|------|---------|
| `src/Aura.Api/Program.cs` | Startup — DI, middleware, endpoint registration |
| `src/Aura.Api/Endpoints/*.cs` | REST endpoints (`Map*Endpoints()` extension methods) |
| `src/Aura.Api/Mcp/McpHandler.cs` | MCP entry point (partial class) |
| `src/Aura.Api/Mcp/McpHandler.*.cs` | MCP domain partials (see below) |
| `src/Aura.Foundation/ServiceCollectionExtensions.cs` | Foundation DI registration |
| `src/Aura.Foundation/Rag/BackgroundIndexer.cs` | Async indexing via `Channel<IndexWorkItem>` |
| `src/Aura.Foundation/Rag/IncrementalIndexer.cs` | File-watcher-based incremental indexing |
| `src/Aura.Foundation/Rag/RagService.cs` | RAG indexing and querying |
| `src/Aura.Foundation/Data/AuraDbContext.cs` | EF Core context (Npgsql + pgvector) |
| `src/Aura.Api/appsettings.json` | Configuration (embedding providers, RAG, ports) |

### MCP Handler Partials

```
McpHandler.cs             — base partial (routing, tool registry)
McpHandler.Generate.cs    — code generation tools
McpHandler.Index.cs       — indexing tools
McpHandler.Inspect.cs     — type/member inspection tools
McpHandler.Languages.cs   — language service tools
McpHandler.Navigate.cs    — callers/implementations/references
McpHandler.Refactor.cs    — refactoring tools
McpHandler.Search.cs      — semantic search tools
McpHandler.Tree.cs        — hierarchical exploration
McpHandler.Validate.cs    — compilation and test validation
McpHandler.Workspaces.cs  — workspace management tools
```

### Endpoint Files

```
src/Aura.Api/Endpoints/
├── HealthEndpoints.cs              → MapHealthEndpoints()
├── McpEndpoints.cs                 → MapMcpEndpoints()
├── WorkspaceEndpoints.cs           → MapWorkspaceEndpoints()
├── WorkspaceIndexEndpoints.cs      → MapWorkspaceIndexEndpoints()
├── WorkspaceGraphEndpoints.cs      → MapWorkspaceGraphEndpoints()
└── WorkspaceSearchEndpoints.cs     → MapWorkspaceSearchEndpoints()
```

## DI Registration Chain

```
Program.cs
├── builder.AddServiceDefaults()                    ← Aspire telemetry, health, resilience
├── builder.Services.AddDbContext<AuraDbContext>()   ← Npgsql + pgvector
├── builder.Services.AddAuraFoundation(config)      ← Foundation chain:
│   ├── AddShellServices()
│   │   └── IProcessRunner → ProcessRunner
│   ├── AddGitServices()
│   │   ├── IGitService → GitService
│   │   └── IGitWorktreeService → GitWorktreeService
│   └── AddRagServices(config)
│       ├── Configure<RagOptions>               (Aura:Rag)
│       ├── Configure<RagWatcherOptions>        (Aura:RagWatcher)
│       ├── Configure<BackgroundIndexerOptions> (Aura:BackgroundIndexer)
│       ├── Configure<EmbeddingOptions>         (Aura:Embedding)
│       ├── Configure<OllamaOptions>            (Aura:Ollama)
│       ├── Configure<OpenAiEmbeddingOptions>   (Aura:OpenAiEmbedding)
│       ├── IEmbeddingProvider (see provider selection below)
│       ├── TextChunker, IIngestorRegistry
│       ├── IRagService → RagService
│       ├── ICodeGraphService → CodeGraphService
│       ├── ICodeGraphEnricher → CodeGraphEnricher
│       ├── BackgroundIndexer (singleton + hosted service)
│       ├── IncrementalIndexer (singleton)
│       └── IWorkspaceRegistryService → WorkspaceRegistryService
├── Developer services (registered individually):
│   ├── IRoslynWorkspaceService → RoslynWorkspaceService
│   ├── IRoslynRefactoringService → RoslynRefactoringService
│   ├── IPythonRefactoringService → PythonRefactoringService
│   ├── ITypeScriptLanguageService → TypeScriptLanguageService
│   ├── ITestGenerationService → RoslynTestGenerator
│   ├── ICodeGraphIndexer → CodeGraphIndexer
│   ├── ITreeBuilderService → TreeBuilderService
│   ├── RoslynCodeIngestor, TreeSitterCodeIngestor
│   └── ResearcherModule.ConfigureServices()
└── AddScoped<McpHandler>()
```

## Ingestor Priority Order

Ingestors registered via `IIngestorRegistry.Register()` — first match wins.

| Priority | ID | Class | Extensions |
|----------|----|-------|------------|
| 1 (highest) | `roslyn-code` | `RoslynCodeIngestor` | `.cs`, `.csx` |
| 2 | `tree-sitter-code` | `TreeSitterCodeIngestor` | `.py` `.ts` `.tsx` `.js` `.jsx` `.go` `.rs` `.java` `.cpp` `.c` `.h` `.rb` `.swift` `.kt` |
| 3 | `pdf` | `PdfIngestor` | `.pdf` |
| 4 | `structured-data` | `StructuredDataIngestor` | `.json` `.yaml` `.yml` `.xml` `.toml` `.env` `.properties` |
| 5 | `code` | `CodeIngestor` | `.cs` `.ts` `.tsx` `.js` `.jsx` `.py` `.rs` `.go` `.java` `.cpp` `.c` `.h` `.hpp` `.fs` `.fsx` |
| 6 | `markdown` | `MarkdownIngestor` | `.md` `.markdown` `.mdx` |
| 7 (lowest) | `plaintext` | `PlainTextIngestor` | `.txt` `.text` `.log` `.cfg` `.ini` `.conf` + extensionless |

Priorities 1–3 are registered at startup in `Program.cs` and inserted at index 0 (shadowing lower-priority defaults for overlapping extensions). Priority 4–7 are defaults from `IngestorRegistry`.

## Embedding Provider Selection

Configured via `Aura:Embedding` section → `EmbeddingOptions`.

| `Provider` value | Resolved to | Behavior |
|------------------|-------------|----------|
| `"openai"` | `OpenAiEmbeddingProvider` | OpenAI-compatible API only |
| `"ollama"` | `OllamaProvider` | Local Ollama only |
| `"auto"` (default) | `FallbackEmbeddingProvider` | Try OpenAI first, fall back to Ollama |

Related config sections:
- `Aura:Embedding` — `Provider`, `BatchSize` (default 100), `FallbackModel` (default `nomic-embed-text`)
- `Aura:OpenAiEmbedding` — `BaseUrl`, `ApiKey`, `TimeoutSeconds` (default 60)
- `Aura:Ollama` — `BaseUrl` (default `http://localhost:11434`)
