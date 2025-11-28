// <copyright file="AuraDbContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data;

using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core database context for Aura Foundation.
/// Contains core entities shared by all Aura applications.
/// </summary>
public class AuraDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuraDbContext"/> class.
    /// </summary>
    /// <param name="options">Database context options.</param>
    public AuraDbContext(DbContextOptions<AuraDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuraDbContext"/> class.
    /// Protected constructor for derived contexts.
    /// </summary>
    /// <param name="options">Database context options.</param>
    protected AuraDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Gets the conversations.</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Gets the messages.</summary>
    public DbSet<Message> Messages => Set<Message>();

    /// <summary>Gets the message RAG contexts.</summary>
    public DbSet<MessageRagContext> MessageRagContexts => Set<MessageRagContext>();

    /// <summary>Gets the agent executions.</summary>
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();

    /// <summary>Gets the RAG chunks for vector search.</summary>
    public DbSet<RagChunk> RagChunks => Set<RagChunk>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");
        ConfigureFoundationEntities(modelBuilder);
    }

    /// <summary>
    /// Configures the foundation entities. Can be called by derived contexts.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected static void ConfigureFoundationEntities(ModelBuilder modelBuilder)
    {
        // Conversation configuration
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500);
            entity.Property(e => e.AgentId).HasColumnName("agent_id").HasMaxLength(100);
            entity.Property(e => e.WorkspacePath).HasColumnName("workspace_path").HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Message configuration
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Model).HasColumnName("model").HasMaxLength(100);
            entity.Property(e => e.TokensUsed).HasColumnName("tokens_used");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // MessageRagContext configuration - stores RAG context used in messages
        modelBuilder.Entity<MessageRagContext>(entity =>
        {
            entity.ToTable("message_rag_contexts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.Query).HasColumnName("query").IsRequired();
            entity.Property(e => e.ContentId).HasColumnName("content_id").HasMaxLength(500).IsRequired();
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.ChunkContent).HasColumnName("chunk_content").IsRequired();
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.SourcePath).HasColumnName("source_path").HasMaxLength(1000);
            entity.Property(e => e.ContentType).HasColumnName("content_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.RetrievedAt).HasColumnName("retrieved_at");

            entity.HasOne(e => e.Message)
                .WithMany()
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.ContentId);
        });

        // AgentExecution configuration
        modelBuilder.Entity<AgentExecution>(entity =>
        {
            entity.ToTable("agent_executions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgentId).HasColumnName("agent_id").HasMaxLength(100);
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.Prompt).HasColumnName("prompt");
            entity.Property(e => e.Response).HasColumnName("response");
            entity.Property(e => e.Model).HasColumnName("model").HasMaxLength(100);
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
            entity.Property(e => e.TokensUsed).HasColumnName("tokens_used");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasOne(e => e.Conversation)
                .WithMany()
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Success);
        });

        // RagChunk configuration for vector search
        modelBuilder.Entity<RagChunk>(entity =>
        {
            entity.ToTable("rag_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ContentId).HasColumnName("content_id").HasMaxLength(500).IsRequired();
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.ContentType).HasColumnName("content_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.SourcePath).HasColumnName("source_path").HasMaxLength(1000);
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.ContentId);
            entity.HasIndex(e => new { e.ContentId, e.ChunkIndex }).IsUnique();
        });
    }
}
