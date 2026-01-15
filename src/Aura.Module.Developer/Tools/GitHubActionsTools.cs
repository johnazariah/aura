// <copyright file="GitHubActionsTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using System.Text;
using Aura.Foundation.Tools;
using Aura.Module.Developer.GitHub;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides tools for GitHub Actions integration - list workflows, check run status, trigger builds, get logs.
/// </summary>
public static class GitHubActionsTools
{
    /// <summary>
    /// Registers GitHub Actions tools with the tool registry.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="gitHubService">The GitHub service.</param>
    /// <param name="loggerFactory">Logger factory for tool logging.</param>
    public static void Register(IToolRegistry registry, IGitHubService gitHubService, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("GitHubActionsTools");

        registry.RegisterTool(CreateListWorkflowsTool(gitHubService, logger));
        registry.RegisterTool(CreateListWorkflowRunsTool(gitHubService, logger));
        registry.RegisterTool(CreateGetWorkflowRunTool(gitHubService, logger));
        registry.RegisterTool(CreateTriggerWorkflowTool(gitHubService, logger));
        registry.RegisterTool(CreateRerunWorkflowTool(gitHubService, logger));
        registry.RegisterTool(CreateCancelWorkflowTool(gitHubService, logger));

        logger.LogInformation("Registered 6 GitHub Actions tools");
    }

    private static ToolDefinition CreateListWorkflowsTool(IGitHubService gitHubService, ILogger logger) => new()
    {
        ToolId = "github.list_workflows",
        Name = "List GitHub Workflows",
        Description = "Lists all GitHub Actions workflows defined in a repository. Returns workflow names, paths, and IDs.",
        Categories = ["github", "ci-cd"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "owner": {
                    "type": "string",
                    "description": "The repository owner (user or organization)"
                },
                "repo": {
                    "type": "string",
                    "description": "The repository name"
                }
            },
            "required": ["owner", "repo"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var owner = input.GetParameter<string>("owner")
                ?? throw new ArgumentException("owner required");
            var repo = input.GetParameter<string>("repo")
                ?? throw new ArgumentException("repo required");

            logger.LogInformation("Listing workflows for {Owner}/{Repo}", owner, repo);

            var workflows = await gitHubService.ListWorkflowsAsync(owner, repo, ct);

            var sb = new StringBuilder();
            sb.AppendLine($"# Workflows in {owner}/{repo}");
            sb.AppendLine();

            if (workflows.Count == 0)
            {
                sb.AppendLine("No workflows found.");
            }
            else
            {
                foreach (var wf in workflows)
                {
                    sb.AppendLine($"- **{wf.Name}** (ID: {wf.Id})");
                    sb.AppendLine($"  - Path: `{wf.Path}`");
                    sb.AppendLine($"  - State: {wf.State}");
                    sb.AppendLine();
                }
            }

            return new ToolResult
            {
                Success = true,
                Output = sb.ToString()
            };
        }
    };

    private static ToolDefinition CreateListWorkflowRunsTool(IGitHubService gitHubService, ILogger logger) => new()
    {
        ToolId = "github.list_workflow_runs",
        Name = "List Workflow Runs",
        Description = "Lists recent workflow runs for a repository. Can filter by workflow, branch, or status. Returns run status, conclusions, and links.",
        Categories = ["github", "ci-cd"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "owner": {
                    "type": "string",
                    "description": "The repository owner (user or organization)"
                },
                "repo": {
                    "type": "string",
                    "description": "The repository name"
                },
                "workflow": {
                    "type": "string",
                    "description": "Optional: Workflow file name (e.g., 'ci.yml') or ID to filter runs"
                },
                "branch": {
                    "type": "string",
                    "description": "Optional: Branch name to filter runs"
                },
                "status": {
                    "type": "string",
                    "enum": ["queued", "in_progress", "completed"],
                    "description": "Optional: Status to filter runs"
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum runs to return (default: 10, max: 30)"
                }
            },
            "required": ["owner", "repo"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var owner = input.GetParameter<string>("owner")
                ?? throw new ArgumentException("owner required");
            var repo = input.GetParameter<string>("repo")
                ?? throw new ArgumentException("repo required");
            var workflow = input.GetParameter<string?>("workflow");
            var branch = input.GetParameter<string?>("branch");
            var status = input.GetParameter<string?>("status");
            var limit = input.GetParameter("limit", 10);
            limit = Math.Min(limit, 30);

            logger.LogInformation("Listing workflow runs for {Owner}/{Repo}", owner, repo);

            var runs = await gitHubService.ListWorkflowRunsAsync(owner, repo, workflow, branch, status, limit, ct);

            var sb = new StringBuilder();
            sb.AppendLine($"# Recent Workflow Runs in {owner}/{repo}");
            if (!string.IsNullOrEmpty(workflow))
            {
                sb.AppendLine($"Workflow: {workflow}");
            }

            if (!string.IsNullOrEmpty(branch))
            {
                sb.AppendLine($"Branch: {branch}");
            }

            sb.AppendLine();

            if (runs.Count == 0)
            {
                sb.AppendLine("No workflow runs found matching the criteria.");
            }
            else
            {
                foreach (var run in runs)
                {
                    var icon = run.Conclusion switch
                    {
                        "success" => "✅",
                        "failure" => "❌",
                        "cancelled" => "🚫",
                        "skipped" => "⏭️",
                        _ => run.Status == "in_progress" ? "🔄" : "⏳"
                    };

                    sb.AppendLine($"## {icon} Run #{run.RunNumber} - {run.Name ?? "Unnamed"}");
                    sb.AppendLine($"- **Run ID**: {run.Id}");
                    sb.AppendLine($"- **Status**: {run.Status}");
                    sb.AppendLine($"- **Conclusion**: {run.Conclusion ?? "pending"}");
                    sb.AppendLine($"- **Branch**: {run.HeadBranch}");
                    sb.AppendLine($"- **Event**: {run.Event}");
                    sb.AppendLine($"- **Commit**: `{run.HeadSha[..Math.Min(7, run.HeadSha.Length)]}`");
                    sb.AppendLine($"- **Created**: {run.CreatedAt:u}");
                    if (!string.IsNullOrEmpty(run.HtmlUrl))
                    {
                        sb.AppendLine($"- **URL**: {run.HtmlUrl}");
                    }

                    sb.AppendLine();
                }
            }

            return new ToolResult
            {
                Success = true,
                Output = sb.ToString()
            };
        }
    };

    private static ToolDefinition CreateGetWorkflowRunTool(IGitHubService gitHubService, ILogger logger) => new()
    {
        ToolId = "github.get_workflow_run",
        Name = "Get Workflow Run Details",
        Description = "Gets detailed information about a specific workflow run including job statuses and step-by-step results. Useful for diagnosing failed builds.",
        Categories = ["github", "ci-cd"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "owner": {
                    "type": "string",
                    "description": "The repository owner (user or organization)"
                },
                "repo": {
                    "type": "string",
                    "description": "The repository name"
                },
                "run_id": {
                    "type": "integer",
                    "description": "The workflow run ID"
                }
            },
            "required": ["owner", "repo", "run_id"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var owner = input.GetParameter<string>("owner")
                ?? throw new ArgumentException("owner required");
            var repo = input.GetParameter<string>("repo")
                ?? throw new ArgumentException("repo required");
            var runId = input.GetParameter<long>("run_id");

            logger.LogInformation("Getting workflow run {RunId} for {Owner}/{Repo}", runId, owner, repo);

            // Get logs which includes job/step info
            var logs = await gitHubService.GetWorkflowRunLogsAsync(owner, repo, runId, ct);

            return new ToolResult
            {
                Success = true,
                Output = logs
            };
        }
    };

    private static ToolDefinition CreateTriggerWorkflowTool(IGitHubService gitHubService, ILogger logger) => new()
    {
        ToolId = "github.trigger_workflow",
        Name = "Trigger Workflow",
        Description = "Triggers a GitHub Actions workflow via workflow_dispatch event. The workflow must have 'workflow_dispatch' trigger configured.",
        Categories = ["github", "ci-cd"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "owner": {
                    "type": "string",
                    "description": "The repository owner (user or organization)"
                },
                "repo": {
                    "type": "string",
                    "description": "The repository name"
                },
                "workflow": {
                    "type": "string",
                    "description": "Workflow file name (e.g., 'ci.yml') or workflow ID"
                },
                "ref": {
                    "type": "string",
                    "description": "The git reference (branch or tag) to run the workflow on"
                },
                "inputs": {
                    "type": "object",
                    "description": "Optional: Key-value pairs of workflow inputs"
                }
            },
            "required": ["owner", "repo", "workflow", "ref"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var owner = input.GetParameter<string>("owner")
                ?? throw new ArgumentException("owner required");
            var repo = input.GetParameter<string>("repo")
                ?? throw new ArgumentException("repo required");
            var workflow = input.GetParameter<string>("workflow")
                ?? throw new ArgumentException("workflow required");
            var @ref = input.GetParameter<string>("ref")
                ?? throw new ArgumentException("ref required");

            Dictionary<string, string>? inputs = null;
            var inputsParam = input.GetParameter<IReadOnlyDictionary<string, object?>?>("inputs");
            if (inputsParam is { Count: > 0 })
            {
                inputs = [];
                foreach (var kvp in inputsParam)
                {
                    inputs[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                }
            }

            logger.LogInformation("Triggering workflow {Workflow} on {Ref} for {Owner}/{Repo}", workflow, @ref, owner, repo);

            await gitHubService.TriggerWorkflowAsync(owner, repo, workflow, @ref, inputs, ct);

            return new ToolResult
            {
                Success = true,
                Output = $"✅ Successfully triggered workflow `{workflow}` on `{@ref}` in {owner}/{repo}.\n\nNote: It may take a few seconds for the run to appear. Use `github.list_workflow_runs` to check status."
            };
        }
    };

    private static ToolDefinition CreateRerunWorkflowTool(IGitHubService gitHubService, ILogger logger) => new()
    {
        ToolId = "github.rerun_workflow",
        Name = "Re-run Workflow",
        Description = "Re-runs a failed or cancelled workflow run. Useful for retrying transient failures.",
        Categories = ["github", "ci-cd"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "owner": {
                    "type": "string",
                    "description": "The repository owner (user or organization)"
                },
                "repo": {
                    "type": "string",
                    "description": "The repository name"
                },
                "run_id": {
                    "type": "integer",
                    "description": "The workflow run ID to re-run"
                }
            },
            "required": ["owner", "repo", "run_id"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var owner = input.GetParameter<string>("owner")
                ?? throw new ArgumentException("owner required");
            var repo = input.GetParameter<string>("repo")
                ?? throw new ArgumentException("repo required");
            var runId = input.GetParameter<long>("run_id");

            logger.LogInformation("Re-running workflow {RunId} for {Owner}/{Repo}", runId, owner, repo);

            await gitHubService.RerunWorkflowAsync(owner, repo, runId, ct);

            return new ToolResult
            {
                Success = true,
                Output = $"✅ Successfully triggered re-run of workflow run {runId} in {owner}/{repo}.\n\nUse `github.list_workflow_runs` to check the new run status."
            };
        }
    };

    private static ToolDefinition CreateCancelWorkflowTool(IGitHubService gitHubService, ILogger logger) => new()
    {
        ToolId = "github.cancel_workflow",
        Name = "Cancel Workflow",
        Description = "Cancels an in-progress workflow run.",
        Categories = ["github", "ci-cd"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "owner": {
                    "type": "string",
                    "description": "The repository owner (user or organization)"
                },
                "repo": {
                    "type": "string",
                    "description": "The repository name"
                },
                "run_id": {
                    "type": "integer",
                    "description": "The workflow run ID to cancel"
                }
            },
            "required": ["owner", "repo", "run_id"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var owner = input.GetParameter<string>("owner")
                ?? throw new ArgumentException("owner required");
            var repo = input.GetParameter<string>("repo")
                ?? throw new ArgumentException("repo required");
            var runId = input.GetParameter<long>("run_id");

            logger.LogInformation("Cancelling workflow {RunId} for {Owner}/{Repo}", runId, owner, repo);

            await gitHubService.CancelWorkflowRunAsync(owner, repo, runId, ct);

            return new ToolResult
            {
                Success = true,
                Output = $"✅ Successfully cancelled workflow run {runId} in {owner}/{repo}."
            };
        }
    };
}
