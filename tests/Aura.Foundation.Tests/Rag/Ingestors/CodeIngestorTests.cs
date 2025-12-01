// <copyright file="CodeIngestorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag.Ingestors;

using Aura.Foundation.Rag.Ingestors;
using FluentAssertions;
using Xunit;

public class CodeIngestorTests
{
    private readonly CodeIngestor _sut = new();

    [Theory]
    [InlineData(".cs", true)]
    [InlineData(".ts", true)]
    [InlineData(".tsx", true)]
    [InlineData(".js", true)]
    [InlineData(".py", true)]
    [InlineData(".go", true)]
    [InlineData(".rs", true)]
    [InlineData(".md", false)]
    [InlineData(".txt", false)]
    public void CanIngest_ReturnsCorrectResult(string extension, bool expected)
    {
        // Arrange
        var filePath = $"test{extension}";

        // Act
        var result = _sut.CanIngest(filePath);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task IngestAsync_CSharpFile_ExtractsHeader()
    {
        // Arrange
        var content = @"using System;
using System.Collections.Generic;

namespace MyApp
{
    public class Program
    {
        public static void Main()
        {
        }
    }
}";

        // Act
        var chunks = await _sut.IngestAsync("test.cs", content);

        // Assert
        chunks.Should().Contain(c => c.ChunkType == "header");
        var header = chunks.First(c => c.ChunkType == "header");
        header.Text.Should().Contain("using System");
        header.Text.Should().Contain("namespace");
    }

    [Fact]
    public async Task IngestAsync_CSharpClass_ExtractsType()
    {
        // Arrange
        var content = @"namespace MyApp;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
}";

        // Act
        var chunks = await _sut.IngestAsync("test.cs", content);

        // Assert
        chunks.Should().Contain(c => c.ChunkType == "type");
        chunks.Should().Contain(c => c.Title != null && c.Title.Contains("Calculator"));
    }

    [Fact]
    public async Task IngestAsync_TypeScriptFunction_ExtractsFunction()
    {
        // Arrange
        var content = @"import { useState } from 'react';

export function useCounter(initial: number) {
    const [count, setCount] = useState(initial);
    
    const increment = () => setCount(c => c + 1);
    const decrement = () => setCount(c => c - 1);
    
    return { count, increment, decrement };
}";

        // Act
        var chunks = await _sut.IngestAsync("test.ts", content);

        // Assert
        chunks.Should().Contain(c => c.Language == "typescript");
        chunks.Should().Contain(c => c.Title == "useCounter" || (c.Text != null && c.Text.Contains("useCounter")));
    }

    [Fact]
    public async Task IngestAsync_PythonFile_ExtractsImportsAndDefs()
    {
        // Arrange
        var content = @"import os
from typing import List

def hello(name: str) -> str:
    return f'Hello, {name}!'

def goodbye(name: str) -> str:
    return f'Goodbye, {name}!'

class Greeter:
    def greet(self, name):
        return hello(name)
";

        // Act
        var chunks = await _sut.IngestAsync("test.py", content);

        // Assert
        chunks.Should().Contain(c => c.Language == "python");
        chunks.Should().Contain(c => c.Title == "hello" || (c.Text != null && c.Text.Contains("def hello")));
    }

    [Fact]
    public async Task IngestAsync_LargeFile_ChunksGeneric()
    {
        // Arrange - Create content that exceeds chunk size
        var lines = Enumerable.Range(1, 200)
            .Select(i => $"// Line {i}: This is a comment to make the file larger")
            .ToList();
        var content = string.Join("\n", lines);

        // Act
        var chunks = await _sut.IngestAsync("test.go", content);

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.Language == "go");
    }

    [Fact]
    public async Task IngestAsync_AllChunksHaveLineNumbers()
    {
        // Arrange
        var content = @"using System;

public class Test
{
    public void Method1() { }
    public void Method2() { }
}";

        // Act
        var chunks = await _sut.IngestAsync("test.cs", content);

        // Assert
        chunks.Should().AllSatisfy(c =>
        {
            c.StartLine.Should().BePositive();
            c.EndLine.Should().BePositive();
        });
    }
}
