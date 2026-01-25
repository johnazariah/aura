# Working with Workflows

Workflows (also called **Stories**) are the core way to accomplish development tasks with Aura. This guide covers all workflow features including Stories, Patterns, and Verification.

## Ways to Start a Workflow

### 1. From a GitHub Issue (Recommended)

The easiest way to start a workflow is from a GitHub issue:

1. In VS Code, run **Command Palette → "Aura: Start Story from Issue"**
2. Paste the GitHub issue URL (e.g., `https://github.com/org/repo/issues/42`)
3. Aura automatically:
   - Creates a git worktree with an isolated branch
   - Opens a new VS Code window in that worktree
   - Binds the issue context to your workflow

This approach gives you:
- **Isolated work environment** - Changes stay in the worktree
- **Issue context** - The agent knows what you're trying to accomplish
- **Easy PR creation** - When done, create a PR back to the issue

### 2. From the Aura Panel

1. Click **Aura** in the Activity Bar
2. Click **"+ New Workflow"**
3. Enter your task description
4. Click **"Create"**

### 3. From Command Palette

1. Press `Ctrl+Shift+P`
2. Type "Aura: New Workflow"
3. Enter description in the input box

---

## Workflow Lifecycle

Every workflow goes through these stages:

```text
Created → Analyzing → Analyzed → Planning → Planned → Executing → Verifying → Completed/Failed
```

| Stage | Description |
|-------|-------------|
| **Created** | Workflow initialized with your description |
| **Analyzing** | Aura examines your codebase and request |
| **Analyzed** | Analysis complete, ready for planning |
| **Planning** | Aura creates step-by-step plan |
| **Planned** | Plan ready for your approval |
| **Executing** | Steps being executed with your approval |
| **Verifying** | Aura runs verification checks (build, tests, lint) |
| **Completed** | All steps done and verified, ready to finalize |
| **Failed** | Something went wrong (can be retried) |

## Creating Workflows

### From VS Code

1. Click **Aura** in the Activity Bar
2. Click **"+ New Workflow"**
3. Enter your task description
4. Click **"Create"**

### From Command Palette

1. Press `Ctrl+Shift+P`
2. Type "Aura: New Workflow"
3. Enter description in the input box

### Tips for Good Descriptions

**Include context:**

```text
Add a caching layer to the ProductService using Redis. 
Cache product lookups for 5 minutes.
```

**Reference existing patterns:**

```text
Create a new CustomerController following the same 
structure as OrderController.
```

**Specify constraints:**

```text
Add pagination to the /api/users endpoint. 
Use cursor-based pagination, not offset.
```

---

## Operational Patterns

Patterns are step-by-step playbooks for complex operations. When you encounter a multi-step task, Aura can load and follow a pattern.

### Available Patterns

| Pattern | Description |
|---------|-------------|
| `comprehensive-rename` | Rename a domain concept across the entire codebase |
| `generate-tests` | Generate comprehensive tests for a class |

### Pattern-Driven Stories

When creating a Story from a GitHub issue, you can bind an operational pattern:

1. Create Story from issue
2. In the Story panel, click **"Attach Pattern"**
3. Select the relevant pattern
4. The agent will follow the pattern's steps

This is particularly useful for:
- **Renames** - Patterns ensure all references, files, and tests are updated
- **Test generation** - Patterns include language-specific best practices
- **Refactoring** - Patterns guide blast radius analysis

### Loading Patterns in Chat

Ask the agent to load a pattern:

```
"Load the comprehensive-rename pattern and rename 
User to Customer across the codebase"
```

The agent will:
1. Load the pattern steps
2. Follow each step in order
3. Verify results after each step

---

## Verification Stage

Before completing a workflow, Aura runs verification checks to ensure your changes work correctly.

### Automatic Project Detection

Aura detects your project type and runs appropriate checks:

| Project Type | Verification Steps |
|--------------|-------------------|
| **.NET (C#/F#)** | `dotnet build`, `dotnet test` |
| **Node.js** | `npm run build`, `npm test` (if scripts exist) |
| **Python** | `pytest` (if pytest installed) |
| **Go** | `go build`, `go test` |
| **Rust** | `cargo build`, `cargo test` |

### Verification Results

After verification runs, you'll see:

```text
✅ Build passed (2.3s)
✅ Tests passed - 47/47 (5.1s)
```

Or if there are issues:

```text
✅ Build passed (2.3s)
❌ Tests failed - 45/47 (5.1s)
   → UserServiceTests.GetById_ReturnsUser failed
   → OrderServiceTests.Create_ValidOrder failed
```

### Handling Verification Failures

When verification fails:

1. **Review the errors** - Click on failed checks to see details
2. **Ask the agent to fix** - "Fix the failing tests"
3. **Re-run verification** - Aura will verify again after fixes
4. **Skip if needed** - You can finalize despite failures (with confirmation)

### Manual Verification

You can trigger verification manually:

- **From UI**: Click **"Run Verification"** in the workflow panel
- **From chat**: "Verify the current changes"
- **From command palette**: "Aura: Verify Workflow"

---

## Understanding Steps

Each workflow is broken into steps. Steps can be:

| Type | Description |
|------|-------------|

| **Create** | Create a new file |
| **Modify** | Edit an existing file |
| **Delete** | Remove a file |
| **Command** | Run a shell command |

### Step States

| State | Icon | Meaning |
|-------|------|---------|

| **Pending** | ⏳ | Waiting to be executed |
| **Executing** | 🔄 | Currently running |
| **Completed** | ✅ | Successfully done |
| **Failed** | ❌ | Error occurred |
| **Skipped** | ⏭️ | User chose to skip |

## Reviewing Changes

Before approving a step, review the proposed changes:

### For New Files

- Full file content shown
- Syntax highlighted

### For Modifications

- Diff view showing additions/removals
- Line-by-line changes

### Making Edits

If a step isn't quite right:

1. Click **"Edit"** instead of "Approve"
2. Modify the code in the editor
3. Click **"Apply Edits"**

The edited version will be used instead.

## Working with Git

Workflows create changes in an isolated git worktree:

```text
your-repo/
├── .worktrees/
│   └── workflow-abc123/    # Isolated workspace
│       └── ... changes ...
└── ... main repo ...
```

This means:

- ✅ Your main branch stays clean
- ✅ You can work on other things while a workflow runs
- ✅ Easy to discard if something goes wrong

## Finalizing a Workflow

When all steps are complete:

### Option 1: Commit & Push

1. Click **"Finalize"**
2. Enter a commit message
3. Check **"Push to remote"**
4. Click **"Complete"**

### Option 2: Create Pull Request

1. Click **"Finalize"**
2. Check **"Create Pull Request"**
3. Enter PR title and description
4. Click **"Complete"**

Aura will:

- Commit all changes
- Push to a new branch
- Open PR creation page (if configured)

### Option 3: Discard

If you don't want to keep the changes:

1. Click **"Discard Workflow"**
2. Confirm deletion

The worktree and all changes will be removed.

## Managing Workflows

### View All Workflows

The Aura panel shows all workflows:

- **Active** - In progress
- **Recent** - Completed in last 7 days

### Resume a Workflow

If VS Code closes or you switch tasks:

1. Open Aura panel
2. Find your workflow in the list
3. Click to resume

### Delete Old Workflows

1. Right-click a workflow
2. Select **"Delete"**

Or clean up all old workflows:

1. Command Palette → "Aura: Clean Up Workflows"

## Troubleshooting Workflows

### Workflow Stuck

If a workflow isn't progressing:

1. Check the status in Aura panel
2. Look for error messages
3. Try **"Retry Step"** for failed steps

### Wrong Changes Generated

If Aura misunderstood your intent:

1. **Edit** the step before approving
2. Or **Skip** and manually make changes
3. Consider restarting with a clearer description

### Git Conflicts

If changes conflict with your main branch:

1. Finalize the workflow anyway
2. Resolve conflicts in the PR
3. Or discard and rebase first

## Best Practices

1. **One task per workflow** - Keep focused
2. **Review every step** - Don't blindly approve
3. **Test as you go** - Run tests after each step
4. **Commit often** - Don't let changes pile up
5. **Learn from edits** - If you often edit steps, improve your descriptions
