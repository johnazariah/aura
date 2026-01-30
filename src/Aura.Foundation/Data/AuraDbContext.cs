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

    /// <summary>Gets the code nodes for graph RAG.</summary>
    public DbSet<CodeNode> CodeNodes => Set<CodeNode>();

    /// <summary>Gets the code edges for graph RAG.</summary>
    public DbSet<CodeEdge> CodeEdges => Set<CodeEdge>();

    /// <summary>Gets the index metadata for tracking index freshness.</summary>
    public DbSet<IndexMetadata> IndexMetadata => Set<IndexMetadata>();

    /// <summary>Gets the workspaces (onboarded directories/repositories).</summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();

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
            entity.Property(e => e.RepositoryPath).HasColumnName("repository_path").HasMaxLength(1000);
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
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id").HasMaxLength(16);
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.ContentId);
            entity.HasIndex(e => new { e.ContentId, e.ChunkIndex }).IsUnique();
            entity.HasIndex(e => e.WorkspaceId);
        });

        // CodeNode configuration for graph RAG
        modelBuilder.Entity<CodeNode>(entity =>
        {
            entity.ToTable("code_nodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.NodeType).HasColumnName("node_type").HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(2000);
            entity.Property(e => e.FilePath).HasColumnName("file_path").HasMaxLength(1000);
            entity.Property(e => e.LineNumber).HasColumnName("line_number");
            entity.Property(e => e.Signature).HasColumnName("signature").HasMaxLength(2000);
            entity.Property(e => e.Modifiers).HasColumnName("modifiers").HasMaxLength(200);
            entity.Property(e => e.RepositoryPath).HasColumnName("repository_path").HasMaxLength(1000);
            entity.Property(e => e.PropertiesJson).HasColumnName("properties").HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.IndexedAt).HasColumnName("indexed_at");

            entity.HasIndex(e => e.NodeType);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.FullName);
            entity.HasIndex(e => e.RepositoryPath);
            entity.HasIndex(e => new { e.RepositoryPath, e.NodeType });
        });

        // CodeEdge configuration for graph RAG
        modelBuilder.Entity<CodeEdge>(entity =>
        {
            entity.ToTable("code_edges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.EdgeType).HasColumnName("edge_type").HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.PropertiesJson).HasColumnName("properties").HasColumnType("jsonb");

            entity.HasOne(e => e.Source)
                .WithMany(n => n.OutgoingEdges)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Target)
                .WithMany(n => n.IncomingEdges)
                .HasForeignKey(e => e.TargetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SourceId);
            entity.HasIndex(e => e.TargetId);
            entity.HasIndex(e => e.EdgeType);
            entity.HasIndex(e => new { e.SourceId, e.EdgeType });
            entity.HasIndex(e => new { e.TargetId, e.EdgeType });
        });

        // IndexMetadata configuration for tracking index freshness
        modelBuilder.Entity<IndexMetadata>(entity =>
        {
            entity.ToTable("index_metadata");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.WorkspacePath).HasColumnName("workspace_path").HasMaxLength(1024).IsRequired();
            entity.Property(e => e.IndexType).HasColumnName("index_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.IndexedAt).HasColumnName("indexed_at");
            entity.Property(e => e.CommitSha).HasColumnName("commit_sha").HasMaxLength(40);
            entity.Property(e => e.CommitAt).HasColumnName("commit_at");
            entity.Property(e => e.FilesIndexed).HasColumnName("files_indexed");
            entity.Property(e => e.ItemsCreated).HasColumnName("items_created");
            entity.Property(e => e.StatsJson).HasColumnName("stats").HasColumnType("jsonb");

            entity.HasIndex(e => e.WorkspacePath);
            entity.HasIndex(e => e.IndexType);
            entity.HasIndex(e => new { e.WorkspacePath, e.IndexType }).IsUnique();
        });

        // Workspace configuration for onboarded directories/repositories
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("workspaces");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(16).IsRequired();
            entity.Property(e => e.CanonicalPath).HasColumnName("canonical_path").HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastAccessedAt).HasColumnName("last_accessed_at");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.GitRemoteUrl).HasColumnName("git_remote_url").HasMaxLength(2048);
            entity.Property(e => e.DefaultBranch).HasColumnName("default_branch").HasMaxLength(255);

            entity.HasIndex(e => e.CanonicalPath).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastAccessedAt);
        });
    }
}
