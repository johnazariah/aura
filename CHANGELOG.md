# Changelog

All notable changes to Aura will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## v2.0.0 — Personal Knowledge MCP Server (2026-03-03)

### Breaking Changes
- Removed agent orchestration, VS Code extension, prompt templates, story lifecycle
- Removed Aspire orchestration host
- MCP tools reduced from 13 to 10 (removed aura_workflow, aura_pattern, aura_edit, aura_docs)

### Added
- TreeSitterCodeIngestor for Python, TypeScript, JavaScript, Go, Rust, Java, C/C++
- StructuredDataIngestor for JSON, YAML, XML, TOML
- PdfIngestor for PDF document indexing
- `aura_index` MCP tool for on-demand indexing
- OpenAI embedding provider with configurable batching
- Auto-failover embedding (OpenAI → Ollama)
- Cross-platform system tray app (Avalonia)
- macOS build support in release pipeline
- Deploy-Dev.ps1 for quick development deployment
- Global Copilot MCP config at ~/.copilot/mcp-config.json

### Changed
- RoslynCodeIngestor now registered as primary C# handler (was regex fallback)
- IncrementalIndexer routes through ingestor pipeline (was plain text only)
- Default embedding provider changed to "auto" (try OpenAI, fall back to Ollama)
- Embedding dimensions normalized to 768 for provider compatibility
- CI pipeline simplified (removed coverage/integration test jobs)

### Removed
- ~60,000 lines of agent/orchestration/extension code
- agents/, prompts/, extension/ directories
- 128 agent tools, prompt templates, guardian system
- Azure OpenAI and OpenAI chat providers (kept embedding only)

See [ADR-025](.project/adr/025-personal-knowledge-mcp-pivot.md) for rationale.

## [1.5.0] - 2026-01-30

### Added

- **TypeScript refactoring support** - `aura_navigate` and `aura_refactor` now support `.ts/.tsx/.js/.jsx` files via ts-morph
- **Strongly-typed constants** - New `LlmProviders`, `ResourceNames`, `BuiltInToolIds`, `ChunkMetadataKeys` classes eliminate magic strings
- **JSON schema DTOs** - `ReActResponseDto`, `WorkflowPlanDto`, `CodeModificationDto` with `JsonSchemaGenerator` for strict LLM output validation
- **Multi-workspace search** - Workspace registry enables cross-workspace queries via `aura_search` with `workspaces: ["*"]`
- **Workspace-scoped API** - Unified `/api/workspaces/{id}/index`, `/graph`, `/search` endpoints

### Changed

- **MCP tool consolidation** - Reduced from 17 to 13 tools for better discoverability:
  - `aura_workspaces` merged into `aura_workspace` (list, add, remove, set_default operations)
  - `aura_get_node` merged into `aura_tree` (get_node operation)
  - `aura_docs_list` and `aura_docs_get` merged into `aura_docs` (list, get operations)
- **Database schema** - Squashed migrations to single `InitialCreate`, standardized snake_case column names
- **Story naming** - Unified `_workflowService` → `_storyService` throughout codebase
- **Large file splits** - `McpHandler`, `RoslynRefactoringService`, `StoryService` split into partial files for maintainability

### Fixed

- Various stability improvements and test coverage enhancements

## [1.4.0] - 2026-01-23

### Added

- Initial public release with core MCP tools
- RAG indexing with pgvector embeddings
- Code graph with Roslyn analysis
- VS Code extension with Stories workflow
- Windows installer with bundled PostgreSQL

