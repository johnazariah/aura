# AURA Agent Development Guide - Creating Custom Agents

You are tasked with creating comprehensive documentation for developers who want to extend Aura by creating custom agents. This is one of Aura's most powerful features and deserves excellent documentation with clear examples.

## 🎯 Your Mission

Create detailed, practical documentation in `docs/agent-development/` that teaches developers how to create custom agents using either **markdown prompts** (simple, no code) or **C# implementations** (full control).

## 🔑 Critical Context

Aura supports **two approaches** to agent development:

### 1. Prompt-Based Agents (Markdown Files)
- **Simplest approach**: No coding required
- Agent defined in `.md` file with frontmatter + prompt
- Frontmatter specifies provider, model, temperature, capabilities
- Loaded dynamically by `DynamicAgentRegistry`
- Executed via `ConfigurableAgent` which uses `ILlmProviderRegistry`
- Hot-reload capable (file changes detected automatically)
- **Perfect for**: Business analysts, quick prototypes, prompt engineering

### 2. Code-Based Agents (C# Classes)
- **Full control**: Implement `IAgentExecutor` interface
- Can use any .NET libraries, complex logic, external services
- Can be registered directly (DI) or via plugin system
- Plugin approach enables hot-reload
- **Perfect for**: Complex workflows, integrations, performance-critical agents

Both approaches integrate seamlessly into the same orchestration system.

## 📋 Required Documentation Structure

### 1. Overview (`index.md`)
- What are agents in Aura?
- Why create custom agents?
- Two approaches: prompt-based vs code-based
- Decision guide: Which approach should I use?
- What you'll need (prerequisites)
- Quick comparison table

### 2. Quick Start (`quick-start.md`)
- **Fastest path**: Create a markdown agent in 5 minutes
- Complete walkthrough with example
- Testing the agent
- Seeing it in action
- Next steps

### 3. Markdown Agents Section (`markdown-agents/`)

#### `markdown-agents/index.md`
- Overview of prompt-based agents
- When to use this approach
- How they work (DynamicAgentRegistry → ConfigurableAgent → LLM)
- Limitations and capabilities
- File location (`agents/` directory)
- Hot-reload development workflow

#### `markdown-agents/anatomy.md`
- Structure of an agent markdown file
- Frontmatter section (YAML)
- Prompt section (Markdown)
- System vs user prompts
- Template variables and how they're replaced
- Complete annotated example

#### `markdown-agents/frontmatter.md`
- All frontmatter properties explained:
  - `id` (required, unique identifier)
  - `name` (required, display name)
  - `description` (required, what the agent does)
  - `provider` (optional, default: "ollama")
  - `model` (optional, default: "qwen2.5-coder:7b")
  - `temperature` (optional, default: 0.7)
  - `capabilities` (array of strings)
  - `priority` (execution order)
  - `enabled` (bool, default: true)
- Examples of each configuration
- How provider selection works (local vs remote LLMs)

#### `markdown-agents/prompts.md`
- Crafting effective prompts
- Prompt engineering best practices for agents
- Using context from previous agents
- Accessing workflow data
- Error handling in prompts
- Output format guidance
- Examples: Good vs bad prompts
- Iterating and testing prompts

#### `markdown-agents/examples.md`
- **Complete examples** of markdown agents:
  1. Simple task breakdown agent
  2. Code review agent
  3. Security analysis agent
  4. Documentation generator
  5. API design agent
- Each example includes:
  - Use case description
  - Complete markdown file
  - Example input/output
  - Explanation of design choices

### 4. Code-Based Agents Section (`coded-agents/`)

#### `coded-agents/index.md`
- Overview of code-based agents
- When to use this approach (complex logic, external APIs, performance)
- How they work (IAgentExecutor interface)
- Direct registration vs plugin system
- Development workflow

#### `coded-agents/interface.md`
- `IAgentExecutor` interface explained
- `AgentType` property (unique identifier)
- `ExecuteAsync` method signature
- `AgentContext` parameter (workflow data, correlation IDs)
- `AgentResult` return type (success, output, error)
- Cancellation token support
- **Complete interface with XML comments**

#### `coded-agents/implementation.md`
- Step-by-step: Implementing your first agent
- Class structure and naming conventions
- Using dependency injection (services, LLM providers, repositories)
- Accessing workflow context
- Calling other agents (if needed)
- Error handling with Result<T> pattern
- Logging and observability
- **Complete working example**: ValidationAgent implementation

#### `coded-agents/testing.md`
- Unit testing agents
- Test project structure
- Mocking dependencies (NSubstitute)
- Testing success and failure paths
- Testing cancellation
- Integration testing with actual LLM (optional)
- **Complete test suite example**

#### `coded-agents/examples.md`
- **Complete examples** of code-based agents:
  1. Custom validation agent (uses Roslyn)
  2. External API integration agent (calls REST API)
  3. Database migration agent (generates EF migrations)
  4. Performance analysis agent (benchmarks code)
- Each example includes:
  - Use case
  - Complete C# code
  - Dependencies required
  - Registration code
  - Unit tests
  - Usage example

### 5. Advanced Topics

#### `plugin-system.md`
- Plugin architecture overview
- Creating an agent plugin
- `IAgentPlugin` interface
- Plugin metadata (version, dependencies)
- Folder structure for plugins
- Building and packaging
- Deploying plugins
- **Complete plugin example** with project structure

#### `hot-reload.md`
- How hot-reload works in Aura
- Markdown agents: Automatic file watching
- Code plugins: Reloading assemblies
- Development workflow with hot-reload
- Debugging during hot-reload
- Limitations and edge cases

#### `provider-selection.md`
- Multi-provider LLM system overview
- `ILlmProvider` abstraction
- Available providers (OLLAMA, future: MAF/GPT-4)
- Choosing provider per agent (frontmatter or constructor)
- Fallback logic
- Model selection strategies
- Cost vs performance considerations
- **Examples**: Cheap agent for simple tasks, expensive for complex

#### `deployment.md`
- Deploying markdown agents (copy to `agents/` folder)
- Deploying code agents (DI registration)
- Deploying plugin agents (copy to `plugins/` folder)
- Configuration in production
- Versioning strategies
- Updating agents without downtime (hot-reload)

## 🔍 What to Review

### Source Code (CRITICAL)
- `agents/*.md` - Examine ALL existing agent definitions
- `src/AgentOrchestrator.Agents/` - Examine ALL agent implementations
- `src/AgentOrchestrator.Agents/DynamicAgentRegistry.cs` - How markdown agents load
- `src/AgentOrchestrator.Agents/ConfigurableAgent.cs` - How markdown agents execute
- `src/AgentOrchestrator.Providers/` - LLM provider system
- `src/AgentOrchestrator.Workflow/Interfaces/IAgentExecutor.cs` - The interface
- `tests/AgentOrchestrator.Agents.Tests/` - Agent tests for patterns

### Documentation
- `docs/MULTI-PROVIDER-AGENTS.md` - Provider system details
- `docs/PLUGIN_SYSTEM.md` - Plugin architecture
- Current agent definitions for real-world examples

## ✍️ Writing Guidelines

### Audience
- **Primary**: Developers who want to extend Aura
- **Secondary**: Prompt engineers, business analysts (for markdown agents)
- **Assume**: Comfortable with C# and .NET (for code-based agents)
- **Assume**: Basic understanding of LLMs and prompts (for markdown agents)

### Style
1. **Tutorial-driven**: Every concept demonstrated with code
2. **Complete examples**: No "TODO" or "..." in examples
3. **Copy-paste ready**: Code examples should work as-is
4. **Progressive complexity**: Simple examples first, advanced later
5. **Troubleshooting**: Anticipate and address common errors

### Code Examples
- Use **real, complete code** - no pseudo-code
- Include necessary `using` statements
- Show file paths and project structure
- Highlight key lines with comments
- Include both success and error handling

### Structure Every Page
```markdown
# Page Title

> **What You'll Learn**: Brief overview

## Prerequisites
- What reader needs to know/have

## Main Content
[Concepts with examples]

## Complete Example
[Full, working example]

## Common Pitfalls
- Error X: How to fix it
- Error Y: How to fix it

## Next Steps
- What to read next
```

## 📊 Quality Criteria

Your agent development guide is successful if:

- ✅ A developer can create and test a markdown agent in 10 minutes
- ✅ A developer can implement a code-based agent in 30 minutes
- ✅ All examples compile and run successfully
- ✅ Decision guide clearly explains when to use each approach
- ✅ Frontmatter properties are all documented with examples
- ✅ Prompt engineering best practices are clear and actionable
- ✅ IAgentExecutor interface is fully explained
- ✅ Plugin system is accessible to advanced users
- ✅ Hot-reload workflow is clear and practical
- ✅ Multi-provider selection is explained with cost/performance tradeoffs
- ✅ Testing guidance includes complete test examples
- ✅ Common errors are addressed proactively

## 🎯 Real Agent Examples to Document

Make sure to include these real agents from the codebase as examples:

**Markdown Agents** (from `agents/` folder):
- Business Analyst Agent
- Coding Agent
- Testing Agent
- Documentation Agent
- Orchestration Agent
- Validation Agent
- PR Health Monitor Agent

**Code-Based Agents** (from `src/AgentOrchestrator.Agents/`):
- Any custom implementations
- Example validation agent
- Any integration agents

## 🚀 Execution Priority

1. `index.md` - Overview and decision guide
2. `quick-start.md` - Get them successful fast
3. `markdown-agents/anatomy.md` - Core understanding
4. `markdown-agents/examples.md` - Practical reference
5. `coded-agents/interface.md` - Foundation for code
6. `coded-agents/implementation.md` - Practical guide
7. `coded-agents/examples.md` - Real implementations
8. Rest of advanced topics

## 🎓 Remember

Agent development is Aura's superpower. Developers who master this can automate their entire workflow exactly how they want. Your documentation should make them feel empowered and excited about the possibilities.

**Show, don't just tell.** Every concept should be backed by a complete, working example that they can copy, run, and learn from.

Now go create documentation that turns users into agent developers! 🚀
