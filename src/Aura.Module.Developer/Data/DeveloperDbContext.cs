// <copyright file="DeveloperDbContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data;

using Aura.Module.Developer.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Database context for the Developer module.
/// Manages developer-specific entities only. Foundation entities are managed by AuraDbContext.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DeveloperDbContext"/> class.
/// </remarks>
/// <param name="options">Database context options.</param>
public sealed class DeveloperDbContext(DbContextOptions<DeveloperDbContext> options) : DbContext(options)
{

    /// <summary>Gets the workflows.</summary>
    public DbSet<Story> Workflows => Set<Story>();

    /// <summary>Gets the workflow steps.</summary>
    public DbSet<StoryStep> WorkflowSteps => Set<StoryStep>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure developer-specific entities only
        ConfigureDeveloperEntities(modelBuilder);
    }

    private static void ConfigureDeveloperEntities(ModelBuilder modelBuilder)
    {
        // Workflow configuration
        modelBuilder.Entity<Story>(entity =>
        {
            entity.ToTable("workflows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(1000);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.RepositoryPath).HasColumnName("repository_path").HasMaxLength(1000);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.WorktreePath).HasColumnName("worktree_path").HasMaxLength(1000);
            entity.Property(e => e.GitBranch).HasColumnName("git_branch").HasMaxLength(500);
            entity.Property(e => e.AnalyzedContext).HasColumnName("analyzed_context").HasColumnType("jsonb");
            entity.Property(e => e.ExecutionPlan).HasColumnName("execution_plan").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.PullRequestUrl).HasColumnName("pull_request_url").HasMaxLength(1000);

            // Issue integration (Story Model)
            entity.Property(e => e.IssueUrl).HasColumnName("issue_url").HasMaxLength(1000);
            entity.Property(e => e.IssueProvider).HasColumnName("issue_provider").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.IssueNumber).HasColumnName("issue_number");
            entity.Property(e => e.IssueOwner).HasColumnName("issue_owner").HasMaxLength(200);
            entity.Property(e => e.IssueRepo).HasColumnName("issue_repo").HasMaxLength(200);
            entity.Property(e => e.AutomationMode).HasColumnName("automation_mode").HasConversion<string>().HasMaxLength(20);

            // Pattern binding
            entity.Property(e => e.PatternName).HasColumnName("pattern_name").HasMaxLength(100);
            entity.Property(e => e.PatternLanguage).HasColumnName("pattern_language").HasMaxLength(50);

            // Executor preference
            entity.Property(e => e.PreferredExecutor).HasColumnName("preferred_executor").HasMaxLength(50);

            // Chat
            entity.Property(e => e.ChatHistory).HasColumnName("chat_history").HasColumnType("jsonb");

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IssueUrl);
        });

        // WorkflowStep configuration
        modelBuilder.Entity<StoryStep>(entity =>
        {
            entity.ToTable("workflow_steps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StoryId).HasColumnName("workflow_id");
            entity.Property(e => e.Order).HasColumnName("order");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(500);
            entity.Property(e => e.Capability).HasColumnName("capability").HasMaxLength(100);
            entity.Property(e => e.Language).HasColumnName("language").HasMaxLength(50);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AssignedAgentId).HasColumnName("assigned_agent_id").HasMaxLength(100);
            entity.Property(e => e.Input).HasColumnName("input").HasColumnType("jsonb");
            entity.Property(e => e.Output).HasColumnName("output").HasColumnType("jsonb");
            entity.Property(e => e.Error).HasColumnName("error");
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            // Assisted workflow UI fields
            entity.Property(e => e.Approval).HasColumnName("approval");
            entity.Property(e => e.ApprovalFeedback).HasColumnName("approval_feedback");
            entity.Property(e => e.SkipReason).HasColumnName("skip_reason");
            entity.Property(e => e.ChatHistory).HasColumnName("chat_history").HasColumnType("jsonb");
            entity.Property(e => e.NeedsRework).HasColumnName("needs_rework");
            entity.Property(e => e.PreviousOutput).HasColumnName("previous_output").HasColumnType("jsonb");

            // Executor override
            entity.Property(e => e.ExecutorOverride).HasColumnName("executor_override").HasMaxLength(50);

            entity.HasOne(e => e.Story)
                .WithMany(w => w.Steps)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.StoryId, e.Order });
            entity.HasIndex(e => e.Status);
        });
    }
}
