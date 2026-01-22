# AURA Getting Started Guide - Documentation Prompt

You are tasked with creating comprehensive "Getting Started" documentation that gets new users up and running with Aura in under 15 minutes.

## 🎯 Your Mission

Create documentation in `docs/getting-started/` that provides a smooth onboarding experience, emphasizing Aura's **local-first** design and making the first workflow execution feel like magic.

## 📋 Required Files

### 1. `index.md` - Overview
- What is Aura in one compelling paragraph
- Why use Aura (key benefits)
- What you'll accomplish in this guide
- Prerequisites checklist
- Architecture diagram (ASCII art)
- Quick navigation to installation

### 2. `installation.md` - Complete Setup Guide
- **Step 1**: Install Prerequisites
  - .NET 10 SDK (with download link and version check)
  - Docker Desktop or Podman (for PostgreSQL)
  - OLLAMA (with model download: `ollama pull qwen2.5-coder:7b`)
  - Git (version 2.40+)
  - VS Code (optional but recommended)
  
- **Step 2**: Install Aura
  - Clone repository
  - Build solution (`dotnet build`)
  - Run tests to verify (`dotnet test`)
  
- **Step 3**: Install VS Code Extension
  - From VSIX file (if available)
  - From source (development mode)
  - Verify extension loaded
  
- **Step 4**: Start Aura Services
  - Using Aspire (recommended): `dotnet run --project src/AgentOrchestrator.AppHost`
  - What Aspire provides (PostgreSQL, migrations, health checks)
  - Verify services are running (dashboard at https://localhost:17154)
  - Health check: `curl http://localhost:5258/health`
  
- **Troubleshooting Common Installation Issues**
  - OLLAMA not responding
  - PostgreSQL connection failures
  - Port conflicts
  - Windows Defender exclusions needed

### 3. `first-workflow.md` - Your First Workflow
- **Goal**: Generate a simple Calculator class with tests and docs in under 10 minutes
- **Approach**: Local-first (no GitHub required)

**Using VS Code Extension:**
- Open Aura sidebar
- Create new workflow
- Enter description: "Create a C# calculator with add, subtract, multiply, divide methods"
- Click "Execute Workflow"
- Watch progress in Task Monitor
- Review generated code in your workspace
- Run the generated tests
- Celebrate success! 🎉

**Using API Directly:**
```bash
# Create workflow
curl -X POST http://localhost:5258/api/workflows \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Calculator Service",
    "description": "Create a C# calculator with add, subtract, multiply, divide methods. Include unit tests and documentation.",
    "repository": "my-project",
    "workspacePath": "/path/to/your/workspace"
  }'

# Execute workflow
curl -X POST http://localhost:5258/api/workflows/{id}/execute

# Monitor progress
curl http://localhost:5258/api/workflows/{id}/status

# Review results in your workspace
cd /path/to/your/workspace
git log
git show HEAD
```

**What Just Happened:**
- OrchestrationAgent broke down the task into steps
- BusinessAnalystAgent analyzed requirements
- CodingAgent generated Calculator.cs
- TestingAgent generated CalculatorTests.cs
- DocumentationAgent updated README.md
- ValidationAgent verified code compiles and tests pass
- All changes committed to a feature branch

**Verify the Output:**
```bash
# Check generated files
ls src/Calculator.cs
ls tests/CalculatorTests.cs

# Run the tests
dotnet test

# All tests should pass!
```

### 4. `configuration.md` - Essential Configuration
- **Minimal Configuration** (for local-first)
  - OLLAMA endpoint (default: http://localhost:11434)
  - Default model (qwen2.5-coder:7b)
  - Database (auto-configured by Aspire)
  
- **Optional GitHub Integration**
  - GitHub Personal Access Token
  - Repository configuration
  - When to configure GitHub (not for first workflow!)
  
- **VS Code Extension Settings**
  - Orchestrator API URL
  - Auto-refresh intervals
  - Display preferences
  
- **Configuration File Structure**
  - `appsettings.json` vs `appsettings.Development.json`
  - Environment variables
  - User secrets for sensitive data
  
- **Advanced Configuration**
  - Custom agent models per agent
  - Workspace paths
  - Git configuration
  - Link to full Configuration Reference

### 5. `troubleshooting.md` - Common Issues & Solutions
- **Installation Issues**
  - OLLAMA model not found
  - Docker/Podman not running
  - Port already in use
  - Build failures
  
- **First Workflow Issues**
  - "WorkspacePath is required" error
  - "Workspace must be a Git repository" error
  - OLLAMA timeout errors
  - Compilation failures
  - Test failures
  
- **Extension Issues**
  - Extension not connecting to API
  - GitHub authentication failures
  - Workflows not appearing
  
- **Getting Help**
  - Checking logs
  - Health endpoints
  - GitHub Discussions
  - Filing issues

## ✍️ Writing Guidelines

### Tone
- **Encouraging**: "You're about to see AI generate production-quality code!"
- **Clear**: No jargon without explanation
- **Confidence-building**: Celebrate each success
- **Honest**: If something takes time, say so

### Structure
- **Progressive**: Each step builds on previous
- **Checkpoints**: "Verify this works before continuing"
- **Visual**: Use code blocks, command output examples
- **Copy-paste ready**: All commands should work as-is

### Critical Emphasis
1. **Local-first is the default** - Make this abundantly clear
2. **No GitHub required** for first workflow
3. **Fast feedback** - They should see success quickly
4. **Troubleshooting is proactive** - Address issues before they encounter them

## 📊 Quality Criteria

Your getting started guide succeeds if:

- ✅ User completes installation in 10 minutes
- ✅ User executes first workflow successfully in 5 minutes
- ✅ User understands local-first concept
- ✅ User knows GitHub is optional
- ✅ User can troubleshoot common issues
- ✅ User feels confident to explore further
- ✅ Every command/example works without modification
- ✅ No prerequisite is missing

## 🔍 What to Review

- `README.md` - Current quick start
- `docs/USAGE.md` - Existing usage patterns
- `src/AgentOrchestrator.AppHost/README.md` - Aspire setup
- `extension/QUICKSTART.md` - Extension quick start
- `docs/NEW-MACHINE-SETUP.md` - Setup requirements
- `docs/CONFIGURATION.md` - Configuration options

## 🎯 Example First Workflow Output

Show exactly what files Aura generates:

```
your-workspace/
├── .git/
├── src/
│   └── Calculator.cs          # ← Generated by CodingAgent
│       namespace MyProject;
│       
│       public class Calculator
│       {
│           public int Add(int a, int b) => a + b;
│           public int Subtract(int a, int b) => a - b;
│           ...
│       }
│
├── tests/
│   └── CalculatorTests.cs     # ← Generated by TestingAgent
│       [Fact]
│       public void Add_TwoNumbers_ReturnsSum()
│       {
│           var calc = new Calculator();
│           var result = calc.Add(2, 3);
│           Assert.Equal(5, result);
│       }
│
└── README.md                   # ← Updated by DocumentationAgent
    ## Calculator
    
    A simple calculator service with basic arithmetic operations.
    ...
```

Make it tangible and exciting!

Now create documentation that gets users to their "aha!" moment as fast as possible! 🚀
