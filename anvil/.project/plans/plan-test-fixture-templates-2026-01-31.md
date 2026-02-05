# Test Fixture Template Extraction

**Status:** Plan  
**Created:** 2026-01-31  
**ADR:** [ADR-017-test-fixture-templates](../../.project/adr/ADR-017-test-fixture-templates.md)

## Summary

Replace embedded git repos with template extraction at runtime. See ADR-017 for full design rationale.

## Research Complete ✅

- Embedded git repos cause ownership and clone issues
- Submodules add unnecessary complexity
- Template extraction is simpler and supports parallel runs

## Implementation Steps

1. [ ] Remove `.git` from `fixtures/repos/csharp-console`
2. [ ] Rename `fixtures/repos/` → `fixtures/templates/`
3. [ ] Add `runs/` to `.gitignore`
4. [ ] Add `TemplateExtractor` service to Anvil
5. [ ] Update `StoryRunner` to:
   - Extract template to timestamped folder
   - Git init and commit
   - Add safe.directory for AuraService
   - Pass new path to Aura
6. [ ] Add `--cleanup` flag to `run` command
7. [ ] Add `anvil clean` command
8. [ ] Update scenario YAML to reference template names (not paths)
9. [ ] (Optional) Add tar packaging for distribution
