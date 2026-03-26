# Installation

## Prerequisites

| Requirement | Minimum | Notes |
|---|---|---|
| OS | Windows 10 1809+ (x64) or macOS 12+ | |
| RAM | 8 GB | More helps with larger models |
| Disk | ~500 MB + space for models | Ollama models are ~4 GB each |
| Ollama | Latest | Required for local embeddings |
| PostgreSQL | 15+ with pgvector | Bundled on Windows; Homebrew on macOS |

## Windows — Installer

1. Download the latest `.exe` installer from [GitHub Releases](https://github.com/johnazariah/aura/releases).
2. Run the installer. It will:
   - Install Aura to `C:\Program Files\Aura\`
   - Bundle PostgreSQL (port **5433**) with pgvector
   - Create two Windows Services: **AuraDB** (PostgreSQL) and **AuraService** (Aura API)
   - Optionally install the Aura system tray app
3. The installer checks for port conflicts on 5433 and 5300. If either port is in use, you will be prompted to resolve the conflict.
4. Install [Ollama](https://ollama.com/) separately and pull the required model:

```powershell
ollama pull nomic-embed-text
```

5. Verify the services are running:

```powershell
Get-Service AuraDB, AuraService | Format-Table Name, Status
```

### Upgrading

Run a newer installer over an existing installation. Your data in `C:\ProgramData\Aura\data` is preserved.

## macOS — Manual Setup

### 1. Install PostgreSQL with pgvector

```bash
brew install postgresql@17
brew install pgvector
brew services start postgresql@17
```

Create the database:

```bash
createuser -s aura
createdb -O aura aura
psql -d aura -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

### 2. Install Ollama

```bash
brew install ollama
ollama serve &
ollama pull nomic-embed-text
```

### 3. Install Aura

Run the install script from the repository:

```bash
cd installers/macos
chmod +x install-service.sh
./install-service.sh
```

This installs Aura to `/usr/local/share/aura`, creates a launch agent (`com.aura.api`), and starts the API on `http://localhost:5300`.

### 4. Verify

```bash
curl http://localhost:5300/health
```

## Uninstalling

### Windows

Use **Add or Remove Programs** or run the uninstaller from `C:\Program Files\Aura\`.

### macOS

```bash
cd installers/macos
chmod +x uninstall-service.sh
./uninstall-service.sh
```

## Next Steps

- [First Run](first-run.md) — verify your installation
- [Quick Start](quick-start.md) — index your first project

