# Installing Aura

This guide walks you through installing Aura on **Windows** or **macOS**.

## Prerequisites

Before installing Aura, ensure you have:

### Windows

| Requirement | Details |
|-------------|---------|
| **Windows 10/11** | 64-bit, version 1903 or later |
| **Ollama** | Local LLM runtime - [download here](https://ollama.com) |
| **RAM** | 8GB minimum (16GB+ recommended for larger models) |
| **Disk Space** | ~500MB for Aura + space for LLM models (2-8GB each) |
| **GPU** | Optional but recommended for faster inference |

### macOS

| Requirement | Details |
|-------------|---------|
| **macOS 12+** | Intel or Apple Silicon (M1/M2/M3/M4) |
| **Homebrew** | Package manager - [install from brew.sh](https://brew.sh) |
| **RAM** | 8GB minimum (16GB+ recommended for larger models) |
| **Disk Space** | ~500MB for Aura + space for LLM models (2-8GB each) |

---

## Windows Installation

### Download

1. Go to the [Releases page](https://github.com/johnazariah/aura/releases)
2. Download the latest `Aura-Setup-X.Y.Z.exe`
3. Run the installer

## Installation Steps

### 1. Run the Installer

Double-click `Aura-Setup-X.Y.Z.exe` to start installation.

> **Note:** You may see a Windows SmartScreen warning. Click "More info" → "Run anyway" to proceed.

### 2. Choose Installation Options

The installer offers several options:

| Option | Description | Recommended |
|--------|-------------|-------------|

| **Install as Windows Service** | Aura API starts automatically on boot | ✅ Yes |
| **Start system tray monitor** | Shows Aura status in taskbar | ✅ Yes |
| **Auto-start tray with Windows** | Tray icon appears on login | ✅ Yes |
| **Install VS Code extension** | Adds Aura to VS Code (if detected) | ✅ Yes |

### 3. Complete Installation

Click **Install** and wait for the process to complete. The installer will:

- Install Aura API and system tray application
- Set up PostgreSQL database (bundled)
- Install the VS Code extension (if VS Code is detected)
- Start the Aura service

### 4. Verify Installation

After installation:

1. Look for the **Aura icon** in your system tray (bottom-right)
2. Click it to see the status window
3. All indicators should show green checkmarks

### What Gets Installed (Windows)

Aura installs to `C:\Program Files\Aura\` by default:

```text
C:\Program Files\Aura\
├── api\                    # Aura API service
├── tray\                   # System tray application
├── agents\                 # AI agent definitions
├── prompts\                # Prompt templates
├── pgsql\                  # PostgreSQL binaries
├── extension\              # VS Code extension (.vsix)
└── scripts\                # Helper scripts

C:\ProgramData\Aura\
└── data\                   # PostgreSQL database files
```

### Windows Services

Two services are created:

| Service | Purpose |
|---------|---------|
| **AuraDB** | PostgreSQL database for storing workflows and code index |
| **AuraService** | Main Aura API (optional, if you chose service install) |

---

## macOS Installation

### Quick Start (Homebrew)

For developers building from source, we provide a setup script that installs all prerequisites:

```bash
# Clone the repository
git clone https://github.com/johnazariah/aura.git
cd aura

# Run the macOS setup script
./setup/install-mac.sh
```

The script installs:

| Component | Purpose |
|-----------|---------|
| **OrbStack** | Docker-compatible container runtime |
| **PostgreSQL 16** | Database with pgvector extension |
| **Ollama** | Local LLM runtime |
| **nomic-embed-text** | Embedding model for RAG |
| **qwen2.5-coder:7b** | Code generation model |
| **.NET SDK** | Runtime for Aura |

### Manual Installation

If you prefer to install components manually:

```bash
# Install prerequisites
brew install --cask orbstack
brew install postgresql@16 pgvector
brew install --cask ollama dotnet-sdk

# Start services
brew services start postgresql@16
open -a OrbStack
open -a Ollama

# Set up database
psql postgres -c "CREATE ROLE aura WITH LOGIN PASSWORD 'aura';"
createdb -O aura aura
psql -d aura -c "CREATE EXTENSION IF NOT EXISTS vector;"

# Pull LLM models
ollama pull nomic-embed-text
ollama pull qwen2.5-coder:7b

# Build and run Aura
cd aura
dotnet build
dotnet run --project src/Aura.AppHost
```

### Verify Installation (macOS)

After running the setup:

1. Check that PostgreSQL is running: `brew services list`
2. Check that Ollama is running: `curl http://localhost:11434/api/tags`
3. Start Aura: `dotnet run --project src/Aura.AppHost`
4. API available at: `http://localhost:5280`

---

## Next Steps

→ Continue to **[First Run Setup](first-run.md)** to configure Ollama and verify everything works.

## Uninstalling

To remove Aura:

1. Open **Settings** → **Apps** → **Installed apps**
2. Find "Aura" and click **Uninstall**
3. The uninstaller will stop services and remove all files

> **Note:** Your database files in `C:\ProgramData\Aura\data\` are preserved by default. Delete manually if you want a clean removal.
