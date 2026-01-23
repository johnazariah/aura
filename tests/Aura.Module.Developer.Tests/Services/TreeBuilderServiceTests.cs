// <copyright file="TreeBuilderServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Services;

using Aura.Foundation.Rag;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class TreeBuilderServiceTests
{
    private readonly TreeBuilderService _sut;

    public TreeBuilderServiceTests()
    {
        _sut = new TreeBuilderService(NullLogger<TreeBuilderService>.Instance);
    }

    [Fact]
    public void BuildTree_EmptyChunks_ReturnsEmptyTree()
    {
        // Arrange
        var chunks = Array.Empty<TreeChunk>();

        // Act
        var result = _sut.BuildTree(chunks);

        // Assert
        Assert.Equal(".", result.RootPath);
        Assert.Empty(result.Nodes);
        Assert.Equal(0, result.TotalNodes);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void BuildTree_SingleFile_ReturnsFileNode()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService { }")
            {
                SymbolName = "OrderService",
                Language = "csharp",
                StartLine = 1,
                EndLine = 10,
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 2);

        // Assert
        Assert.Single(result.Nodes);
        var fileNode = result.Nodes[0];
        Assert.Equal("file:src/Services/OrderService.cs:", fileNode.NodeId);
        Assert.Equal("OrderService.cs", fileNode.Name);
        Assert.Equal("file", fileNode.Type);
    }

    [Fact]
    public void BuildTree_FileWithType_ReturnsTypeAsChild()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService { }")
            {
                SymbolName = "OrderService",
                Language = "csharp",
                StartLine = 5,
                EndLine = 50,
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 3);

        // Assert
        Assert.Single(result.Nodes);
        var fileNode = result.Nodes[0];
        Assert.NotNull(fileNode.Children);
        Assert.Single(fileNode.Children);

        var typeNode = fileNode.Children[0];
        Assert.Equal("type:src/Services/OrderService.cs:OrderService", typeNode.NodeId);
        Assert.Equal("OrderService", typeNode.Name);
        Assert.Equal("type", typeNode.Type);
    }

    [Fact]
    public void BuildTree_TypeWithMethods_ReturnsMethodsAsChildren()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService { }")
            {
                SymbolName = "OrderService",
                Language = "csharp",
                StartLine = 5,
                EndLine = 50,
            },
            new("src/Services/OrderService.cs", "method", "public async Task ProcessAsync() { }")
            {
                SymbolName = "ProcessAsync",
                ParentSymbol = "OrderService",
                Language = "csharp",
                StartLine = 10,
                EndLine = 20,
            },
            new("src/Services/OrderService.cs", "method", "public Order GetOrder(int id) { }")
            {
                SymbolName = "GetOrder",
                ParentSymbol = "OrderService",
                Language = "csharp",
                StartLine = 22,
                EndLine = 30,
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 3);

        // Assert
        var fileNode = result.Nodes[0];
        var typeNode = fileNode.Children![0];
        Assert.NotNull(typeNode.Children);
        Assert.Equal(2, typeNode.Children.Count);

        var methods = typeNode.Children.Select(c => c.Name).ToList();
        Assert.Contains("ProcessAsync", methods);
        Assert.Contains("GetOrder", methods);
    }

    [Fact]
    public void BuildTree_MaxDepth1_ReturnsOnlyFiles()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService { }")
            {
                SymbolName = "OrderService",
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 1);

        // Assert
        var fileNode = result.Nodes[0];
        Assert.Null(fileNode.Children);
    }

    [Fact]
    public void BuildTree_DetailMax_IncludesSignatures()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService : IOrderService { }")
            {
                SymbolName = "OrderService",
                Signature = "public class OrderService : IOrderService",
                StartLine = 5,
                EndLine = 50,
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 2, detail: TreeDetail.Max);

        // Assert
        var typeNode = result.Nodes[0].Children![0];
        Assert.Equal("public class OrderService : IOrderService", typeNode.Signature);
    }

    [Fact]
    public void BuildTree_DetailMin_ExcludesSignatures()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService : IOrderService { }")
            {
                SymbolName = "OrderService",
                Signature = "public class OrderService : IOrderService",
                StartLine = 5,
                EndLine = 50,
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 2, detail: TreeDetail.Min);

        // Assert
        var typeNode = result.Nodes[0].Children![0];
        Assert.Null(typeNode.Signature);
    }

    [Fact]
    public void GetNode_ValidNodeId_ReturnsContent()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService { /* full content */ }")
            {
                SymbolName = "OrderService",
                Signature = "public class OrderService",
                Language = "csharp",
                StartLine = 5,
                EndLine = 50,
            }
        };

        // Act
        var result = _sut.GetNode(chunks, "type:src/Services/OrderService.cs:OrderService");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("OrderService", result.Name);
        Assert.Equal("type", result.Type);
        Assert.Equal("src/Services/OrderService.cs", result.Path);
        Assert.Contains("full content", result.Content);
        Assert.Equal(5, result.LineStart);
        Assert.Equal(50, result.LineEnd);
        Assert.Equal("csharp", result.Metadata?.Language);
    }

    [Fact]
    public void GetNode_InvalidNodeId_ReturnsNull()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "public class OrderService { }")
            {
                SymbolName = "OrderService",
            }
        };

        // Act
        var result = _sut.GetNode(chunks, "type:src/Services/NotFound.cs:NotFound");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetNode_FileNodeId_ReturnsFileContent()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "header", "using System;\nnamespace Aura;")
            {
                Title = "File Header",
            },
            new("src/Services/OrderService.cs", "type", "public class OrderService { }")
            {
                SymbolName = "OrderService",
            }
        };

        // Act
        var result = _sut.GetNode(chunks, "file:src/Services/OrderService.cs:");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("OrderService.cs", result.Name);
        Assert.Equal("file", result.Type);
        Assert.Contains("using System", result.Content);
    }

    [Fact]
    public void BuildTree_MultipleFiles_GroupsByFolder()
    {
        // Arrange
        var chunks = new List<TreeChunk>
        {
            new("src/Services/OrderService.cs", "type", "class OrderService { }")
            {
                SymbolName = "OrderService",
            },
            new("src/Services/CustomerService.cs", "type", "class CustomerService { }")
            {
                SymbolName = "CustomerService",
            },
            new("src/Models/Order.cs", "type", "class Order { }")
            {
                SymbolName = "Order",
            }
        };

        // Act
        var result = _sut.BuildTree(chunks, maxDepth: 2);

        // Assert
        Assert.Equal(3, result.Nodes.Count);
        var files = result.Nodes.Select(n => n.Name).ToList();
        Assert.Contains("OrderService.cs", files);
        Assert.Contains("CustomerService.cs", files);
        Assert.Contains("Order.cs", files);
    }
}
