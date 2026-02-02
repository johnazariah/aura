// <copyright file="ResearcherDbContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data;

using Aura.Module.Researcher.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entity Framework context for the Researcher module.
/// </summary>
public class ResearcherDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResearcherDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public ResearcherDbContext(DbContextOptions<ResearcherDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets or sets the sources.</summary>
    public DbSet<Source> Sources => Set<Source>();

    /// <summary>Gets or sets the excerpts.</summary>
    public DbSet<Excerpt> Excerpts => Set<Excerpt>();

    /// <summary>Gets or sets the concepts.</summary>
    public DbSet<Concept> Concepts => Set<Concept>();

    /// <summary>Gets or sets the concept links.</summary>
    public DbSet<ConceptLink> ConceptLinks => Set<ConceptLink>();

    /// <summary>Gets or sets the source-concept relationships.</summary>
    public DbSet<SourceConcept> SourceConcepts => Set<SourceConcept>();

    /// <summary>Gets or sets the syntheses.</summary>
    public DbSet<Synthesis> Syntheses => Set<Synthesis>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Check if we're using InMemory or non-PostgreSQL database (for testing)
        var isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        // Source configuration
        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            if (isPostgres)
            {
                entity.Property(e => e.Authors).HasColumnType("text[]");
                entity.Property(e => e.Tags).HasColumnType("text[]");
                entity.Property(e => e.Embedding).HasColumnType("vector(384)");
            }
            else
            {
                // Ignore Vector type for InMemory/SQLite testing
                entity.Ignore(e => e.Embedding);
            }

            entity.HasIndex(e => e.SourceType);
            entity.HasIndex(e => e.ReadingStatus);
            entity.HasIndex(e => e.ArxivId);
            entity.HasIndex(e => e.Doi);
        });

        // Excerpt configuration
        modelBuilder.Entity<Excerpt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            if (isPostgres)
            {
                entity.Property(e => e.Embedding).HasColumnType("vector(384)");
            }
            else
            {
                entity.Ignore(e => e.Embedding);
            }

            entity.HasOne(e => e.Source)
                .WithMany(s => s.Excerpts)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Concept configuration
        modelBuilder.Entity<Concept>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            if (isPostgres)
            {
                entity.Property(e => e.Aliases).HasColumnType("text[]");
                entity.Property(e => e.Embedding).HasColumnType("vector(384)");
            }
            else
            {
                entity.Ignore(e => e.Embedding);
            }

            entity.HasIndex(e => e.Name).IsUnique();
        });

        // ConceptLink configuration
        modelBuilder.Entity<ConceptLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Relationship).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.FromConcept)
                .WithMany(c => c.OutgoingLinks)
                .HasForeignKey(e => e.FromConceptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ToConcept)
                .WithMany(c => c.IncomingLinks)
                .HasForeignKey(e => e.ToConceptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Source)
                .WithMany()
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SourceConcept configuration (many-to-many)
        modelBuilder.Entity<SourceConcept>(entity =>
        {
            entity.HasKey(e => new { e.SourceId, e.ConceptId });
            entity.HasOne(e => e.Source)
                .WithMany()
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Concept)
                .WithMany(c => c.SourceConcepts)
                .HasForeignKey(e => e.ConceptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Synthesis configuration
        modelBuilder.Entity<Synthesis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            if (isPostgres)
            {
                entity.Property(e => e.SourceIds).HasColumnType("uuid[]");
            }
        });
    }
}
