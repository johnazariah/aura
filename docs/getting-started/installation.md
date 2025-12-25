# Installing Aura

This guide walks you through installing Aura on Windows.

## Prerequisites

Before installing Aura, ensure you have:

| Requirement | Details |
|-------------|---------|
| **Windows 10/11** | 64-bit, version 1903 or later |
| **Ollama** | Local LLM runtime - [download here](https://ollama.com) |
| **RAM** | 8GB minimum (16GB+ recommended for larger models) |
| **Disk Space** | ~500MB for Aura + space for LLM models (2-8GB each) |
| **GPU** | Optional but recommended for faster inference |

## Download

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

## What Gets Installed

Aura installs to `C:\Program Files\Aura\` by default:

```
C:\Program Files\Aura\
├── api\                    # Aura API service
├── tray\                   # System tray application
├── agents\                 # AI agent definitions
├── pgsql\                  # PostgreSQL database
├── extension\              # VS Code extension (.vsix)
├── scripts\                # Helper scripts
└── data\                   # Database files
```

## Windows Services

Two services are created:

| Service | Purpose |
|---------|---------|
| **AuraDB** | PostgreSQL database for storing workflows and code index |
| **AuraService** | Main Aura API (optional, if you chose service install) |

## Next Steps

→ Continue to **[First Run Setup](first-run.md)** to configure Ollama and verify everything works.

## Uninstalling

To remove Aura:

1. Open **Settings** → **Apps** → **Installed apps**
2. Find "Aura" and click **Uninstall**
3. The uninstaller will stop services and remove all files

> **Note:** Your database files in `C:\Program Files\Aura\data\` are preserved by default. Delete manually if you want a clean removal.
