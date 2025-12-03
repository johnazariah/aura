// <copyright file="CSharpIngesterAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Agents;

using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class CSharpIngesterAgentTests
{
    private readonly CSharpIngesterAgent _agent = new(NullLogger<CSharpIngesterAgent>.Instance);

    [Fact]
    public void AgentId_ShouldBeCSharpIngester()
    {
        Assert.Equal("csharp-ingester", _agent.AgentId);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectCapabilities()
    {
        Assert.Contains("ingest:cs", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:csx", _agent.Metadata.Capabilities);
    }

    [Fact]
    public void Metadata_ShouldHavePriority10()
    {
        Assert.Equal(10, _agent.Metadata.Priority);
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleClass_ShouldExtractChunks()
    {
        // Arrange
        var code = """
            namespace TestNamespace;

            /// <summary>
            /// A simple test class.
            /// </summary>
            public class TestClass
            {
                public string Name { get; set; }

                public void DoSomething()
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "TestFile.cs",
                ["content"] = code,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.NotNull(output);
        Assert.Contains("chunks", output.Artifacts.Keys);

        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);
        Assert.NotNull(chunks);
        Assert.True(chunks.Count >= 3, $"Expected at least 3 chunks (class, property, method), got {chunks.Count}");

        // Should have the class
        var classChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkTypes.Class);
        Assert.NotNull(classChunk);
        Assert.Equal("TestClass", classChunk.SymbolName);
        Assert.Equal("TestNamespace.TestClass", classChunk.FullyQualifiedName);

        // Should have the property
        var propertyChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkTypes.Property);
        Assert.NotNull(propertyChunk);
        Assert.Equal("Name", propertyChunk.SymbolName);
        Assert.Equal("TestClass", propertyChunk.ParentSymbol);

        // Should have the method
        var methodChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkTypes.Method);
        Assert.NotNull(methodChunk);
        Assert.Equal("DoSomething", methodChunk.SymbolName);
    }

    [Fact]
    public async Task ExecuteAsync_WithInterface_ShouldExtractInterface()
    {
        // Arrange
        var code = """
            namespace TestNamespace;

            public interface ITestService
            {
                Task<string> GetDataAsync(int id);
            }
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "ITestService.cs",
                ["content"] = code,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var interfaceChunk = chunks?.FirstOrDefault(c => c.ChunkType == ChunkTypes.Interface);
        Assert.NotNull(interfaceChunk);
        Assert.Equal("ITestService", interfaceChunk.SymbolName);
    }

    [Fact]
    public async Task ExecuteAsync_WithRecord_ShouldExtractRecord()
    {
        // Arrange
        var code = """
            namespace TestNamespace;

            public record Person(string Name, int Age);
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "Person.cs",
                ["content"] = code,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var recordChunk = chunks?.FirstOrDefault(c => c.ChunkType == ChunkTypes.Record);
        Assert.NotNull(recordChunk);
        Assert.Equal("Person", recordChunk.SymbolName);
    }

    [Fact]
    public async Task ExecuteAsync_WithEnum_ShouldExtractEnum()
    {
        // Arrange
        var code = """
            namespace TestNamespace;

            public enum Status
            {
                Pending,
                Active,
                Completed
            }
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "Status.cs",
                ["content"] = code,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var enumChunk = chunks?.FirstOrDefault(c => c.ChunkType == ChunkTypes.Enum);
        Assert.NotNull(enumChunk);
        Assert.Equal("Status", enumChunk.SymbolName);
    }

    [Fact]
    public async Task ExecuteAsync_WithNestedClass_ShouldExtractBoth()
    {
        // Arrange
        var code = """
            namespace TestNamespace;

            public class OuterClass
            {
                public class InnerClass
                {
                    public int Value { get; set; }
                }
            }
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "Nested.cs",
                ["content"] = code,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var classes = chunks?.Where(c => c.ChunkType == ChunkTypes.Class).ToList();
        Assert.NotNull(classes);
        Assert.True(classes.Count >= 2, $"Expected at least 2 classes, got {classes.Count}");
        Assert.Contains(classes, c => c.SymbolName == "OuterClass");
        Assert.Contains(classes, c => c.SymbolName == "InnerClass");
    }

    [Fact]
    public async Task ExecuteAsync_WithConstructor_ShouldExtractConstructor()
    {
        // Arrange
        var code = """
            namespace TestNamespace;

            public class MyService
            {
                private readonly ILogger _logger;

                public MyService(ILogger logger)
                {
                    _logger = logger;
                }
            }
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "MyService.cs",
                ["content"] = code,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var ctorChunk = chunks?.FirstOrDefault(c => c.ChunkType == ChunkTypes.Constructor);
        Assert.NotNull(ctorChunk);
        Assert.Equal("MyService", ctorChunk.SymbolName);
    }

    [Fact]
    public async Task ExecuteAsync_MissingFilePath_ShouldThrow()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["content"] = "public class Foo { }",
            });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _agent.ExecuteAsync(context));
    }

    [Fact]
    public async Task ExecuteAsync_MissingContent_ShouldThrow()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "Test.cs",
            });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _agent.ExecuteAsync(context));
    }
}
