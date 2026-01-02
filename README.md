# Aura

[![CI](https://github.com/johnazariah/aura/actions/workflows/ci.yml/badge.svg)](https://github.com/johnazariah/aura/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/johnazariah/aura/graph/badge.svg)](https://codecov.io/gh/johnazariah/aura)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Local-first, privacy-safe AI foundation for knowledge work.**

Think of it as **"Windows Recall, but local and safe"** - your data never leaves your machine.

## What is Aura?

Aura is a cross-platform AI infrastructure that runs entirely on your machine:

- **Local LLM** - Ollama on your GPU/CPU
- **Local Database** - PostgreSQL storing everything locally  
- **Local RAG** - Index and query your files, code, documents
- **Local Agents** - Hot-reloadable AI capabilities

## The Privacy Promise

```text
Works offline - no internet required
LLM runs locally (Ollama)
Database is local (PostgreSQL)
RAG index is local

No cloud uploads
No telemetry  
No API keys required
```

## Architecture

Aura is built from **composable modules**:

| Component | Description |
|-----------|-------------|

| **Aura.Foundation** | Core services - agents, LLM, RAG, database |
| **Aura.Module.Developer** | Code automation, git worktrees, workflows |
| **Aura.Module.Research** | Paper management, synthesis (future) |
| **Aura.Module.Personal** | Receipts, budgets, general assistant (future) |

Enable only what you need:

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer"]
    }
  }
}
```

## Quick Start

```bash
# Clone
git clone https://github.com/johnazariah/aura.git
cd aura

# Install git hooks (recommended)
.\scripts\Install-GitHooks.ps1

# Build
dotnet build

# Run (requires Docker for PostgreSQL, Ollama installed)
dotnet run --project src/Aura.AppHost
```

## Requirements

- .NET 9.0
- Docker (for PostgreSQL)
- Ollama (for local LLM)
- GPU recommended (but CPU works)

## Platforms

| Platform | Status |
|----------|--------|

| Windows | Supported |
| macOS | Supported |
| Linux | Supported |

## Documentation

- [Tool Prerequisites](docs/TOOL-PREREQUISITES.md) - Required external tools for language-specific coding agents
- [Specifications](.project/spec/) - System design and module specifications
- [Implementation Plans](.project/plan/) - Phase-by-phase implementation guides

## License

MIT License - see [LICENSE](LICENSE)
