# Speaker Notes

Detailed notes for each slide in the Aura presentation.

---

## Slide 1: Title

**Duration:** 30 seconds

- Welcome attendees
- Brief personal introduction if needed
- Set expectations: ~40 minutes plus Q&A

---

## Slide 2: Agenda

**Duration:** 1 minute

- Walk through the four parts
- Mention that there will be time for questions at the end
- Invite questions during the talk if preferred

---

## Slide 3: Part 1 Header

**Duration:** 10 seconds

- Transition slide, no notes needed

---

## Slide 4: The Problem with Cloud AI

**Duration:** 3 minutes

**Key Points:**

1. **Privacy Concerns**
   - "When you use GitHub Copilot, your code goes to Microsoft servers"
   - "For regulated industries—healthcare, finance, government—this can be a non-starter"
   - "Even for personal projects, do you want your code analyzed by third parties?"

2. **Dependency Issues**
   - "Raise your hand if you've ever had an AI assistant fail during crunch time"
   - "Rate limits hit when you need them least"
   - "At $0.01-0.10 per request, costs add up—especially for code generation which needs many iterations"

3. **Trust Deficit**
   - "We're asked to trust that our data is handled correctly"
   - "Terms of service can change—remember when OpenAI changed their data policy?"

**Transition:** "So what's the alternative?"

---

## Slide 5: The Vision

**Duration:** 2 minutes

**Key Points:**

- "Imagine having a full AI assistant that never phones home"
- "Windows Recall promised this but got it wrong—it captured everything including passwords"
- "Aura takes a different approach: we never capture anything without your action"

**Walk through the two columns:**
- Left: What users actually want (don't just read the bullet points—elaborate)
- Right: "And this is what Aura delivers—every component runs locally"

**Transition:** "Let's look at what that actually means..."

---

## Slide 6: The Privacy Promise

**Duration:** 2 minutes

**Key Points:**

- Point to the diagram: "Everything is on your machine"
- "No internet required—you can work on an airplane"
- "No telemetry—we don't even know you're using Aura"
- "No API keys needed for the default configuration"

**Emphasis:**
- "Cloud services like GitHub API or Azure OpenAI are explicitly opt-in"
- "You have to configure them yourself—they're not default"

**Anecdote opportunity:** "I actually run Aura on my work laptop with sensitive code..."

---

## Slide 7: Why Now?

**Duration:** 2 minutes

**Key Points:**

1. **Walk through the table:**
   - "Llama 3 and Qwen 2.5 Coder are genuinely good for coding tasks"
   - "Ollama made hosting trivial—one command to run any model"
   - "pgvector brought production-quality vector search to PostgreSQL"
   - "Aspire solved the 'but I don't want to learn Kubernetes' problem"

2. **The Sweet Spot:**
   - "7B models fit in 8GB VRAM—most gaming laptops can run this"
   - "Inference is fast enough that you're not waiting"
   - "For coding tasks specifically, quality is approaching cloud models"

**Transition:** "So let's see how we built this..."

---

## Slide 8: Part 2 Header

**Duration:** 10 seconds

- Transition slide

---

## Slide 9: High-Level Architecture

**Duration:** 3 minutes

**Walk through from top to bottom:**

1. **VS Code Extension**
   - "This is how users interact with Aura"
   - "Workflow management, chat, status monitoring"

2. **Aura API**
   - "REST API—could be consumed by any client"
   - "The extension is just one possible UI"

3. **Modules**
   - "Only Developer Module is complete today"
   - "Research and Personal are future work"
   - "Key point: modules are independent—you enable only what you need"

4. **Foundation**
   - "This is the shared infrastructure"
   - "Every module gets agents, LLM access, RAG, tools, database"

5. **Infrastructure**
   - "Ollama for LLM, PostgreSQL for data, filesystem for your code"
   - "All local, all under your control"

---

## Slide 10: Composable Modules

**Duration:** 2 minutes

**Key Points:**

- Show the configuration: "This is all you need to enable a module"
- "Want developer workflows? Enable developer. Don't need research? Don't enable it."

**Emphasize the key rule:**
- "Modules never depend on each other"
- "This is critical for maintainability and complexity management"
- "If Research needs something from Developer, that goes in Foundation"

---

## Slide 11: Foundation Layer

**Duration:** 2 minutes

**Quick walkthrough of the four quadrants:**

1. **Agents** - "Markdown files that define AI personalities"
2. **RAG** - "Semantic search over your code and documents"
3. **LLM Providers** - "Ollama default, cloud optional"
4. **Data** - "Entity Framework Core, repositories, transactions"

**Transition:** "Let's dig deeper into the RAG pipeline..."

---

## Slide 12: The RAG Pipeline

**Duration:** 2 minutes

**Walk through the flow:**

1. **Ingest** - "We scan your files—code, markdown, documentation"
2. **Embed** - "Ollama generates embeddings locally using nomic-embed-text"
3. **Store** - "Vectors go into PostgreSQL with pgvector"
4. **Query → Search → Retrieve** - "When you ask a question, we find semantically similar content"

**Mention:**
- "nomic-embed-text is 137M parameters—fast on any GPU"
- "HNSW index means approximate but very fast nearest neighbor search"

---

## Slide 13: Code Graph

**Duration:** 3 minutes

**Start with the problem:**
- "Vector RAG finds similar text, not related code"
- "If I ask 'what implements IWorkflowService', that's a graph traversal"

**Explain the solution:**
- "We build an entity-relationship graph of your code"
- "Nodes are things: solutions, projects, types, methods"
- "Edges are relationships: contains, inherits, implements, calls"

**Key technologies:**
- "TreeSitter for fast multi-language parsing"
- "Roslyn for deep C# analysis when needed"

---

## Slide 14: Markdown Agent Definitions

**Duration:** 2 minutes

**Show the example:**
- "This is a complete agent definition"
- "Metadata tells us what LLM to use"
- "Capabilities tag what this agent can do"
- "System Prompt is the personality"

**Benefits:**
- "Human-readable—a non-developer could modify this"
- "Hot-reloadable—drop a file, it becomes available"
- "Version-controllable—track changes in git"

---

## Slide 15: Part 3 Header

**Duration:** 10 seconds

- "Now let's see how this works in practice"

---

## Slide 16: The Developer Workflow

**Duration:** 3 minutes

**Walk through each phase:**

1. **CREATE** - "User describes what they want to accomplish"
2. **ANALYZE** - "Agent enriches with RAG context, structures requirements"
3. **PLAN** - "Business analyst agent breaks into steps"
4. **EXECUTE** - "One step at a time, human reviews each"
5. **COMPLETE** - "Commit, push, create PR"

**Emphasize:**
- "This is all local—no GitHub sync required"
- "Git worktrees isolate each workflow"

---

## Slide 17: Step Types and Capabilities

**Duration:** 2 minutes

**Walk through the table:**
- "Each step specifies what capability is needed"
- "System matches to available agents"
- "User can always reassign if they prefer a different agent"

**Example:**
- "If a step needs C# coding, we find the C# Coding Agent"
- "If you want to use the Rust agent instead, you can reassign"

---

## Slide 18: Human-in-the-Loop

**Duration:** 3 minutes

**Philosophy quote:**
- "The user orchestrates. Aura executes."
- "This is intentional—we're not trying to replace developers"

**Why not full automation?**
- "LLMs make mistakes—anyone who's used Copilot knows this"
- "Context matters—the LLM doesn't know your deadline or constraints"
- "Users know best—they can catch issues early"

**Walk through actions:**
- "Approve moves forward"
- "Reject gives feedback for retry"
- "Chat lets you have a conversation"
- "Reassign changes the agent"

---

## Slide 19: Demo: VS Code Extension

**Duration:** 3 minutes

**Option A: Live Demo**
- If doing a live demo, switch to VS Code here
- Show workflow tree, create a workflow, walk through steps

**Option B: Screenshots**
- Walk through the UI components
- Explain each panel's purpose

**Key points to demonstrate:**
- Creating a workflow from VS Code
- Seeing the analysis results
- Stepping through execution
- Approving/rejecting outputs

---

## Slide 20: Part 4 Header

**Duration:** 10 seconds

- "Now let's look under the hood"

---

## Slide 21: Project Structure

**Duration:** 2 minutes

**Walk through the directories:**

1. **Aura.Foundation** - "Core infrastructure everyone uses"
2. **Aura.Module.Developer** - "The developer vertical"
3. **Aura.Api** - "All endpoints in one file"
4. **Aura.AppHost** - "Aspire orchestration"

**Also mention:**
- "extension/ is TypeScript for VS Code"
- "agents/ is where agent definitions live"
- "prompts/ is Handlebars templates"

---

## Slide 22: Agent Execution Flow

**Duration:** 2 minutes

**Walk through the diagram:**

1. "Agent definition provides the system prompt—who the agent is"
2. "RAG context is injected automatically—relevant code snippets"
3. "Prompt template tells the agent what to do—Handlebars format"
4. "All goes to the LLM provider"

**Key insight:**
- "RAG injection is automatic—agents don't have to request it"
- "ConfigurableAgent handles this transparently"

---

## Slide 23: ReAct Tool Execution

**Duration:** 3 minutes

**Explain the loop:**
- "Agent gets task plus list of available tools"
- "THINK: Agent reasons about what to do"
- "ACT: Agent calls a tool"
- "OBSERVE: System returns result"
- "Loop continues until agent says DONE"

**Why ReAct over function calling?**
- "Model-agnostic—works with any Ollama model"
- "Debuggable—you can see the agent's reasoning"
- "Flexible—agent can adapt to unexpected situations"

---

## Slide 24: Code Graph with TreeSitter

**Duration:** 2 minutes

**Show the YAML config:**
- "Each language has queries defined in YAML"
- "TreeSitter parses the code, queries extract structure"

**Supported languages:**
- "C#, TypeScript, Python, Rust, Go, F#, Haskell, Elm..."
- "Adding a new language is just adding a YAML file"

**API queries:**
- "Find by name, find implementations, find callers, get members"

---

## Slide 25: API Design

**Duration:** 2 minutes

**Show the endpoints:**
- "All in Program.cs—about 2000 lines"
- "Controversial choice: single file for all endpoints"

**Why single file?**
- "Easy to find—no hunting through controllers"
- "At this scale, it's manageable"
- "Would split if it grows much larger"

---

## Slide 26: Key Technologies

**Duration:** 1 minute

**Quick walkthrough of the table:**
- Point out the key ones: .NET 9, Aspire, pgvector, TreeSitter, Ollama

**Test coverage:**
- "400+ unit tests"
- "Integration tests with real PostgreSQL"

---

## Slide 27: What's Next

**Duration:** 2 minutes

**Near-term:**
- "MCP integration is exciting—Model Context Protocol"
- "macOS native support for broader reach"

**Future modules:**
- "Research for paper management"
- "Personal for general assistant work"

**Community:**
- "MIT License—truly open source"
- "Contributions welcome"

---

## Slide 28: Questions

**Duration:** 5 minutes

**Prepared for common questions:**

1. "How does it compare to GitHub Copilot?"
   - "Different philosophy—privacy first, you control everything"
   - "Copilot is cloud-only, Aura is local-first"

2. "What about model quality?"
   - "7B models are good for coding tasks"
   - "You can use Azure OpenAI if you want cloud models"

3. "What about Windows Recall?"
   - "We're capture-on-demand, not continuous"
   - "You explicitly index what you want"

4. "Can I use it at work?"
   - "Check your company policy, but since it's local-only..."

---

## Slide 29: Thank You

**Duration:** 30 seconds

- Thank the audience
- Remind them of the GitHub URL
- Offer to stay for individual questions

---

## General Tips

### If the demo fails:
- Have screenshots ready as backup
- "Let me show you what this would look like..."

### If time is running short:
- Skip slides 22-25 (technical deep dive)
- Go straight from demo to "What's Next"

### If audience is non-technical:
- Spend more time on Motivations
- Less time on ReAct and TreeSitter details
- Focus on the user experience

### If audience is highly technical:
- Dive deeper into architecture
- Discuss the trade-offs in ADRs
- Talk about the RAG pipeline in detail
