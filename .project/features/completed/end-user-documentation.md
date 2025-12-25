# End-User Documentation

**Status:** ✅ Complete  
**Completed:** 2025-12-25  
**Effort:** 8-16 hours  
**Priority:** P1 - Near Term

## Summary

Create user-facing documentation for installing, configuring, and using Aura. Currently only developer documentation exists.

## Current State

- `README.md` has basic overview but targets developers
- `.project/` contains specs and ADRs (internal)
- `docs/TOOL-PREREQUISITES.md` exists but is technical
- No getting-started guide for end users

## Goals

1. Clear installation guide for Windows users
2. First-run experience documentation
3. Workflow creation and management guide
4. Troubleshooting guide
5. Configuration reference

## Documentation Structure

```
docs/
├── README.md                    # Documentation index
├── getting-started/
│   ├── installation.md          # Windows installer guide
│   ├── first-run.md             # Initial setup and configuration
│   └── quick-start.md           # Create your first workflow
├── user-guide/
│   ├── workflows.md             # Creating and managing workflows
│   ├── agents.md                # Understanding and using agents
│   ├── indexing.md              # Code indexing and RAG
│   └── extension.md             # VS Code extension usage
├── configuration/
│   ├── llm-providers.md         # Configuring Ollama, Azure, OpenAI
│   ├── database.md              # PostgreSQL setup
│   └── settings.md              # appsettings.json reference
└── troubleshooting/
    ├── common-issues.md         # FAQ and solutions
    ├── logs.md                  # Finding and reading logs
    └── support.md               # Getting help
```

## Content Outline

### 1. Installation Guide (`getting-started/installation.md`)

```markdown
# Installing Aura

## Prerequisites

Before installing Aura, ensure you have:

- **Windows 10/11** (64-bit)
- **Ollama** - Local LLM runtime ([download](https://ollama.com))
- **4GB+ RAM** (8GB+ recommended for larger models)
- **GPU** (optional but recommended for faster inference)

## Download

1. Go to [Releases](https://github.com/johnazariah/aura/releases)
2. Download `Aura-Setup-X.Y.Z.exe`
3. Run the installer

## Installation Options

- **Install as Windows Service** - Aura starts automatically on boot
- **System Tray** - Monitor Aura status from taskbar
- **VS Code Extension** - Installed automatically

## Verify Installation

1. Open VS Code
2. Look for "Aura" in the sidebar
3. Check that all status indicators are green
```

### 2. First Run Guide (`getting-started/first-run.md`)

```markdown
# First Run Setup

## 1. Start Ollama

Aura needs Ollama running for AI capabilities:

1. Open Ollama from Start Menu
2. Wait for "Ollama is running" notification

## 2. Download a Model

Open PowerShell and run:

ollama pull llama3:8b

This downloads a ~4GB model. Smaller options:
- `ollama pull phi3:mini` (2GB)
- `ollama pull gemma:2b` (1.4GB)

## 3. Open VS Code

1. Open your project folder in VS Code
2. Click the Aura icon in the sidebar
3. Verify "Aura API" shows green checkmark

## 4. Index Your Code

1. In Aura sidebar, click "Index Codebase"
2. Wait for indexing to complete
3. You're ready to create workflows!
```

### 3. Workflow Guide (`user-guide/workflows.md`)

- Creating a workflow from issue/task description
- Understanding workflow stages (Create → Analyze → Plan → Execute)
- Reviewing and approving steps
- Working with generated code
- Finalizing and creating PRs

### 4. Troubleshooting (`troubleshooting/common-issues.md`)

| Problem | Solution |
|---------|----------|
| "Aura API not responding" | Check Windows Service is running |
| "No LLM provider available" | Ensure Ollama is running, model downloaded |
| "Database connection failed" | Check PostgreSQL service |
| "Extension not loading" | Reinstall from VSIX |

## Tasks

- [ ] Create `docs/` folder structure
- [ ] Write installation guide
- [ ] Write first-run guide with screenshots
- [ ] Write workflow tutorial
- [ ] Write troubleshooting FAQ
- [ ] Add configuration reference
- [ ] Link from README.md
- [ ] Add to GitHub Pages (optional)

## Screenshots Needed

- [ ] VS Code extension sidebar
- [ ] System Status panel (all green)
- [ ] New Workflow panel
- [ ] Workflow with steps
- [ ] Step approval dialog
- [ ] System tray icon and menu

## Dependencies

- PostgreSQL Setup spec (for database section)
- Bundled Extension spec (for extension installation)
