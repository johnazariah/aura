# First Run Setup

After installing Aura, follow these steps to get everything working.

## 1. Install and Start Ollama

Aura uses Ollama to run AI models locally on your machine.

### Install Ollama

If you haven't already:

1. Download from [ollama.com](https://ollama.com)
2. Run the installer
3. Ollama starts automatically

### Verify Ollama is Running

Look for the Ollama icon in your system tray. You can also check in PowerShell:

```powershell
ollama --version
```

## 2. Download an AI Model

Ollama needs at least one model to work. Open PowerShell and run:

```powershell
# Recommended: Good balance of speed and quality
ollama pull qwen2.5-coder:7b

# Also pull an embedding model for code search
ollama pull nomic-embed-text
```

### Model Options

| Model | Size | Best For |
|-------|------|----------|

| `qwen2.5-coder:7b` | ~4GB | General coding tasks (recommended) |
| `qwen2.5-coder:14b` | ~8GB | Better quality, needs more RAM |
| `llama3.2:3b` | ~2GB | Faster responses, less capable |
| `deepseek-coder:6.7b` | ~4GB | Alternative coding model |

> **Tip:** Start with `qwen2.5-coder:7b`. You can always pull more models later.

## 3. Verify Aura is Running

### Check the System Tray

1. Look for the Aura icon in your system tray (bottom-right of taskbar)
2. Click it to open the status window
3. You should see:
   - ✅ **AuraDB** - Database running
   - ✅ **Aura API** - API service running
   - ✅ **Ollama** - LLM provider available

### Check from VS Code

1. Open VS Code
2. Look for "Aura" in the Activity Bar (left sidebar)
3. Click to open the Aura panel
4. Check "System Status" - all items should be green

## 4. Index Your First Project

Before Aura can help with your code, it needs to understand your project:

1. Open a project folder in VS Code
2. In the Aura panel, find "Code Graph"
3. Click **"Index Repository"**
4. Wait for indexing to complete (progress shown in status)

### What Gets Indexed

Aura extracts:

- **Functions & Methods** - Names, signatures, documentation
- **Classes & Types** - Structure and relationships
- **Files** - Content for semantic search

### Supported Languages

| Language | Parser |
|----------|--------|

| C# | Roslyn (full semantic analysis) |
| TypeScript/JavaScript | TreeSitter |
| Python | TreeSitter |
| Go | TreeSitter |
| Java | TreeSitter |
| Rust | TreeSitter |
| And more... | TreeSitter |

## 5. Test with Chat

Try asking Aura about your code:

1. In VS Code, open Command Palette (`Ctrl+Shift+P`)
2. Run "Aura: Open Chat"
3. Ask a question like:
   - "What does the `UserService` class do?"
   - "How is authentication implemented?"
   - "Find all API endpoints"

Aura will search your indexed code and provide context-aware answers.

## Troubleshooting First Run

| Issue | Solution |
|-------|----------|

| Ollama not detected | Ensure Ollama is running (check system tray) |
| No models available | Run `ollama pull qwen2.5-coder:7b` |
| Database not starting | Check Windows Services for "AuraDB" |
| Extension not visible | Reinstall: `& "$env:ProgramFiles\Aura\scripts\install-extension.ps1"` |

## Next Steps

→ Continue to **[Quick Start](quick-start.md)** to create your first AI-assisted workflow.

## Optional: Configure Cloud LLM

For better quality responses, you can use cloud LLM providers:

- [Configure Azure OpenAI](../configuration/llm-providers.md#azure-openai)
- [Configure OpenAI](../configuration/llm-providers.md#openai)
