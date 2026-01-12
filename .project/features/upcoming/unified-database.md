# Unified Database Configuration

**Status:** 🚧 In Progress  
**Priority:** Critical (blocking development workflow)  
**Estimated Effort:** Low (1-2 hours)

## Problem

Switching between `Start-Api` (dev) and `Update-LocalInstall` (production) causes database migration failures because they use different configurations:

| Mode | Port | Database | User | Container |
|------|------|----------|------|-----------|
| Dev (Aspire) | 5433 (dynamic) | `auradb` | `postgres` | `aura-postgres` |
| Production | 5432 | `aura` | `aura` | `aura-postgres` |

Same container name + different configs = migration state corruption.

## Solution

Standardize on ONE database configuration used by both dev and production:

| Setting | Value |
|---------|-------|
| Container | `aura-postgres` |
| Port | `5432` (fixed) |
| Database | `auradb` |
| User | `postgres` |
| Password | (dev default via Aspire, configurable in prod) |
| Image | `pgvector/pgvector:pg17` |

## Implementation

### 1. Update AppHost.cs

Pin Aspire's Postgres to port 5432:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithContainerName("aura-postgres")
    .WithDataVolume("aura-postgres-data")
    .WithHostPort(5432)  // <-- ADD: Fixed port
    .WithPgAdmin();

var auraDb = postgres.AddDatabase("auradb");
```

### 2. Update install-windows.ps1

Change database name and user to match:

```powershell
podman run -d `
    --name aura-postgres `
    -e POSTGRES_USER=postgres `       # Changed from 'aura'
    -e POSTGRES_PASSWORD=$env:AURA_DB_PASSWORD `  # Use env var
    -e POSTGRES_DB=auradb `           # Changed from 'aura'
    -p 5432:5432 `
    -v aura-pgdata:/var/lib/postgresql/data `
    pgvector/pgvector:pg17            # Updated to pg17
```

### 3. Update connection string defaults

Ensure `appsettings.json` uses the standard connection:

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5432;Database=auradb;Username=postgres;Password=${DEV_PASSWORD}"
  }
}
```

Note: In dev, the actual password is set via Aspire's generated connection string. The above is a placeholder example.

### 4. Add port conflict detection

In `Start-Api.ps1`, check if port 5432 is already in use by a non-Aura process:

```powershell
$existingContainer = podman ps -a --filter "name=aura-postgres" --format "{{.Names}}" 2>$null
$portInUse = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue

if ($portInUse.TcpTestSucceeded -and -not $existingContainer) {
    Write-Warning "Port 5432 is in use by another process. Stop it or use a different port."
}
```

## Migration Path

For existing users who have data in the old configuration:

1. Stop all Aura services
2. Export data if needed: `pg_dump -h localhost -p 5432 -U aura aura > backup.sql`
3. Remove old container: `podman rm -f aura-postgres`
4. Remove old volume: `podman volume rm aura-pgdata`
5. Start fresh with `Start-Api.ps1`
6. Re-index workspaces

## Success Criteria

- [ ] `Start-Api` uses port 5432
- [ ] `Update-LocalInstall` uses same database config
- [ ] Switching between dev/prod doesn't corrupt migrations
- [ ] Existing dev environments work after container reset

## Files Changed

- `src/Aura.AppHost/AppHost.cs` - Pin port 5432
- `setup/install-windows.ps1` - Update DB name/user
- `scripts/Start-Api.ps1` - Add port conflict check (optional)
