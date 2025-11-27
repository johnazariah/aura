// <copyright file="DeveloperDbContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data;

using Aura.Foundation.Data;
using Aura.Module.Developer.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Database context for the Developer module.
/// Extends AuraDbContext with developer-specific entities.
/// </summary>
public sealed class DeveloperDbContext : AuraDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperDbContext"/> class.
    /// </summary>
    /// <param name="options">Database context options.</param>
    public DeveloperDbContext(DbContextOptions<DeveloperDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the workflows.</summary>
    public DbSet<Workflow> Workflows => Set<Workflow>();

    /// <summary>Gets the workflow steps.</summary>
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure foundation entities first
        ConfigureFoundationEntities(modelBuilder);

        // Configure developer-specific entities
        ConfigureDeveloperEntities(modelBuilder);
    }

    private static void ConfigureDeveloperEntities(ModelBuilder modelBuilder)
    {
        // Workflow configuration
        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.ToTable("workflows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkItemId).HasColumnName("work_item_id").HasMaxLength(500);
            entity.Property(e => e.WorkItemTitle).HasColumnName("work_item_title").HasMaxLength(1000);
            entity.Property(e => e.WorkItemDescription).HasColumnName("work_item_description");
            entity.Property(e => e.WorkItemUrl).HasColumnName("work_item_url").HasMaxLength(2000);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.WorkspacePath).HasColumnName("workspace_path").HasMaxLength(1000);
            entity.Property(e => e.GitBranch).HasColumnName("git_branch").HasMaxLength(500);
            entity.Property(e => e.DigestedContext).HasColumnName("digested_context").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.WorkItemId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // WorkflowStep configuration
        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("workflow_steps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id");
            entity.Property(e => e.Order).HasColumnName("order");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(500);
            entity.Property(e => e.Capability).HasColumnName("capability").HasMaxLength(100);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AssignedAgentId).HasColumnName("assigned_agent_id").HasMaxLength(100);
            entity.Property(e => e.Input).HasColumnName("input").HasColumnType("jsonb");
            entity.Property(e => e.Output).HasColumnName("output").HasColumnType("jsonb");
            entity.Property(e => e.Error).HasColumnName("error");
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasOne(e => e.Workflow)
                .WithMany(w => w.Steps)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.WorkflowId, e.Order });
            entity.HasIndex(e => e.Status);
        });
    }
}
