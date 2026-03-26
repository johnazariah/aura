# Aura Documentation

Aura is a personal knowledge MCP server. It indexes codebases and documents locally — using Roslyn, TreeSitter, and pgvector RAG — and exposes tools to GitHub Copilot via the Model Context Protocol.

## Getting Started

1. [Installation](getting-started/installation.md) — Windows installer or macOS manual setup
2. [First Run](getting-started/first-run.md) — Verify health endpoints, check Ollama models, index a folder
3. [Quick Start](getting-started/quick-start.md) — Register a workspace, trigger indexing, search via Copilot

## User Guide

- [MCP Tools](user-guide/mcp-tools.md) — All 10 tools with operations and examples
- [Indexing](user-guide/indexing.md) — 7 ingestors, supported file types, how indexing works
- [Use Cases](user-guide/use-cases.md) — Code search, PDF research, config search, multi-language projects
- [Cheat Sheet](user-guide/cheat-sheet.md) — Quick reference for REST endpoints and MCP tool operations

## Configuration

- [Settings Reference](configuration/settings.md) — All config sections with defaults
- [LLM Providers](configuration/llm-providers.md) — Ollama, OpenAI, auto-failover

## MCP Tools / API Reference

- [API Reference](mcp-tools/api-reference.md) — Detailed MCP tool schemas for all 10 tools

## Troubleshooting

- [Common Issues](troubleshooting/common-issues.md) — Ollama, PostgreSQL, embedding, service problems
- [Logs & Diagnostics](troubleshooting/logs.md) — Log locations and interpretation
- [Getting Help](troubleshooting/support.md) — Bug reports, feature requests, community

## Benchmarks

- [README Review Benchmark](benchmarks/brightsword-readme-review-benchmark.md) — LLM output quality scoring
