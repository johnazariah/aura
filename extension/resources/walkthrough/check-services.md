# Check Services

Aura requires two services to be running:

## 1. Aura API Server
The main backend that provides AI capabilities.

**Status indicators:**
- ✅ **Online** - Ready to use
- ❌ **Offline** - See below to start

**Starting the API:**
- **Installed version**: The AuraService should start automatically. Check Windows Services or use the Aura system tray app.
- **Development**: Run `Start-Api` in PowerShell from the repo folder.

## 2. Ollama (or Cloud LLM)
Provides the AI models for code generation and chat.

**For local Ollama:**
- Install from [ollama.ai](https://ollama.ai)
- Run `ollama serve` or use the Ollama app

**For cloud (Azure OpenAI/OpenAI):**
- Configure in settings or `appsettings.json`
- See documentation for setup

## Quick Check
Click the **Refresh** button in System Status to verify connections.
