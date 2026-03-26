# Indexing

Aura indexes your codebase into two structures: a **RAG vector store** (pgvector) for semantic search and a **code graph** (nodes + edges) for structural navigation. Seven ingestors handle different file types.

## Ingestors

| Ingestor | ID | File Extensions | Description |
|---|---|---|---|
| **Roslyn** | `roslyn-code` | `.cs`, `.csx` | Full Roslyn semantic analysis. Produces RAG chunks and code graph nodes/edges with type relationships, call graphs, and member details. |
| **TreeSitter** | `treesitter-code` | `.py`, `.ts`, `.tsx`, `.js`, `.jsx`, `.go`, `.rs`, `.java`, `.cpp`, `.c`, `.h`, `.rb`, `.swift`, `.kt` | AST-aware parsing via TreeSitter grammars. Produces RAG chunks and code graph. Skips `.cs`/`.csx` (handled by Roslyn). |
| **Markdown** | `markdown` | `.md`, `.markdown`, `.mdx` | Splits by headings; preserves code blocks as separate chunks. |
| **StructuredData** | `structured-data` | `.json`, `.yaml`, `.yml`, `.xml`, `.toml`, `.env`, `.properties` | Structure-aware chunking by top-level keys. |
| **PDF** | `pdf` | `.pdf` | Uses `pdftotext` (poppler-utils). Paragraph-based chunking with metadata extraction. |
| **Code** (regex) | `code` | `.cs`, `.ts`, `.js`, `.py`, `.rs`, `.go`, `.java`, `.cpp`, `.c`, `.h`, `.hpp`, `.fs`, `.fsx` | Regex-based fallback. Overridden by Roslyn or TreeSitter when those modules are loaded. |
| **PlainText** | `plaintext` | `.txt`, `.text`, `.log`, `.cfg`, `.ini`, `.conf` + extensionless | Line-based chunking (1500 chars, 200 overlap). Catch-all fallback. |

### Priority

When multiple ingestors match the same extension, the last registered one wins. The registration order (from lowest to highest priority) is:

1. Markdown → 2. Code (regex) → 3. StructuredData → 4. PlainText → 5. PDF → 6. Roslyn → 7. TreeSitter

In practice: `.cs` files are handled by Roslyn, `.py`/`.ts`/`.js`/`.go` etc. by TreeSitter, `.pdf` by PDF, and so on.

## Triggering Indexing

### Via MCP

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "index_directory",
    "path": "C:/projects/my-app",
    "recursive": true
  }
}
```

### Via REST API

```bash
# Register workspace with immediate indexing
curl -X POST http://localhost:5300/api/workspaces \
  -H "Content-Type: application/json" \
  -d '{"path": "C:/projects/my-app", "startIndexing": true}'

# Re-index an existing workspace
curl -X POST http://localhost:5300/api/workspaces/{workspaceId}/index
```

### Filtering by File Pattern

Index only specific files:

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "index_directory",
    "path": "C:/projects/research",
    "filePattern": "*.pdf"
  }
}
```

## Index Status

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "stats",
    "path": "C:/projects/my-app"
  }
}
```

Or via REST:

```bash
curl http://localhost:5300/api/workspaces/{workspaceId}/index
```

Returns freshness (`fresh`, `stale`, `not-indexed`), chunk count, graph node count, and last-indexed commit.

## Default Include / Exclude Patterns

**Included by default:**

`*.cs`, `*.md`, `*.txt`, `*.json`, `*.yaml`, `*.yml`, `*.ts`, `*.tsx`, `*.js`, `*.jsx`, `*.py`, `*.rs`, `*.csproj`, `*.sln`, `*.props`, `*.targets`, `*.fsproj`, `*.pdf`, `*.go`, `*.java`, `*.cpp`, `*.c`, `*.h`, `*.rb`, `*.swift`, `*.kt`, `*.xml`, `*.toml`

**Excluded by default:**

`**/bin/**`, `**/obj/**`, `**/node_modules/**`, `**/.git/**`, `**/.vs/**`, `**/packages/**`, `**/dist/**`, `**/.nuget/**`, `**/*.dll`, `**/*.exe`, `**/*.pdb`, `**/*.cache`, `**/wwwroot/lib/**`, `**/.idea/**`, `**/coverage/**`, `**/.venv/**`, `**/venv/**`, `**/cache/**`, `**/publish/**`, `**/temp/**`, `**/TestResults/**`, `**/__pycache__/**`

Override these in configuration — see [Settings Reference](../configuration/settings.md).

## Clearing the Index

```bash
# Clear RAG chunks
curl -X DELETE http://localhost:5300/api/workspaces/{workspaceId}/index

# Clear code graph
curl -X DELETE http://localhost:5300/api/workspaces/{workspaceId}/graph
```

## Git Worktree Support

Aura detects git worktrees and maps worktree paths back to the main repository workspace. Use `aura_workspace` with `detect_worktree` to check whether a path is inside a worktree:

```json
{
  "name": "aura_workspace",
  "arguments": {
    "operation": "detect_worktree",
    "path": "C:/projects/my-app-feature-branch"
  }
}
```
