// <copyright file="WorkspaceRegistryServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using System.IO.Abstractions.TestingHelpers;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class WorkspaceRegistryServiceTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly DbContextOptions<AuraDbContext> _dbOptions;
    private readonly IDbContextFactory<AuraDbContext> _dbContextFactory;
    private readonly WorkspaceRegistryService _sut;

    public WorkspaceRegistryServiceTests()
    {
        _fileSystem = new MockFileSystem();

        // Create in-memory database options - reuse same database name for consistency
        var dbName = Guid.NewGuid().ToString();
        _dbOptions = new DbContextOptionsBuilder<AuraDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Return a new context instance each time to avoid disposal issues
        _dbContextFactory = Substitute.For<IDbContextFactory<AuraDbContext>>();
        _dbContextFactory.CreateDbContext().Returns(_ => new MinimalTestDbContext(_dbOptions));

        _sut = new WorkspaceRegistryService(
            _fileSystem,
            _dbContextFactory,
            NullLogger<WorkspaceRegistryService>.Instance);
    }

    [Fact]
    public void ListWorkspaces_EmptyRegistry_ReturnsEmptyList()
    {
        // Act
        var result = _sut.ListWorkspaces();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AddWorkspace_ValidPath_AddsWorkspace()
    {
        // Arrange
        var path = @"c:\work\my-project";

        // Act
        var result = _sut.AddWorkspace(path);

        // Assert
        result.Should().NotBeNull();
        result.Path.Should().Be("c:/work/my-project");
        result.Id.Should().HaveLength(16);
        result.Alias.Should().BeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact]
    public void AddWorkspace_WithAlias_SetsAlias()
    {
        // Arrange
        var path = @"c:\work\my-project";
        var alias = "myproj";

        // Act
        var result = _sut.AddWorkspace(path, alias);

        // Assert
        result.Alias.Should().Be(alias);
    }

    [Fact]
    public void AddWorkspace_WithTags_SetsTags()
    {
        // Arrange
        var path = @"c:\work\my-project";
        var tags = new List<string> { "dotnet", "api" };

        // Act
        var result = _sut.AddWorkspace(path, tags: tags);

        // Assert
        result.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public void AddWorkspace_FirstWorkspace_SetsAsDefault()
    {
        // Arrange
        var path = @"c:\work\my-project";

        // Act
        _sut.AddWorkspace(path);
        var defaultWorkspace = _sut.GetDefaultWorkspace();

        // Assert
        defaultWorkspace.Should().NotBeNull();
        defaultWorkspace!.Path.Should().Be("c:/work/my-project");
    }

    [Fact]
    public void AddWorkspace_DuplicatePath_ThrowsException()
    {
        // Arrange
        var path = @"c:\work\my-project";
        _sut.AddWorkspace(path);

        // Act
        var act = () => _sut.AddWorkspace(path);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void AddWorkspace_DuplicateAlias_ThrowsException()
    {
        // Arrange
        _sut.AddWorkspace(@"c:\work\project1", "myalias");

        // Act
        var act = () => _sut.AddWorkspace(@"c:\work\project2", "myalias");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Alias already in use*");
    }

    [Fact]
    public void GetWorkspace_ById_ReturnsWorkspace()
    {
        // Arrange
        var added = _sut.AddWorkspace(@"c:\work\my-project");

        // Act
        var result = _sut.GetWorkspace(added.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(added.Id);
    }

    [Fact]
    public void GetWorkspace_ByAlias_ReturnsWorkspace()
    {
        // Arrange
        _sut.AddWorkspace(@"c:\work\my-project", "myproj");

        // Act
        var result = _sut.GetWorkspace("myproj");

        // Assert
        result.Should().NotBeNull();
        result!.Alias.Should().Be("myproj");
    }

    [Fact]
    public void GetWorkspace_ByAliasIgnoresCase_ReturnsWorkspace()
    {
        // Arrange
        _sut.AddWorkspace(@"c:\work\my-project", "MyProj");

        // Act
        var result = _sut.GetWorkspace("MYPROJ");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetWorkspace_NonExistent_ReturnsNull()
    {
        // Act
        var result = _sut.GetWorkspace("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RemoveWorkspace_ExistingWorkspace_ReturnsTrue()
    {
        // Arrange
        var added = _sut.AddWorkspace(@"c:\work\my-project");

        // Act
        var result = _sut.RemoveWorkspace(added.Id);

        // Assert
        result.Should().BeTrue();
        _sut.ListWorkspaces().Should().BeEmpty();
    }

    [Fact]
    public void RemoveWorkspace_NonExistent_ReturnsFalse()
    {
        // Act
        var result = _sut.RemoveWorkspace("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveWorkspace_DefaultWorkspace_ClearsDefault()
    {
        // Arrange
        var ws1 = _sut.AddWorkspace(@"c:\work\project1");
        var ws2 = _sut.AddWorkspace(@"c:\work\project2");

        // Ensure ws1 is default
        _sut.SetDefault(ws1.Id);

        // Act
        _sut.RemoveWorkspace(ws1.Id);

        // Assert - default should be ws2 now
        var defaultWs = _sut.GetDefaultWorkspace();
        defaultWs.Should().NotBeNull();
        defaultWs!.Id.Should().Be(ws2.Id);
    }

    [Fact]
    public void SetDefault_ValidId_ReturnsTrue()
    {
        // Arrange
        _sut.AddWorkspace(@"c:\work\project1");
        var ws2 = _sut.AddWorkspace(@"c:\work\project2");

        // Act
        var result = _sut.SetDefault(ws2.Id);

        // Assert
        result.Should().BeTrue();
        _sut.GetDefaultWorkspace()!.Id.Should().Be(ws2.Id);
    }

    [Fact]
    public void SetDefault_InvalidId_ReturnsFalse()
    {
        // Act
        var result = _sut.SetDefault("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ResolveWorkspaceIds_EmptyList_ReturnsEmptyList()
    {
        // Act
        var result = _sut.ResolveWorkspaceIds([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveWorkspaceIds_WildCard_ReturnsAllWorkspaces()
    {
        // Arrange
        var ws1 = _sut.AddWorkspace(@"c:\work\project1");
        var ws2 = _sut.AddWorkspace(@"c:\work\project2");

        // Act
        var result = _sut.ResolveWorkspaceIds(["*"]);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(ws1.Id);
        result.Should().Contain(ws2.Id);
    }

    [Fact]
    public void ResolveWorkspaceIds_ById_ResolvesCorrectly()
    {
        // Arrange
        var ws1 = _sut.AddWorkspace(@"c:\work\project1");
        _sut.AddWorkspace(@"c:\work\project2");

        // Act
        var result = _sut.ResolveWorkspaceIds([ws1.Id]);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(ws1.Id);
    }

    [Fact]
    public void ResolveWorkspaceIds_ByAlias_ResolvesCorrectly()
    {
        // Arrange
        var ws1 = _sut.AddWorkspace(@"c:\work\project1", "proj1");
        _sut.AddWorkspace(@"c:\work\project2", "proj2");

        // Act
        var result = _sut.ResolveWorkspaceIds(["proj1"]);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(ws1.Id);
    }

    [Fact]
    public void ResolveWorkspaceIds_MixedReferences_ResolvesDeduplicated()
    {
        // Arrange
        var ws1 = _sut.AddWorkspace(@"c:\work\project1", "proj1");

        // Act - same workspace referenced by both ID and alias
        var result = _sut.ResolveWorkspaceIds([ws1.Id, "proj1"]);

        // Assert - should be deduplicated
        result.Should().ContainSingle().Which.Should().Be(ws1.Id);
    }

    [Fact]
    public void ResolveWorkspaceIds_NonExistentReference_SkipsIt()
    {
        // Arrange
        var ws1 = _sut.AddWorkspace(@"c:\work\project1");

        // Act
        var result = _sut.ResolveWorkspaceIds([ws1.Id, "nonexistent"]);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(ws1.Id);
    }

    [Fact]
    public void ListWorkspaces_WithIndexedChunks_ShowsIndexStatus()
    {
        // Arrange
        var workspace = _sut.AddWorkspace(@"c:\work\my-project");

        // Add some chunks to the database using a fresh context
        using (var dbContext = new MinimalTestDbContext(_dbOptions))
        {
            dbContext.RagChunks.Add(new RagChunk
            {
                Id = Guid.NewGuid(),
                ContentId = "c:/work/my-project/file.cs",
                SourcePath = "c:/work/my-project/file.cs",
                WorkspaceId = workspace.Id,
                ChunkIndex = 0,
                Content = "test",
                ContentType = RagContentType.Code,
                Embedding = null
            });
            dbContext.SaveChanges();
        }

        // Create a fresh service to ensure cache is clear
        var freshService = new WorkspaceRegistryService(
            _fileSystem,
            _dbContextFactory,
            NullLogger<WorkspaceRegistryService>.Instance);

        // Act
        var result = freshService.ListWorkspaces();

        // Assert
        result.Should().ContainSingle();
        result[0].Indexed.Should().BeTrue();
        result[0].ChunkCount.Should().Be(1);
    }

    [Fact]
    public void Persistence_SavesAndLoadsRegistry()
    {
        // Arrange
        _sut.AddWorkspace(@"c:\work\project1", "proj1", ["tag1"]);
        _sut.AddWorkspace(@"c:\work\project2", "proj2");

        // Create a new service instance (simulates restart)
        var newService = new WorkspaceRegistryService(
            _fileSystem,
            _dbContextFactory,
            NullLogger<WorkspaceRegistryService>.Instance);

        // Act
        var result = newService.ListWorkspaces();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(w => w.Alias == "proj1");
        result.Should().Contain(w => w.Alias == "proj2");
    }

    [Fact]
    public void AddWorkspace_NullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.AddWorkspace(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddWorkspace_EmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.AddWorkspace("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetWorkspace_EmptyId_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.GetWorkspace("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Minimal test DbContext that uses Ignore() to skip pgvector types entirely.
    /// </summary>
    private sealed class MinimalTestDbContext : AuraDbContext
    {
        public MinimalTestDbContext(DbContextOptions<AuraDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Completely ignore types with Vector properties
            modelBuilder.Ignore<CodeNode>();

            // Configure RagChunk without embedding
            modelBuilder.Entity<RagChunk>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Ignore(e => e.Embedding);
            });

            modelBuilder.Entity<Workspace>().HasKey(e => e.Id);
            modelBuilder.Entity<IndexMetadata>().HasKey(e => e.Id);
        }
    }
}
