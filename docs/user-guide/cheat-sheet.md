# Aura Developer Cheat Sheet

Quick reference for common Aura tasks.

## Workflows (Multi-step Development Tasks)

### Create a Workflow
1. Click **+ New Workflow** in Aura panel
2. Enter description → **Create**
3. Wait for analysis and planning
4. Review and approve each step

### Good Workflow Prompts

| Task | Example Prompt |
|------|----------------|
| New endpoint | `Create POST /api/products endpoint with validation, following OrderController patterns` |
| Add feature | `Add user favorites feature: model, repository, service, and API endpoints` |
| Write tests | `Add unit tests for OrderService covering all public methods using xUnit` |
| Refactor | `Extract email logic from UserService into EmailNotificationService` |
| Bug fix | `Fix: validation allows negative quantities. Add check and tests.` |
| Documentation | `Add XML docs to all public methods in PaymentController` |

### Step Actions

| Action | When to Use |
|--------|-------------|
| **Approve** | Step output looks correct |
| **Reject** | Step output is wrong, let agent retry |
| **Skip** | Step not needed |
| **Chat** | Ask agent to modify or explain |
| **Reassign** | Use a different agent for this step |

---

## Chat (Code-Aware Q&A)

### Open Chat
- Click **Chat** tab in Aura panel
- Or: `Ctrl+Shift+P` → "Aura: Open Chat"

### Useful Questions

| Goal | Question |
|------|----------|
| Understand code | `How does authentication work in this project?` |
| Find code | `Where is payment processing implemented?` |
| Trace flow | `What happens when a user places an order?` |
| Find patterns | `How is logging done in this project?` |
| Security review | `Are there SQL injection risks in UserRepository?` |
| Get overview | `Give me a high-level overview of this codebase` |

---

## Code Indexing

### Index Your Project
1. Open project folder in VS Code
2. Aura panel → **Code Graph** section
3. Click **Index Repository**

### Re-index When
- After pulling significant changes
- After adding new files/directories
- After changing project structure

---

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Command Palette | `Ctrl+Shift+P` |
| New Workflow | `Ctrl+Shift+P` → "Aura: New Workflow" |
| Open Chat | `Ctrl+Shift+P` → "Aura: Open Chat" |
| Focus Aura Panel | Click Aura icon in Activity Bar |

---

## Workflow Lifecycle

```
Created → Analyzing → Analyzed → Planning → Planned → Executing → Completed
              ↓           ↓          ↓          ↓           ↓
          [RAG search] [agent    [step-by-  [you        [all steps
           for context] enriches] step plan] approve]    done]
```

---

## Tips for Best Results

### Workflows
- ✅ Reference existing code: "following UserController patterns"
- ✅ Specify frameworks: "using FluentValidation"
- ✅ Mention constraints: "async/await throughout"
- ✅ Review each step before approving
- ❌ Don't be too vague: "make it better"

### Chat
- ✅ Index your project first
- ✅ Ask specific questions
- ✅ Use follow-up questions
- ✅ Reference files when relevant
- ❌ Don't assume chat knows unindexed code

---

## Available Agents

| Agent | Capability | Use For |
|-------|------------|---------|
| Coding Agent | coding, testing | Writing/editing code |
| Documentation Agent | documentation | READMEs, comments |
| Code Review Agent | code-review | Quality feedback |
| Build Fixer | build-fixing | Fixing compile errors |
| C# Specialist | csharp | Roslyn-powered C# work |
| Language Specialists | per-language | Python, TypeScript, Go, etc. |

---

## Need Help?

- **Docs**: [docs/README.md](../README.md)
- **Troubleshooting**: [troubleshooting/common-issues.md](../troubleshooting/common-issues.md)
- **Configuration**: [configuration/settings.md](../configuration/settings.md)
