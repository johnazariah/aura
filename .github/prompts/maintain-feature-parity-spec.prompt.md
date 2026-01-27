---
mode: agent
description: Update the feature-parity specification after codebase changes
tools:
  - read_file
  - list_dir
  - grep_search
  - semantic_search
  - replace_string_in_file
  - create_file
---

# Maintain Feature-Parity Specification

Update the feature-parity specification documents in `.project/feature-parity-spec/` to reflect recent codebase changes.

## Context

The feature-parity specification is a comprehensive set of documents that describe every feature, API, and behavior of the Aura codebase. It exists so that an agent could rewrite the application to feature-parity with only these documents.

## Specification Documents

| Document | Covers |
|----------|--------|
| `00-overview.md` | Architecture, principles, technology choices |
| `01-foundation.md` | Agents, LLM, RAG, Code Graph, Tools, Git, Prompts |
| `02-developer-module.md` | Stories, Steps, Roslyn, refactoring, test generation |
| `03-api-layer.md` | REST endpoints, MCP tools, SSE streaming |
| `04-extension.md` | VS Code views, commands, services, providers |
| `05-data-model.md` | PostgreSQL schema, entities, migrations |
| `06-agents-prompts.md` | Agent markdown, language configs, templates, patterns |
| `07-configuration.md` | All configuration options |
| `08-code-review.md` | Technical debt, simplification opportunities |

## Task

1. **Identify what changed**: Review the recent changes in the codebase (git diff, changed files, or user description)

2. **Determine affected specs**: Map changes to specification documents:
   - New/modified API endpoint → `03-api-layer.md`
   - New MCP tool → `03-api-layer.md` MCP section
   - Schema change → `05-data-model.md`
   - New agent → `06-agents-prompts.md`
   - New configuration → `07-configuration.md`
   - Architecture change → `00-overview.md`
   - Foundation service → `01-foundation.md`
   - Developer module → `02-developer-module.md`
   - Extension change → `04-extension.md`

3. **Read current spec**: Read the relevant section of the affected spec document

4. **Update accurately**: Make surgical updates that:
   - Add new features/endpoints/options
   - Update changed behaviors
   - Remove deleted features
   - Maintain consistent formatting

5. **Update metrics if changed**: If the overall counts changed, update `README.md`:
   ```markdown
   ## Metrics
   - ~50 REST API endpoints
   - 11 MCP tools
   - 10+ agent definitions
   ```

## Formatting Guidelines

- Use tables for APIs, configs, and enumerated items
- Use code blocks for schemas, JSON structures, templates
- Use headers consistently: `##` for sections, `###` for subsections
- Include example values where helpful
- Document defaults and required vs optional

## Quality Checks

After updating:
- [ ] Information is accurate to current implementation
- [ ] No outdated references remain
- [ ] Examples work with current codebase
- [ ] Formatting is consistent with rest of document
- [ ] `README.md` metrics are updated if counts changed

## Example Update

If a new MCP tool `aura_analyze` was added:

1. Open `03-api-layer.md`
2. Find the "### Available Tools" section
3. Add new row to the tools table:
   ```markdown
   | `aura_analyze` | Analyze code complexity | `filePath`, `metrics` |
   ```
4. Add detailed documentation in appropriate subsection
5. Update tool count in `README.md` if it changed
