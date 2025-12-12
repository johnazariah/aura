# Code Graph Status in System Status Panel

**Status:** 🔲 Not Started

## Summary

Add Code Graph statistics to the System Status panel in the VS Code extension, alongside the existing RAG Index status.

## Motivation

Currently the System Status panel shows:
- Aura API
- Ollama (with models)
- Database
- RAG Index

Code Graph (Roslyn-based semantic index) is missing, making it hard for users to know if their codebase has been indexed for structural queries.

## Requirements

1. Add "Code Graph" item to System Status panel
2. Show total nodes/edges count for the current repository
3. Filter stats by `repositoryPath` (current workspace)
4. Show expandable details similar to RAG Index

## Technical Notes

- API endpoint exists: `GET /api/graph/stats?repositoryPath=...`
- Service method exists: `auraApiService.getCodeGraphStats(repositoryPath)`
- Need to add to `healthCheckService.ts` and `statusTreeProvider.ts`

## Related Files

- `extension/src/providers/statusTreeProvider.ts`
- `extension/src/services/healthCheckService.ts`
- `extension/src/services/auraApiService.ts`
