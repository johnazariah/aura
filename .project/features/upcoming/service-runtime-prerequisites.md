# Service Runtime Prerequisites & Diagnostics

**Status:** 📋 Proposed
**Priority:** High
**Created:** 2026-01-25

## Problem Statement

Aura runs as a Windows service under LocalSystem account, which lacks access to user-specific package caches (NuGet, npm, Go modules, etc.). When quality gates run `dotnet restore` or similar commands in worktrees, they fail with cryptic "Unable to resolve package" errors.

Users have no visibility into:
1. Why a quality gate failed
2. What prerequisites are missing
3. How to fix the problem

This is a **distribution/operations problem** that affects all users, not just developers.

## Goals

1. **Zero-config for common scenarios** - .NET, Node, Go, Python, Rust should work out of the box
2. **Clear diagnostics** - Users can understand what went wrong and how to fix it
3. **Health visibility** - API endpoint to check prerequisite status
4. **Graceful degradation** - Missing language tooling doesn't break Aura, just limits capabilities

## Non-Goals

- Supporting every possible language/toolchain
- Pre-installing language runtimes (user responsibility)
- Cross-platform parity in first iteration (Windows first)

## Design

### 1. Shared Cache Infrastructure

Create shared cache directories accessible to LocalSystem:

```
C:\ProgramData\Aura\
├── caches\
│   ├── nuget\           # NUGET_PACKAGES
│   ├── go\              # GOMODCACHE
│   ├── npm\             # npm_config_cache
│   ├── pip\             # PIP_CACHE_DIR
│   └── cargo\           # CARGO_HOME/registry
├── logs\
└── config\
```

### 2. Environment Variables for Service

Set these in the service context (during install):

| Variable | Value |
|----------|-------|
| `NUGET_PACKAGES` | `C:\ProgramData\Aura\caches\nuget` |
| `GOMODCACHE` | `C:\ProgramData\Aura\caches\go` |
| `npm_config_cache` | `C:\ProgramData\Aura\caches\npm` |
| `PIP_CACHE_DIR` | `C:\ProgramData\Aura\caches\pip` |
| `CARGO_HOME` | `C:\ProgramData\Aura\caches\cargo` |

### 3. Quality Gate Service Updates

Update `QualityGateService` to:
- Set environment variables when spawning processes
- Return structured error information
- Log which cache paths are being used

```csharp
public record QualityGateResult
{
    // Existing fields...
    
    // New diagnostic fields
    public string? DiagnosticCategory { get; init; }  // "restore", "build", "test"
    public string? DiagnosticSuggestion { get; init; } // Human-readable fix
    public IReadOnlyList<string>? MissingPackages { get; init; }
}
```

### 4. Prerequisites Health Check

New endpoint: `GET /health/prerequisites`

```json
{
  "status": "degraded",
  "languages": {
    "dotnet": {
      "available": true,
      "version": "10.0.100",
      "cacheConfigured": true,
      "cachePath": "C:\\ProgramData\\Aura\\caches\\nuget",
      "cacheWritable": true
    },
    "go": {
      "available": false,
      "suggestion": "Install Go from https://go.dev/dl/"
    },
    "node": {
      "available": true,
      "version": "22.0.0",
      "cacheConfigured": true,
      "cachePath": "C:\\ProgramData\\Aura\\caches\\npm",
      "cacheWritable": true
    },
    "python": {
      "available": true,
      "version": "3.13.0",
      "cacheConfigured": false,
      "suggestion": "Python cache not configured; pip install may be slow"
    },
    "rust": {
      "available": false,
      "suggestion": "Install Rust from https://rustup.rs/"
    }
  },
  "services": {
    "git": { "available": true, "version": "2.47.0" },
    "copilotCli": { "available": true }
  }
}
```

### 5. Installation Script Updates

Update `install-windows.ps1` to:

```powershell
# Create cache directories
$cacheDirs = @(
    "C:\ProgramData\Aura\caches\nuget",
    "C:\ProgramData\Aura\caches\go",
    "C:\ProgramData\Aura\caches\npm",
    "C:\ProgramData\Aura\caches\pip",
    "C:\ProgramData\Aura\caches\cargo"
)
foreach ($dir in $cacheDirs) {
    New-Item -ItemType Directory -Path $dir -Force
    # Grant LocalSystem and Administrators full control
    icacls $dir /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)F"
}

# Set service environment variables
# (stored in registry under service key)
```

### 6. Startup Validation

On service startup:
1. Check each language toolchain availability
2. Verify cache directories exist and are writable
3. Log warnings for missing prerequisites
4. Populate health check cache

### 7. Story Diagnostics Field

Add to Story entity:

```csharp
/// <summary>Gets or sets diagnostic information from failed gates.</summary>
public string? GateDiagnostics { get; set; }
```

Expose via API so users can see why their story failed.

## Implementation Plan

### Phase 1: Core Infrastructure (Critical)
- [ ] Create cache directories in installer
- [ ] Set environment variables for service
- [ ] Update QualityGateService to use shared caches
- [ ] Add structured error fields to QualityGateResult

### Phase 2: Health & Diagnostics
- [ ] Implement `/health/prerequisites` endpoint
- [ ] Add startup validation with logging
- [ ] Add `GateDiagnostics` to Story entity
- [ ] Expose diagnostics in story API response

### Phase 3: User Experience
- [ ] Extension shows prerequisites status in status bar
- [ ] Story failure shows diagnostic suggestion
- [ ] Documentation for troubleshooting

## Testing

1. **Fresh install test** - Install on clean Windows, verify caches created
2. **Service context test** - Run restore as LocalSystem, verify it works
3. **Missing toolchain test** - Remove Go from PATH, verify graceful degradation
4. **Permission test** - Verify cache directories have correct ACLs

## Files to Modify

| File | Change |
|------|--------|
| `setup/install-windows.ps1` | Create cache dirs, set env vars |
| `src/Aura.Module.Developer/Services/QualityGateService.cs` | Use env vars, structured errors |
| `src/Aura.Api/Program.cs` | Add `/health/prerequisites` endpoint |
| `src/Aura.Api/Startup/PrerequisiteChecker.cs` | New - startup validation |
| `src/Aura.Module.Developer/Data/Entities/Story.cs` | Add GateDiagnostics field |
| `docs/troubleshooting/` | New troubleshooting guide |

## Open Questions

1. Should we pre-populate caches with common packages during install?
2. Should we support user-configurable cache locations?
3. How do we handle cache growth over time? (cleanup policy)
4. Should the health check be part of `/health` or separate?

## References

- [NuGet Environment Variables](https://learn.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-environment-variables)
- [Go Module Cache](https://go.dev/ref/mod#module-cache)
- [npm cache configuration](https://docs.npmjs.com/cli/v10/commands/npm-cache)
