# Working with Workflows

Workflows are the core way to accomplish development tasks with Aura. This guide covers all workflow features.

## Workflow Lifecycle

Every workflow goes through these stages:

```text
Created → Analyzing → Analyzed → Planning → Planned → Executing → Completed/Failed
```

| Stage | Description |
|-------|-------------|

| **Created** | Workflow initialized with your description |
| **Analyzing** | Aura examines your codebase and request |
| **Analyzed** | Analysis complete, ready for planning |
| **Planning** | Aura creates step-by-step plan |
| **Planned** | Plan ready for your approval |
| **Executing** | Steps being executed with your approval |
| **Completed** | All steps done, ready to finalize |
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
