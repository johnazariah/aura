# Unified Database Configuration

**Status:** ✅ Complete  
**Completed:** 2026-01-13  
**Last Updated:** 2026-01-13

## Overview

Standardized PostgreSQL configuration across development (Aspire) and production (standalone container) modes to prevent database migration failures when switching between environments.

## Problem Solved

Previously, switching between `Start-Api` (dev) and `Update-LocalInstall` (production) caused migration failures because they used different configurations:

| Mode | Port | Database | User |
|------|------|----------|------|
| Dev (Aspire) | Dynamic | `auradb` | `postgres` |
| Production | 5432 | `aura` | `aura` |

Same container name + different configs = migration state corruption.

## Solution

All environments now use identical configuration:

| Setting | Value |
|---------|-------|
| Container | `aura-postgres` |
| Port | `5432` (fixed) |
| Database | `auradb` |
| User | `postgres` |
| Image | `pgvector/pgvector:pg17` |

## Implementation

### AppHost.cs
Aspire now pins PostgreSQL to port 5432:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithContainerName("aura-postgres")
    .WithDataVolume("aura-postgres-data")
    .WithHostPort(5432)  // Fixed port
    .WithPgAdmin();

var auraDb = postgres.AddDatabase("auradb");
```

### install-windows.ps1
Production container uses matching config:

```powershell
podman run -d `
    --name aura-postgres `
    -e POSTGRES_USER=postgres `
    -e POSTGRES_PASSWORD=aura `
    -e POSTGRES_DB=auradb `
    -p 5432:5432 `
    -v aura-pgdata:/var/lib/postgresql/data `
    pgvector/pgvector:pg17
```

### Start-Api.ps1 Port Conflict Detection
Added check for port 5432 conflicts with non-Aura processes:

```powershell
$portCheck = Test-NetConnection -ComputerName localhost -Port 5432
if ($portCheck.TcpTestSucceeded -and -not $existingContainer) {
    Write-Warning "Port 5432 is in use by another process!"
    # Prompt user to continue or exit
}
```

## Files Changed

- `src/Aura.AppHost/AppHost.cs` - Fixed port 5432
- `setup/install-windows.ps1` - Unified database name/user
- `scripts/Start-Api.ps1` - Port conflict detection
- `docs/configuration/settings.md` - Updated port references
- `docs/troubleshooting/common-issues.md` - Updated port references
- `.project/features/completed/postgresql-setup.md` - Updated port references

## Migration Path

For existing users with data in old configuration:

1. Stop all Aura services
2. Export data if needed: `pg_dump -h localhost -p 5432 -U postgres auradb > backup.sql`
3. Remove old container: `podman rm -f aura-postgres`
4. Remove old volume: `podman volume rm aura-pgdata`
5. Start fresh with `Start-Api.ps1`
6. Re-index workspaces

## Success Criteria

- ✅ `Start-Api` uses port 5432
- ✅ `Update-LocalInstall` uses same database config
- ✅ Switching between dev/prod doesn't corrupt migrations
- ✅ Port conflict detection warns users of existing PostgreSQL
