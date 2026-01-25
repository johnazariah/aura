# Windows Installer Build Guide

> **Last Updated:** 2026-01-04  
> **Purpose:** Complete reference for building the Aura Windows installer

## Quick Start

```powershell
# Full publish + installer build
.\scripts\Publish-Release.ps1 -Version 1.0.1
& "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe" "/DMyAppVersion=1.0.1" "installers/windows/Aura.iss"
```

Output: `publish/installers/Aura-Setup-1.0.1.exe` (~110MB)

---

## Prerequisites

### Required Tools

| Tool | Purpose | Installation |
|------|---------|--------------|
| **.NET 10 SDK** | Build Aura.Api and Aura.Tray | Pre-installed |
| **Node.js 20+** | Build VS Code extension | Pre-installed |
| **Inno Setup 6** | Create Windows installer | `winget install JRSoftware.InnoSetup` |

### Inno Setup Location

Inno Setup may install to different locations:
- User install (winget): `$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe`
- System install: `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`

The `Build-Installer.ps1` script searches both locations.

---

## Build Process

### Step 1: Publish Release

```powershell
.\scripts\Publish-Release.ps1 -Version 1.0.1
```

This script:
1. Builds `Aura.Api` (self-contained win-x64)
2. Builds `Aura.Tray` (self-contained win-x64)
3. Copies `agents/` folder
4. Copies `prompts/` folder
5. Builds VS Code extension (npm ci, webpack, vsce package)
6. Downloads PostgreSQL 16.4-1 binaries (~100MB download, cached)
7. Removes bloat from PostgreSQL (pgAdmin, symbols, docs)

### Step 2: Build Installer

```powershell
.\scripts\Build-Installer.ps1 -Version 1.0.1
# Or directly:
& "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe" "/DMyAppVersion=1.0.1" "installers/windows/Aura.iss"
```

---

## Key Learnings & Gotchas

### PostgreSQL Bundling

#### ⚠️ pgAdmin 4 Bloat (~800MB)

The PostgreSQL Windows binaries include pgAdmin 4 with a full Python 3.12 environment. **This is not needed for Aura** and must be removed:

```powershell
# Folders to remove from pgsql (saves ~800MB):
Remove-Item "$pgsqlDir/pgAdmin 4" -Recurse -Force  # ~616MB (includes Python)
Remove-Item "$pgsqlDir/symbols" -Recurse -Force    # ~180MB (debug symbols)
Remove-Item "$pgsqlDir/doc" -Recurse -Force        # ~10MB
Remove-Item "$pgsqlDir/include" -Recurse -Force    # ~4MB (headers)
Remove-Item "$pgsqlDir/StackBuilder" -Recurse -Force  # ~2MB
```

**Result:** PostgreSQL folder shrinks from ~920MB to ~120MB.

#### ⚠️ initdb --locale Flag

On Windows, `initdb` does NOT support `--locale=en_US.UTF-8`. This causes errors:

```
initdb: error: invalid locale name "en_US.UTF-8"
```

**Solution:** Omit the `--locale` flag on Windows. Windows uses system locale automatically.

```inno
; CORRECT (Windows):
Filename: "{app}\pgsql\bin\initdb.exe"; Parameters: "-D ""{commonappdata}\Aura\data"" -U postgres -E UTF8"

; WRONG (Linux-style):
Filename: "{app}\pgsql\bin\initdb.exe"; Parameters: "-D ... --locale=en_US.UTF-8"
```

#### ⚠️ Data Directory Location

**Never put database files in Program Files!** Use ProgramData instead:

```inno
; CORRECT - writable by service:
{commonappdata}\Aura\data  ; Expands to C:\ProgramData\Aura\data

; WRONG - requires admin, causes permission issues:
{app}\data  ; Expands to C:\Program Files\Aura\data
```

### VS Code Extension Build

#### ⚠️ npm Dependencies

The extension build requires running `npm ci` (or `npm install`) before packaging. The publish script handles this, but if building manually:

```powershell
cd extension
npm ci                              # Install dependencies
npm run package                     # Build with webpack
npx @vscode/vsce package           # Create .vsix
```

#### ⚠️ Deprecation Warnings

You may see npm deprecation warnings like:
- `inflight@1.0.6: This module is not supported`
- `eslint@8.57.1: This version is no longer supported`

**These are warnings, not errors.** The build continues successfully.

### API Configuration

#### ⚠️ Production vs Development Paths

The API needs different paths for agents/prompts in production vs development:

**appsettings.json** (fallback paths):
```json
{
  "Aura": {
    "Directories": {
      "Agents": ["../agents", "agents"],
      "Prompts": ["../prompts", "../../prompts"]
    }
  }
}
```

**appsettings.Production.json** (installed mode):
```json
{
  "Aura": {
    "Directories": {
      "Agents": ["../agents"],
      "Prompts": ["../prompts"]
    }
  }
}
```

The installer must set `ASPNETCORE_ENVIRONMENT=Production`:

```inno
[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\AuraService"; ValueType: string; ValueName: "Environment"; ValueData: "ASPNETCORE_ENVIRONMENT=Production"; Flags: uninsdeletevalue
```

### Prompts Folder

#### ⚠️ Must Be Bundled

The `prompts/` folder contains Handlebars templates required by agents. **The installer previously did not include this folder**, causing runtime errors:

```
System.IO.DirectoryNotFoundException: Prompts directory not found
```

**Solution:** Added to Publish-Release.ps1:
```powershell
Copy-Item "prompts" "$OutputDir/win-x64/prompts" -Recurse -Force
```

Added to Aura.iss:
```inno
Source: "..\..\publish\win-x64\prompts\*"; DestDir: "{app}\prompts"; Flags: ignoreversion recursesubdirs
```

---

## Final Installer Contents

After a successful build, `publish/win-x64/` contains:

```
publish/win-x64/
├── api/              # ~180MB - Self-contained .NET API
│   ├── Aura.Api.exe
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── ... (DLLs, Roslyn, etc.)
├── tray/             # ~25MB - System tray app
│   └── Aura.Tray.exe
├── agents/           # ~50KB - Agent markdown definitions
│   └── *.md
├── prompts/          # ~20KB - Handlebars templates
│   └── *.prompt
├── extension/        # ~300KB - VS Code extension
│   └── aura-1.0.1.vsix
├── scripts/          # ~2KB - Helper scripts
│   └── install-extension.ps1
└── pgsql/            # ~120MB - PostgreSQL 16 (trimmed)
    ├── bin/
    ├── lib/
    └── share/
```

**Total uncompressed:** ~325MB  
**Installer size:** ~110MB (LZMA2 compression)

---

## Troubleshooting

### Build Failures

| Error | Cause | Solution |
|-------|-------|----------|
| `ISCC not found` | Inno Setup not installed | `winget install JRSoftware.InnoSetup` |
| `npm error` during extension build | Node.js issue | Ensure Node.js 20+ installed |
| `vsce: command not found` | Missing npm dependency | Run `npm ci` in extension folder |
| Compression abort during Inno Setup | Installer too large | Remove pgAdmin from pgsql folder |

### Runtime Failures After Install

| Error | Cause | Solution |
|-------|-------|----------|
| `Prompts directory not found` | prompts/ not bundled | Add prompts to publish script |
| `initdb: invalid locale` | --locale flag on Windows | Remove --locale from initdb command |
| `Access denied` on data folder | Data in Program Files | Use {commonappdata} for data |
| Extension not loading prompts | Wrong ASPNETCORE_ENVIRONMENT | Set Production in registry |

---

## pgvector Extension

**Status:** Not yet bundled (marked as high priority)

Vector search requires the pgvector extension. Without it:
- Semantic search (RAG) is disabled
- Code indexing still works (uses keyword search)

To add pgvector:
1. Compile pgvector.dll for PostgreSQL 16 on Windows
2. Place in `installers/pgsql-extensions/`
3. Update Publish-Release.ps1 to copy to pgsql/lib/

See: `installers/pgsql-extensions/README.md`

---

## Version Checklist

When releasing a new version:

- [ ] Update version in `scripts/Publish-Release.ps1` call
- [ ] Extension version auto-syncs from parameter
- [ ] Pass version to ISCC: `/DMyAppVersion=X.Y.Z`
- [ ] Verify installer filename matches version
