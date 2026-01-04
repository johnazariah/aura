# Check Services

Aura requires two services to be running:

## 1. Aura API Server
The main backend that provides AI capabilities.

**Status indicators:**
- ✅ **Online** - Ready to use
- ❌ **Offline** - Start the server with `Start-Api` in PowerShell

## 2. Ollama (or Cloud LLM)
Provides the AI models for code generation and chat.

**For local Ollama:**
- Install from [ollama.ai](https://ollama.ai)
- Run `ollama serve` or use the Ollama app

**For cloud (Azure OpenAI/OpenAI):**
- Configure in `appsettings.json`
- See documentation for setup

## Quick Check
Click the **Refresh** button in System Status to verify connections.
