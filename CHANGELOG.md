# Changelog

All notable changes to Aura will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

