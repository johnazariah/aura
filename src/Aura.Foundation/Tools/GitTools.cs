// <copyright file="GitTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools;

using System.Text;
using Aura.Foundation.Git;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registers git tools for source control operations.
/// </summary>
public static class GitTools
{
    /// <summary>
    /// Registers all git tools with the tool registry.
    /// </summary>
    public static void RegisterGitTools(IToolRegistry registry, IGitService gitService, ILogger logger)
    {
        // git.status - Get repository status
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "git.status",
            Name = "Git Status",
            Description = "Get the current status of the git repository including branch, modified files, and staged changes.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the repository (defaults to working directory)" }
                    },
                    "required": []
                }
                """,
            Categories = ["git"],
            Handler = async (input, ct) =>
            {
                var path = input.GetParameter<string?>("path") ?? input.WorkingDirectory;
                if (string.IsNullOrEmpty(path))
                {
                    return ToolResult.Fail("No repository path specified and no working directory set");
                }

                var result = await gitService.GetStatusAsync(path, ct);
                if (!result.Success)
                {
                    return ToolResult.Fail(result.Error ?? "Failed to get git status");
                }

                var status = result.Value!;
                var sb = new StringBuilder();
                sb.AppendLine($"Branch: {status.CurrentBranch}");
                sb.AppendLine($"Clean: {!status.IsDirty}");

                if (status.StagedFiles.Count > 0)
                {
                    sb.AppendLine($"\nStaged files ({status.StagedFiles.Count}):");
                    foreach (var file in status.StagedFiles)
                    {
                        sb.AppendLine($"  + {file}");
                    }
                }

                if (status.ModifiedFiles.Count > 0)
                {
                    sb.AppendLine($"\nModified files ({status.ModifiedFiles.Count}):");
                    foreach (var file in status.ModifiedFiles)
                    {
                        sb.AppendLine($"  M {file}");
                    }
                }

                if (status.UntrackedFiles.Count > 0)
                {
                    sb.AppendLine($"\nUntracked files ({status.UntrackedFiles.Count}):");
                    foreach (var file in status.UntrackedFiles)
                    {
                        sb.AppendLine($"  ? {file}");
                    }
                }

                return ToolResult.Ok(sb.ToString().Trim());
            }
        });

        // git.commit - Stage all changes and commit
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "git.commit",
            Name = "Git Commit",
            Description = "Stage all changes and create a commit with the specified message.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "message": { "type": "string", "description": "The commit message" },
                        "path": { "type": "string", "description": "Path to the repository (defaults to working directory)" }
                    },
                    "required": ["message"]
                }
                """,
            Categories = ["git"],
            Handler = async (input, ct) =>
            {
                var message = input.GetRequiredParameter<string>("message");
                var path = input.GetParameter<string?>("path") ?? input.WorkingDirectory;

                if (string.IsNullOrEmpty(path))
                {
                    return ToolResult.Fail("No repository path specified and no working directory set");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    return ToolResult.Fail("Commit message cannot be empty");
                }

                var result = await gitService.CommitAsync(path, message, ct);
                if (!result.Success)
                {
                    return ToolResult.Fail(result.Error ?? "Failed to commit");
                }

                return ToolResult.Ok($"Committed: {result.Value}");
            }
        });

        // git.branch - Get current branch or create a new one
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "git.branch",
            Name = "Git Branch",
            Description = "Get the current branch name, or create and checkout a new branch.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string", "description": "Name of branch to create (omit to just get current branch)" },
                        "baseBranch": { "type": "string", "description": "Base branch to create from (defaults to current branch)" },
                        "path": { "type": "string", "description": "Path to the repository (defaults to working directory)" }
                    },
                    "required": []
                }
                """,
            Categories = ["git"],
            Handler = async (input, ct) =>
            {
                var name = input.GetParameter<string?>("name");
                var baseBranch = input.GetParameter<string?>("baseBranch");
                var path = input.GetParameter<string?>("path") ?? input.WorkingDirectory;

                if (string.IsNullOrEmpty(path))
                {
                    return ToolResult.Fail("No repository path specified and no working directory set");
                }

                if (string.IsNullOrEmpty(name))
                {
                    // Just get current branch
                    var branchResult = await gitService.GetCurrentBranchAsync(path, ct);
                    if (!branchResult.Success)
                    {
                        return ToolResult.Fail(branchResult.Error ?? "Failed to get current branch");
                    }
                    return ToolResult.Ok($"Current branch: {branchResult.Value}");
                }
                else
                {
                    // Create and checkout new branch
                    var createResult = await gitService.CreateBranchAsync(path, name, baseBranch, ct);
                    if (!createResult.Success)
                    {
                        return ToolResult.Fail(createResult.Error ?? "Failed to create branch");
                    }
                    return ToolResult.Ok($"Created and checked out branch: {createResult.Value!.Name}");
                }
            }
        });

        // git.checkout - Switch to an existing branch
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "git.checkout",
            Name = "Git Checkout",
            Description = "Switch to an existing branch.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "branch": { "type": "string", "description": "Name of the branch to checkout" },
                        "path": { "type": "string", "description": "Path to the repository (defaults to working directory)" }
                    },
                    "required": ["branch"]
                }
                """,
            Categories = ["git"],
            Handler = async (input, ct) =>
            {
                var branch = input.GetRequiredParameter<string>("branch");
                var path = input.GetParameter<string?>("path") ?? input.WorkingDirectory;

                if (string.IsNullOrEmpty(path))
                {
                    return ToolResult.Fail("No repository path specified and no working directory set");
                }

                var result = await gitService.CheckoutAsync(path, branch, ct);
                if (!result.Success)
                {
                    return ToolResult.Fail(result.Error ?? "Failed to checkout branch");
                }

                return ToolResult.Ok($"Switched to branch: {branch}");
            }
        });

        // git.push - Push current branch to remote
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "git.push",
            Name = "Git Push",
            Description = "Push the current branch to the remote repository.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "setUpstream": { "type": "boolean", "description": "Set upstream tracking for new branches. Default is true." },
                        "path": { "type": "string", "description": "Path to the repository (defaults to working directory)" }
                    },
                    "required": []
                }
                """,
            Categories = ["git"],
            Handler = async (input, ct) =>
            {
                var setUpstream = input.GetParameter("setUpstream", true);
                var path = input.GetParameter<string?>("path") ?? input.WorkingDirectory;

                if (string.IsNullOrEmpty(path))
                {
                    return ToolResult.Fail("No repository path specified and no working directory set");
                }

                var result = await gitService.PushAsync(path, setUpstream, ct);
                if (!result.Success)
                {
                    return ToolResult.Fail(result.Error ?? "Failed to push");
                }

                return ToolResult.Ok("Push successful");
            }
        });

        // git.pull - Pull latest changes from remote
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "git.pull",
            Name = "Git Pull",
            Description = "Pull the latest changes from the remote repository.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the repository (defaults to working directory)" }
                    },
                    "required": []
                }
                """,
            Categories = ["git"],
            Handler = async (input, ct) =>
            {
                var path = input.GetParameter<string?>("path") ?? input.WorkingDirectory;

                if (string.IsNullOrEmpty(path))
                {
                    return ToolResult.Fail("No repository path specified and no working directory set");
                }

                var result = await gitService.PullAsync(path, ct);
                if (!result.Success)
                {
                    return ToolResult.Fail(result.Error ?? "Failed to pull");
                }

                return ToolResult.Ok("Pull successful");
            }
        });
    }
}
