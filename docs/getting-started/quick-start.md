# Quick Start: Your First Workflow

This guide walks you through creating your first AI-assisted development workflow.

## What is a Workflow?

A workflow is an AI-assisted task that Aura helps you complete step by step. You describe what you want to do, and Aura:

1. **Analyzes** the request and your codebase
2. **Plans** the steps needed
3. **Executes** each step with your approval
4. **Creates** a PR when you're done

## Create a Workflow

### Step 1: Open the Workflows Panel

1. In VS Code, click the **Aura icon** in the Activity Bar
2. Click **"New Workflow"** or use `Ctrl+Shift+P` → "Aura: New Workflow"

### Step 2: Describe Your Task

Enter a description of what you want to accomplish:

```text
Add a /health endpoint to the API that returns the current version and uptime
```

Good descriptions include:

- **What** you want to build or change
- **Where** it should be (if relevant)
- **Any constraints** (e.g., "use existing patterns")

### Step 3: Review the Analysis

Aura will analyze your request and show:

- **Understanding** - What Aura thinks you want
- **Scope** - Files and components involved
- **Questions** - Clarifications if needed

Click **"Continue"** if the analysis looks correct.

### Step 4: Review the Plan

Aura creates a step-by-step plan:

```text
1. Create HealthController.cs with /health endpoint
2. Add HealthService for version and uptime tracking
3. Register service in Program.cs
4. Add unit tests for health endpoint
```

Each step shows:

- **Description** - What will be done
- **Files** - What files will be created or modified

Click **"Approve Plan"** to proceed.

### Step 5: Execute Steps

For each step, Aura:

1. Shows the proposed changes
2. Waits for your approval
3. Applies the changes on approval

You can:

- ✅ **Approve** - Apply the changes
- ✏️ **Edit** - Modify before applying
- ⏭️ **Skip** - Skip this step
- 🛑 **Stop** - Cancel the workflow

### Step 6: Finalize

When all steps are complete:

1. Click **"Finalize Workflow"**
2. Choose options:
   - Commit message
   - Create PR (if connected to GitHub)
3. Click **"Complete"**

## Example Workflow

Here's a complete example:

### Input

```text
Add user authentication with JWT tokens. Use the existing UserRepository 
for looking up users. Store the JWT secret in configuration.
```

### Generated Plan

```text
Step 1: Create JwtSettings class for configuration
Step 2: Add JwtSettings to appsettings.json
Step 3: Create JwtService for token generation/validation
Step 4: Create AuthController with /login endpoint
Step 5: Add JWT middleware to Program.cs
Step 6: Add [Authorize] to protected endpoints
Step 7: Add unit tests for JwtService
Step 8: Add integration tests for AuthController
```

### Result

- 8 files created/modified
- All tests passing
- Ready for PR

## Tips for Better Workflows

### Be Specific

```text
❌ "Add logging"
✅ "Add structured logging with Serilog to the OrderService, 
    logging order creation and payment events"
```

### Reference Existing Code

```text
✅ "Add a new endpoint following the same pattern as ProductController"
```

### Break Down Large Tasks

Instead of one massive workflow:

```text
❌ "Implement the entire user management system"
```

Create multiple focused workflows:

```text
✅ "Add user registration endpoint"
✅ "Add user login with JWT"
✅ "Add password reset functionality"
```

## Next Steps

- **[Workflows Deep Dive](../user-guide/workflows.md)** - Advanced workflow features
- **[Chat](../user-guide/chat.md)** - Ask questions about your code
- **[Troubleshooting](../troubleshooting/common-issues.md)** - If something goes wrong
