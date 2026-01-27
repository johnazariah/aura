// <copyright file="DocsServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Services;

using System.Reflection;
using Aura.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class DocsServiceTests
{
    [Fact]
    public void ListDocuments_WithNoFilters_ReturnsAllDocuments()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(d => d.Id == "getting-started");
        result.Should().Contain(d => d.Id == "architecture");
        result.Should().Contain(d => d.Id == "api-reference");
    }

    [Fact]
    public void ListDocuments_FilterByCategory_ReturnsMatchingDocuments()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(category: "guides");

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainSingle(d => d.Id == "getting-started");
        result[0].Category.Should().Be("guides");
    }

    [Fact]
    public void ListDocuments_FilterByCategory_IsCaseInsensitive()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(category: "GUIDES");

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainSingle(d => d.Id == "getting-started");
    }

    [Fact]
    public void ListDocuments_FilterByNonExistentCategory_ReturnsEmpty()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(category: "nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ListDocuments_FilterBySingleTag_ReturnsMatchingDocuments()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(tags: new[] { "tutorial" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainSingle(d => d.Id == "getting-started");
    }

    [Fact]
    public void ListDocuments_FilterByMultipleTags_UsesOrLogic()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(tags: new[] { "tutorial", "advanced" });

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.Id == "getting-started");
        result.Should().Contain(d => d.Id == "architecture");
    }

    [Fact]
    public void ListDocuments_FilterByTags_IsCaseInsensitive()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(tags: new[] { "TUTORIAL" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainSingle(d => d.Id == "getting-started");
    }

    [Fact]
    public void ListDocuments_FilterByCategoryAndTags_AppliesBothFilters()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(category: "technical", tags: new[] { "advanced" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainSingle(d => d.Id == "architecture");
    }

    [Fact]
    public void ListDocuments_FilterByCategoryAndTags_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.ListDocuments(category: "guides", tags: new[] { "advanced" });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetDocument_WithValidId_ReturnsDocumentContent()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.GetDocument("getting-started");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("getting-started");
        result.Title.Should().Be("Getting Started");
        result.Category.Should().Be("guides");
        result.Tags.Should().BeEquivalentTo(new[] { "tutorial", "beginner" });
        result.Content.Should().Contain("# Getting Started");
        result.LastUpdated.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetDocument_WithValidId_IsCaseInsensitive()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.GetDocument("GETTING-STARTED");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("getting-started");
    }

    [Fact]
    public void GetDocument_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.GetDocument("nonexistent-document");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDocument_WithEmptyId_ReturnsNull()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.GetDocument(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDocument_VerifiesDocumentContentFormat()
    {
        // Arrange
        var service = TestableDocsServiceFactory.Create();

        // Act
        var result = service.GetDocument("architecture");

        // Assert
        result.Should().NotBeNull();
        result!.Content.Should().Contain("# Architecture Overview");
        result.Content.Should().Contain("This document describes");
    }

    private static class TestableDocsServiceFactory
    {
        public static DocsService Create()
        {
            var service = new DocsService(NullLogger<DocsService>.Instance);

            var registryField = typeof(DocsService)
                .GetField("_registry", BindingFlags.NonPublic | BindingFlags.Instance);
            var assemblyField = typeof(DocsService)
                .GetField("_assembly", BindingFlags.NonPublic | BindingFlags.Instance);

            if (registryField is null || assemblyField is null)
            {
                throw new InvalidOperationException("Could not access DocsService private fields");
            }

            registryField.SetValue(service, CreateTestRegistry());
            assemblyField.SetValue(service, CreateTestAssembly());

            return service;
        }

        private static object CreateTestRegistry()
        {
            var registryType = typeof(DocsService).Assembly.GetType("Aura.Api.Services.DocsRegistry");
            var documentType = typeof(DocsService).Assembly.GetType("Aura.Api.Services.RegistryDocument");

            if (registryType is null || documentType is null)
            {
                throw new InvalidOperationException("Could not find internal types");
            }

            var registry = Activator.CreateInstance(registryType)!;
            var documentsProperty = registryType.GetProperty("Documents");
            var documentsList = Activator.CreateInstance(typeof(List<>).MakeGenericType(documentType))!;

            var documents = new[]
            {
                CreateDocument(documentType, "getting-started", "Getting Started", "Quick start guide for new users",
                    "guides", new[] { "tutorial", "beginner" }, "guides/getting-started.md"),
                CreateDocument(documentType, "architecture", "Architecture Overview", "System architecture and design patterns",
                    "technical", new[] { "advanced", "design" }, "technical/architecture.md"),
                CreateDocument(documentType, "api-reference", "API Reference", "Complete API documentation",
                    "reference", new[] { "api", "reference" }, "reference/api.md"),
            };

            var addMethod = documentsList.GetType().GetMethod("Add");
            foreach (var doc in documents)
            {
                addMethod!.Invoke(documentsList, new[] { doc });
            }

            documentsProperty!.SetValue(registry, documentsList);
            return registry;
        }

        private static object CreateDocument(Type documentType, string id, string title, string summary,
            string category, string[] tags, string path)
        {
            var doc = Activator.CreateInstance(documentType)!;
            documentType.GetProperty("Id")!.SetValue(doc, id);
            documentType.GetProperty("Title")!.SetValue(doc, title);
            documentType.GetProperty("Summary")!.SetValue(doc, summary);
            documentType.GetProperty("Category")!.SetValue(doc, category);
            documentType.GetProperty("Tags")!.SetValue(doc, new List<string>(tags));
            documentType.GetProperty("Path")!.SetValue(doc, path);
            return doc;
        }

        private static Assembly CreateTestAssembly()
        {
            return new TestAssemblyBuilder()
                .WithResource("Aura.Api.Docs.guides.getting-started.md", "# Getting Started\n\nWelcome to Aura!")
                .WithResource("Aura.Api.Docs.technical.architecture.md", "# Architecture Overview\n\nThis document describes the system architecture.")
                .WithResource("Aura.Api.Docs.reference.api.md", "# API Reference\n\nComplete API documentation.")
                .Build();
        }
    }

    private sealed class TestAssemblyBuilder
    {
        private readonly Dictionary<string, string> _resources = new();

        public TestAssemblyBuilder WithResource(string name, string content)
        {
            _resources[name] = content;
            return this;
        }

        public Assembly Build()
        {
            return new TestAssembly(_resources);
        }
    }

    private sealed class TestAssembly : Assembly
    {
        private readonly Dictionary<string, string> _resources;

        public TestAssembly(Dictionary<string, string> resources)
        {
            _resources = resources;
        }

        public override Stream? GetManifestResourceStream(string name)
        {
            if (_resources.TryGetValue(name, out var content))
            {
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            }

            return null;
        }
    }
}
