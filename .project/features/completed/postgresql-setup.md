# PostgreSQL Setup for End Users

**Status:** ✅ Complete  
**Completed:** 2025-12-25  
**Effort:** 2-4 hours  
**Priority:** P0 - Required for Distribution

## Summary

The current installer checks for Ollama but not PostgreSQL. End users need a simple way to get PostgreSQL running without Docker knowledge.

## Current State

- Installer (`Aura.iss`) checks for Ollama installation
- No PostgreSQL check or setup
- Development uses Aspire with containerized PostgreSQL
- Production expects `ConnectionStrings:auradb` in config

## Options Considered

### Option A: Embedded PostgreSQL (Recommended)

Bundle a portable PostgreSQL with the installer.

**Pros:**

- Zero configuration for users
- Works offline
- No Docker dependency

**Cons:**

- Larger installer (~100MB more)
- Need to manage PostgreSQL lifecycle

### Option B: PostgreSQL Installer Redirect

Check for PostgreSQL, prompt to download if missing.

**Pros:**

- Standard PostgreSQL installation
- User manages their own database

**Cons:**

- Extra step for users
- Version compatibility concerns

### Option C: SQLite for Single-User

Use SQLite instead of PostgreSQL for desktop use.

**Pros:**

- Zero setup
- Tiny footprint

**Cons:**

- Loses pgvector (no vector search)
- Different code path from server deployment

## Recommended Approach: Embedded PostgreSQL

### Implementation

#### 1. Bundle PostgreSQL Portable

Download PostgreSQL portable binaries and include in installer:

```
publish/win-x64/
├── api/
├── tray/
├── agents/
└── pgsql/           # NEW
    ├── bin/
    │   ├── postgres.exe
    │   ├── initdb.exe
    │   └── ...
    ├── lib/
    └── share/
```

#### 2. Update Installer

**File:** `installers/windows/Aura.iss`

Add PostgreSQL files:
```inno
[Files]
Source: "..\..\publish\win-x64\pgsql\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs

[Run]
; Initialize database on first install
Filename: "{app}\pgsql\bin\initdb.exe"; Parameters: "-D ""{app}\data"" -U postgres -E UTF8"; Flags: runhidden; Check: not DatabaseExists
; Create auradb database
Filename: "{app}\pgsql\bin\createdb.exe"; Parameters: "-U postgres auradb"; Flags: runhidden; Check: not DatabaseExists
; Enable pgvector extension
Filename: "{app}\pgsql\bin\psql.exe"; Parameters: "-U postgres -d auradb -c ""CREATE EXTENSION IF NOT EXISTS vector"""; Flags: runhidden

[Code]
function DatabaseExists(): Boolean;
begin
  Result := DirExists(ExpandConstant('{app}\data'));
end;
```

#### 3. Create Windows Service for PostgreSQL

**File:** `installers/windows/Aura.iss`

```inno
[Run]
; Register PostgreSQL as service
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "register -N AuraDB -D ""{app}\data"""; Flags: runhidden
; Start PostgreSQL service
Filename: "sc.exe"; Parameters: "start AuraDB"; Flags: runhidden

[UninstallRun]
; Stop and remove PostgreSQL service
Filename: "sc.exe"; Parameters: "stop AuraDB"; Flags: runhidden
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "unregister -N AuraDB"; Flags: runhidden
```

#### 4. Update Aura.Api Configuration

**File:** `src/Aura.Api/appsettings.json`

```json
{
  "ConnectionStrings": {
    "auradb": "Host=localhost;Port=5433;Database=auradb;Username=postgres"
  }
}
```

Use port 5433 to avoid conflicts with existing PostgreSQL installations.

#### 5. Update Publish Script

**File:** `scripts/Publish-Release.ps1`

```powershell
# Download PostgreSQL portable if not cached
$pgVersion = "16.1"
$pgZip = "postgresql-$pgVersion-1-windows-x64-binaries.zip"
$pgUrl = "https://get.enterprisedb.com/postgresql/$pgZip"

if (-not (Test-Path "cache/$pgZip")) {
    Write-Host "Downloading PostgreSQL $pgVersion..."
    Invoke-WebRequest -Uri $pgUrl -OutFile "cache/$pgZip"
}

# Extract to publish folder
Expand-Archive "cache/$pgZip" -DestinationPath "$OutputDir/win-x64/pgsql" -Force
```

### pgvector Extension

PostgreSQL needs the pgvector extension for vector search. Options:

1. **Pre-compile pgvector** and bundle with PostgreSQL binaries
2. **Use HNSW index** alternative that doesn't require extension
3. **Download at runtime** from pgvector releases

Recommended: Pre-compile and bundle. The extension is ~1MB.

## Tasks

- [ ] Download PostgreSQL 16 portable binaries
- [ ] Compile pgvector for Windows
- [ ] Update `Publish-Release.ps1` to bundle PostgreSQL
- [ ] Update `Aura.iss` to install and configure PostgreSQL
- [ ] Create `AuraDB` Windows service
- [ ] Test fresh install on clean Windows VM
- [ ] Update documentation

## Alternative: Docker Desktop Detection

If user has Docker Desktop, offer to use containerized PostgreSQL:

```inno
[Code]
function DockerExists(): Boolean;
begin
  Result := FileExists('C:\Program Files\Docker\Docker\Docker Desktop.exe');
end;

// In InitializeSetup, offer choice:
// - Use embedded PostgreSQL (default)
// - Use Docker (if detected)
```

## File Size Impact

| Component | Size |
|-----------|------|
| PostgreSQL binaries | ~120 MB (after trimming) |
| pgvector extension | ~1 MB (not yet bundled) |
| **Total addition** | ~121 MB |

Current installer is ~110MB total (LZMA2 compressed).

## Dependencies

- None (but blocks end-user documentation database section)

---

## Implementation Notes (2025-01-04)

### Critical Learnings

#### 1. pgAdmin Bloat Removal

The PostgreSQL Windows binaries include pgAdmin 4 with a full Python 3.12 environment (~616MB). **This must be removed** in `Publish-Release.ps1`:

```powershell
# Remove unnecessary folders (saves ~800MB)
@("pgAdmin 4", "symbols", "doc", "include", "StackBuilder") | ForEach-Object {
    Remove-Item "$pgsqlDir/$_" -Recurse -Force -ErrorAction SilentlyContinue
}
```

**Size reduction:** 920MB → 120MB

#### 2. Windows Locale Flag

On Windows, `initdb` does NOT support `--locale=en_US.UTF-8`:

```
initdb: error: invalid locale name "en_US.UTF-8"
```

**Solution:** Omit the `--locale` flag entirely. Windows uses system locale automatically.

#### 3. Data Directory Location

**Never use `{app}\data`** (Program Files). Use `{commonappdata}\Aura\data` instead:

- `{app}` = `C:\Program Files\Aura\` (requires admin, UAC issues)
- `{commonappdata}` = `C:\ProgramData\Aura\` (writable by services)

#### 4. prompts/ Folder

The `prompts/` folder with Handlebars templates must be bundled alongside `agents/`. Without it, agents fail at runtime.

### Actual Installer Size

| Component | Uncompressed | Compressed |
|-----------|-------------|------------|
| API (self-contained .NET 10) | ~180 MB | ~60 MB |
| Tray app | ~25 MB | ~8 MB |
| PostgreSQL (trimmed) | ~120 MB | ~40 MB |
| Extension + agents + prompts | ~1 MB | ~0.5 MB |
| **Total** | ~325 MB | **~110 MB** |
