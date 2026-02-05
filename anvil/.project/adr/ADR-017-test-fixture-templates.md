# ADR-017: Test Fixture Template Extraction

**Status:** Proposed  
**Date:** 2026-01-31

## Context

Anvil needs test repositories to run scenarios against. These repositories must:
- Be in a known-good initial state
- Be accessible by both Anvil CLI (user context) and Aura Service (LocalSystem)
- Support parallel test execution
- Allow post-mortem inspection of generated code
- Be distributable with Anvil

Initial approaches considered:
1. **Embedded git repos** - Store `.git` folders in fixtures
2. **Git submodules** - Reference external test repos
3. **Template extraction** - Store plain files, git init at runtime

## Decision

We will use **template extraction with timestamped run folders**.

### Storage (Committed to Anvil)

```
anvil/fixtures/templates/           # Plain folders, no .git
├── csharp-console/
│   └── HelloWorld/
│       ├── HelloWorld.csproj
│       └── Program.cs
├── csharp-webapi/
└── python-flask/
```

### Runtime (Created per-run)

```
anvil/runs/20260131-145500-hello-world/
└── csharp-console/
    └── HelloWorld/...
```

### Initialization Sequence

1. Copy template to timestamped run folder
2. `git init` + configure git user
3. `git add -A && git commit -m "Initial state"`
4. Add `safe.directory` for AuraService access
5. Pass run folder path to Aura
6. Execute scenario
7. Keep folder for inspection (configurable cleanup)

## Rationale

### Why not embedded git repos?

- Git warns about embedded repositories
- Clones don't include embedded repo contents properly
- Ownership conflicts between user/service contexts

### Why not submodules?

- Adds external dependency
- Complicates CI/CD
- Overkill for simple test fixtures
- Still has ownership issues

### Why template extraction?

- **Simplicity**: Templates are just plain files
- **Reproducibility**: Fresh known-good state every run
- **Debuggability**: Timestamped folders allow inspection
- **Parallelism**: Each run gets isolated folder
- **Distribution**: Can tar templates for packaging

## Consequences

### Positive

- No git-in-git issues
- Clean source control
- Easy to update templates
- Run isolation enables parallel testing
- Post-mortem inspection of agent output

### Negative

- Slight startup overhead (copy + git init)
- Disk usage from retained run folders
- Need cleanup mechanism

### Mitigations

- `--cleanup` flag for CI (delete after run)
- `anvil clean` command to prune old runs
- Configure max retained runs

## Security Considerations

| Component | Context | Access |
|-----------|---------|--------|
| Anvil CLI | Current user | Creates and initializes run folder |
| Aura Service | LocalSystem/AuraService | Reads template, creates worktrees |

The `safe.directory` configuration is required because:
- Anvil runs as the logged-in user
- Aura Service runs as LocalSystem or a service account
- Git refuses to operate on repos owned by different users without explicit safe.directory

## Implementation Notes

- Run folders should be in a configurable location (default: `anvil/runs/`)
- Add `runs/` to `.gitignore`
- Timestamp format: `YYYYMMDD-HHMMSS-{scenario-name}`
- Consider tarball packaging for binary distribution
