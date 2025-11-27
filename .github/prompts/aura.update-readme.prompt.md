# AURA README Maintenance - Agent Orchestrator Project

You are tasked with maintaining the project's README.md file to ensure it accurately reflects the current state of the **Agent Orchestrator** (also known as **AURA**) project.

## 🎯 Your Mission

Thoroughly review the source code, project structure, documentation in the `plan` folder (especially `STATUS.md` which may be out of date), and update the `README.md` file to provide a comprehensive, well-organized, and accurate overview of the project.

## 📋 Required README Sections

Your updated README must include the following sections in this order:

### 1. Quick Start Guide for Aura VS Code Extension
- **Purpose**: Get users up and running with Aura in minutes
- **Content**:
  - Prerequisites (VS Code version, .NET SDK, OLLAMA, Docker/Podman)
  - Installation steps for the VS Code extension
  - Basic configuration (workspace setup, API connection)
  - First workflow execution example
  - Troubleshooting common setup issues
- **Links**: 
  - Extension documentation (check `extension/` or `docs/` folders)
  - Configuration guide
  - Extension marketplace link (if published)

### 2. Walkthrough: Using Aura to Accelerate Development
- **Purpose**: Show developers how Aura fits into their workflow
- **Content**:
  - Step-by-step guide: From issue creation to PR merge
  - How to sync GitHub issues to local workspace
  - How to trigger AI-powered workflows
  - How to monitor agent progress in real-time
  - How to review and iterate on generated code
  - How to handle validation failures and agent feedback
  - Tips for getting the best results from AI agents
- **Tone**: Practical, example-driven, addresses real developer workflows

### 3. Architecture Overview: Design for Extensibility
- **Purpose**: Explain key architectural decisions that enable extensibility
- **Content**:
  - High-level system architecture diagram (ASCII or description)
  - Multi-provider LLM system (ILlmProvider abstraction, registry pattern)
  - Plugin architecture (hot-reload, agent/provider plugins)
  - Agent pipeline and orchestration model
  - Git workspace management approach
  - Database design for state persistence
  - RAG foundation and knowledge ingestion
- **Focus**: WHY decisions were made, not just WHAT they are
  - Why provider abstraction? (Support multiple LLMs, avoid vendor lock-in)
  - Why plugin system? (Extend without recompilation, hot-reload for development)
  - Why sequential agent execution? (Simpler debugging, clear dependencies)
  - Why workspace-based? (Reproducibility, isolation, Git integration)
- **Keep it concise**: ~2-3 paragraphs per architectural component

### 4. Feature Inventory: Concise but Comprehensive
- **Purpose**: List ALL implemented features without excessive detail
- **Format**: Organized by category (keep descriptions to 1-2 lines each)
- **Categories to cover**:
  - **Core Capabilities**: Workflow orchestration, state management, background monitoring
  - **AI Agents**: List all operational agents (BA, Coding, Testing, Documentation, Orchestration, Validation, PrHealthMonitor)
  - **LLM Integration**: Multi-provider system, OLLAMA support, model configuration
  - **Git Integration**: Branch management, commits, PR creation, PR monitoring
  - **Code Analysis**: Roslyn semantic analysis, code ingestion, validation tools
  - **Knowledge Management**: RAG foundation, vector embeddings, code search
  - **VS Code Extension**: Features like issue sync, workflow triggers, progress tracking, conversation UI
  - **API**: REST endpoints, health checks, OpenAPI documentation
  - **Plugin System**: Dynamic loading, hot-reload, agent/provider extensibility
  - **Database**: PostgreSQL with pgvector, EF Core migrations, state persistence
  - **Development Tools**: Aspire orchestration, multi-tier testing, code coverage
- **Rule**: Keep it SHORT but list EVERYTHING (no feature left behind)

### 5. Customization: Creating and Registering New Agents
- **Purpose**: Guide developers who want to extend Aura with custom agents
- **Content**:
  - Quick overview of agent development options (direct integration vs plugin)
  - Link to plugin development guide: `docs/PLUGIN_SYSTEM.md`
  - Link to agent definition format guide (`.md` files in `agents/` folder)
  - Link to multi-provider agent guide: `docs/MULTI-PROVIDER-AGENTS.md`
  - Simple code example: Minimal agent implementation
  - Registration process (DI for direct, plugin loading for dynamic)
  - Hot-reload capability explanation
- **Tone**: Encouraging, make it seem accessible

### 6. Contributing to the Project
- **Purpose**: Welcome contributors and set expectations
- **Content**:
  - Development philosophy: Greenfield mode, no backward compatibility required
  - How to get started (clone, build, run tests)
  - Code style and conventions (C# 13 modern features, Result<T> pattern)
  - Testing requirements (unit tests before commit, smoke tests before PR)
  - PR submission guidelines
  - Link to detailed developer guide: `docs/AURA-DEVELOPER-GUIDE.md`
  - Areas where contributions are especially welcome
  - Community resources (discussions, issues, contact info)

## 🔍 What to Review

Before updating the README, thoroughly examine:

### Source Code Structure
- `src/` - All projects, understand what each does
- `agents/` - Agent definitions (markdown files with frontmatter)
- `extension/` - VS Code extension code and documentation
- `tests/` - Testing infrastructure and coverage

### Documentation in `plan/`
- `STATUS.md` - **CAUTION**: May be out of date, verify against actual code
- All numbered folders (`00-foundation/`, `01-agents/`, etc.)
- Individual story files - Check completion status vs actual implementation
- `AURA_EXECUTIVE_SUMMARY_2025-11-18.md`
- `AURA_STATE_AND_GAP_ANALYSIS_2025-11-18.md`

### Documentation in `docs/`
- `AURA-DEVELOPER-GUIDE.md`
- `MULTI-PROVIDER-AGENTS.md`
- `PLUGIN_SYSTEM.md`
- `CONFIGURATION.md`
- Any other relevant guides

### Current README.md
- Identify outdated sections
- Find missing features
- Check broken links
- Verify accuracy of examples and commands

## ⚠️ Critical Guidelines

### Accuracy Above All
1. **Verify status claims**: Don't trust `STATUS.md` blindly
   - Cross-reference with actual code in `src/`
   - Check test files to confirm feature implementation
   - Verify agent existence in `agents/` folder
2. **No vaporware**: Only document features that actually exist and work
3. **Honest status**: If something is partially implemented, say so

### Organization Principles
1. **User-first structure**: Start with getting started, then usage, then deep dives
2. **Progressive disclosure**: Basic info first, links to detailed docs for advanced topics
3. **Consistent formatting**: Use clear headings, maintain parallel structure
4. **Scannable content**: Use tables, lists, and visual hierarchy effectively

### Writing Style
1. **Concise but complete**: Every sentence must earn its place
2. **Active voice**: "Aura orchestrates agents" not "Agents are orchestrated by Aura"
3. **Present tense**: "Aura generates code" not "Aura will generate code"
4. **No marketing fluff**: Technical accuracy, not sales copy
5. **Practical examples**: Show, don't just tell

### Links and References
1. **Check all links**: Ensure they point to existing files
2. **Use relative paths**: `docs/GUIDE.md` not absolute URLs
3. **Keep links current**: Update if file locations changed
4. **Provide context**: "See the [Configuration Guide](docs/CONFIGURATION.md)" not just "[here](docs/CONFIGURATION.md)"

## 🚀 Execution Steps

1. **Inventory Phase** (Thorough)
   - Read through ALL source code projects in `src/`
   - Review ALL agent definitions in `agents/`
   - Check ALL documentation in `plan/` and `docs/`
   - Note discrepancies between `STATUS.md` and actual code
   - List all features, agents, and capabilities you find

2. **Verification Phase** (Critical)
   - For each claimed feature in `STATUS.md`, verify it exists in code
   - For each agent listed, confirm the implementation file exists
   - For each link in current README, check if target exists
   - Run `dotnet build` mentally (check dependencies, project structure)

3. **Writing Phase** (Structured)
   - Start with the Quick Start section (most important for users)
   - Build the Walkthrough section (practical value)
   - Document the Architecture section (design decisions)
   - Create the Feature Inventory (comprehensive but concise)
   - Write the Customization section (extensibility)
   - Finish with Contributing section (community building)

4. **Review Phase** (Quality Check)
   - Read through as if you're a new user
   - Check: Can someone get started in 10 minutes?
   - Check: Are all features documented?
   - Check: Are architectural decisions explained?
   - Check: Is customization accessible?
   - Check: Is contribution process clear?
   - Verify: All links work, all claims are accurate

## 📊 Quality Criteria

Your updated README is successful if:

- ✅ A new user can install and run their first workflow in < 15 minutes
- ✅ A developer understands the architecture in < 10 minutes of reading
- ✅ Every implemented feature is documented (even if briefly)
- ✅ A contributor knows exactly how to extend Aura with a custom agent
- ✅ No broken links, no outdated information, no vaporware
- ✅ The document is scannable (good use of headings, tables, lists)
- ✅ Technical accuracy is perfect (verified against actual code)
- ✅ The tone is professional, welcoming, and practical

## 🎯 Output Format

Update the `README.md` file directly. Structure it as follows:

```markdown
# Agent Orchestrator (Aura)

> **Tagline: One compelling sentence about what Aura does**

[Badges for build status, test coverage, .NET version, license]

## 🚀 Quick Start with Aura VS Code Extension
[Content as specified in section 1]

## 💡 Walkthrough: Using Aura to Accelerate Development
[Content as specified in section 2]

## 🏗️ Architecture: Design for Extensibility
[Content as specified in section 3]

## ✨ Features
[Content as specified in section 4 - organized by category]

## 🔌 Creating Custom Agents
[Content as specified in section 5]

## 🤝 Contributing
[Content as specified in section 6]

## 📄 License
[Existing license information]

---

**Built with ❤️ using .NET 10 and AI**
```

## 🎓 Remember

You are the README's maintainer, not its marketer. Your job is to accurately reflect the current state of the project, help users get value quickly, and welcome contributors. Accuracy and clarity are more important than completeness - better to document 80% perfectly than 100% poorly.

**Success = A README that makes Aura accessible, understandable, and extensible.**

Now go review the codebase and update that README! 🚀
