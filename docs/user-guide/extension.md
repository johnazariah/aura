# VS Code Extension

The Aura VS Code extension provides the primary interface for interacting with Aura.

## Features Overview

| Feature | Description |
|---------|-------------|

| **Workflows** | Create, manage, and execute AI-assisted development tasks |
| **Chat** | Code-aware conversations about your codebase |
| **Code Graph** | View and manage code indexing |
| **System Status** | Monitor Aura services and dependencies |

## The Aura Panel

Click the **Aura icon** in the Activity Bar (left sidebar) to open the Aura panel.

### Sections

#### System Status

Shows health of Aura components:

- ✅ Aura API - Core service
- ✅ Database - PostgreSQL connection
- ✅ Ollama - LLM provider
- ✅ Code Graph - Indexing status

#### Workflows

Lists your workflows with status indicators:

- 🟡 In Progress
- ✅ Completed
- ❌ Failed

Click a workflow to open it.

#### Chat

Quick access to code-aware chat.

#### Code Graph

- Repository indexing status
- Symbol counts
- Index/Re-index buttons

## Commands

Access via Command Palette (`Ctrl+Shift+P`):

| Command | Description |
|---------|-------------|
| `Aura: New Workflow` | Create a new workflow |
| `Aura: Start Story from Issue` | Create a workflow from a GitHub issue URL |
| `Aura: Open Chat` | Open chat panel |
| `Aura: Index Repository` | Start code indexing |
| `Aura: Show System Status` | View service health |
| `Aura: Clean Up Workflows` | Remove old workflow data |
| `Aura: Show Aura Logs` | View Aura API logs in the output panel |
| `Aura: Show Ollama Logs` | View Ollama service logs |
| `Aura: Verify Workflow` | Run verification checks on current workflow |

## Workflow Panel

When you open a workflow:

### Header

- Workflow name and description
- Current status
- Created/updated timestamps

### Steps List

- Each step with status icon
- Click to expand details
- Approve/Skip/Edit buttons

### Diff Viewer

For file modifications:

- Side-by-side comparison
- Inline diff view toggle
- Syntax highlighting

### Actions

- **Approve Step** - Apply changes
- **Skip Step** - Move to next
- **Edit Step** - Modify before applying
- **Finalize** - Complete workflow

## Chat Panel

### Input

- Multi-line text input
- Send with Enter or button
- Clear history option

### Messages

- Your questions
- Aura's responses with:
  - Code blocks (syntax highlighted)
  - File references (clickable)
  - Action buttons

### Context

Shows what context Aura is using:

- Current file
- Selection (if any)
- Retrieved code snippets

## Status Bar

The status bar (bottom of VS Code) shows:

- **Aura status icon** - Green (connected), Yellow (warning), Red (error)
- Click for quick status popup

## Settings

Configure the extension in VS Code settings:

```json
{
  "aura.apiUrl": "http://localhost:5300",
  "aura.autoConnect": true,
  "aura.showStatusBar": true
}
```

| Setting | Default | Description |
|---------|---------|-------------|

| `aura.apiUrl` | `http://localhost:5300` | Aura API endpoint |
| `aura.autoConnect` | `true` | Connect on VS Code start |
| `aura.showStatusBar` | `true` | Show status in status bar |

## MCP Integration with GitHub Copilot

Aura exposes your indexed codebase to GitHub Copilot via MCP (Model Context Protocol). This enables Copilot to use Aura's semantic code understanding.

### Setup

Add to your VS Code `settings.json`:

```json
{
  "github.copilot.chat.codeGeneration.instructions": [
    { "file": ".github/copilot-instructions.md" }
  ],
  "mcp": {
    "servers": {
      "aura": {
        "url": "http://localhost:5300/mcp"
      }
    }
  }
}
```

### Available MCP Tools

When configured, Copilot can use these Aura tools:

| Tool | Description |
|------|-------------|
| `aura_search` | Semantic code search across your codebase |
| `aura_navigate` | Find callers, implementations, usages |
| `aura_inspect` | Explore type members and structure |
| `aura_validate` | Check compilation or run tests |
| `aura_refactor` | Rename, extract, change signatures |
| `aura_generate` | Create types, generate tests |
| `aura_workflow` | Manage development workflows |
| `aura_pattern` | Load operational patterns |

### Example Usage

When chatting with Copilot, you can ask it to use Aura's tools:

```
"Search my codebase for authentication logic"
→ Copilot uses aura_search to find relevant code

"Generate tests for the UserService class"
→ Copilot uses aura_generate to create comprehensive tests

"Rename User to Customer across the codebase"
→ Copilot loads a pattern and uses aura_refactor
```

## Viewing Logs

### Aura API Logs

View logs from the Aura API server:

1. **Command Palette** → "Aura: Show Aura Logs"
2. Logs appear in the **Output** panel under "Aura"

### Ollama Logs

View logs from the Ollama LLM service:

1. **Command Palette** → "Aura: Show Ollama Logs"
2. Logs appear in the **Output** panel under "Ollama"

Useful for troubleshooting:
- Model loading issues
- Slow inference
- Memory problems

## Keyboard Shortcuts

Default shortcuts (customizable in VS Code):

| Shortcut | Command |
|----------|---------|

| (none by default) | Aura: New Workflow |
| (none by default) | Aura: Open Chat |

### Setting Shortcuts

1. Open Keyboard Shortcuts (`Ctrl+K Ctrl+S`)
2. Search for "Aura"
3. Click the + to add a keybinding

## Troubleshooting

### Extension Not Loading

1. Check extension is installed:
   - Extensions panel (`Ctrl+Shift+X`)
   - Search "Aura"
   - Should show as installed

2. Reinstall if needed:

   ```powershell
   & "$env:ProgramFiles\Aura\scripts\install-extension.ps1"
   ```

3. Reload VS Code:
   - Command Palette → "Developer: Reload Window"

### Not Connecting to API

1. Check Aura service is running:
   - Look for system tray icon
   - Or check Windows Services for "AuraService"

2. Verify URL in settings matches your setup

3. Check API is responding:

   ```powershell
   curl http://localhost:5300/health
   ```

### Slow Performance

1. Large repositories may slow indexing
2. Check system resources (RAM, CPU)
3. Consider configuring exclusions

## Updates

The extension is bundled with the Aura installer. To update:

1. Download new Aura installer
2. Run installer (upgrades in place)
3. Reload VS Code

Manual update from VSIX:

```powershell
code --install-extension "C:\Program Files\Aura\extension\aura-X.Y.Z.vsix" --force
```
