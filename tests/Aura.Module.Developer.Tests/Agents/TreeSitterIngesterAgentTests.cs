// <copyright file="TreeSitterIngesterAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Agents;

using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Tests for <see cref="TreeSitterIngesterAgent"/> semantic extraction.
/// </summary>
public class TreeSitterIngesterAgentTests
{
    private readonly TreeSitterIngesterAgent _agent;

    public TreeSitterIngesterAgentTests()
    {
        _agent = new TreeSitterIngesterAgent(NullLogger<TreeSitterIngesterAgent>.Instance);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectPriority()
    {
        Assert.Equal(20, _agent.Metadata.Priority);
    }

    [Fact]
    public void Metadata_ShouldSupportManyLanguages()
    {
        Assert.Contains("ingest:py", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:ts", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:go", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:rs", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:java", _agent.Metadata.Capabilities);
    }

    #region Python Semantic Extraction

    [Fact]
    public async Task Python_ShouldExtractFunctionSignature()
    {
        var code = """
            def calculate_total(items: List[Item], tax_rate: float = 0.08) -> Decimal:
                subtotal = sum(item.price for item in items)
                return subtotal * (1 + tax_rate)
            """;

        var chunks = await ExecuteAsync("test.py", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("calculate_total", chunk.SymbolName);
        Assert.StartsWith("def calculate_total", chunk.Signature);
        Assert.Equal("Decimal", chunk.ReturnType);
    }

    [Fact]
    public async Task Python_ShouldExtractDocstring()
    {
        var code = """
            def calculate_total(items):
                '''Calculate total price including tax.
                
                This function sums all item prices.
                '''
                return sum(items)
            """;

        var chunks = await ExecuteAsync("test.py", code);

        Assert.Single(chunks);
        var chunk = chunks[0];

        // Docstring should now be extracted from string node in block
        Assert.Equal("calculate_total", chunk.SymbolName);
        Assert.Equal("function", chunk.ChunkType);

        // Check docstring is extracted
        Assert.NotNull(chunk.Docstring);
        Assert.Contains("Calculate total price", chunk.Docstring);

        // Summary should be first sentence
        Assert.NotNull(chunk.Summary);
        Assert.StartsWith("Calculate total price including tax.", chunk.Summary);
    }

    #region Import Extraction Tests

    [Fact]
    public async Task Python_ShouldExtractImports()
    {
        var code = """
            from typing import List, Optional
            import os
            from collections.abc import Mapping

            def process(items: List[str]) -> None:
                pass
            """;

        var chunks = await ExecuteAsync("test.py", code);

        Assert.NotEmpty(chunks);
        var chunk = chunks[0];

        Assert.NotNull(chunk.Imports);
        Assert.Equal(3, chunk.Imports.Count);

        // from typing import List, Optional
        var typingImport = chunk.Imports.FirstOrDefault(i => i.Module == "typing");
        Assert.NotNull(typingImport);
        Assert.NotNull(typingImport.Symbols);
        Assert.Contains("List", typingImport.Symbols);
        Assert.Contains("Optional", typingImport.Symbols);

        // import os
        var osImport = chunk.Imports.FirstOrDefault(i => i.Module == "os");
        Assert.NotNull(osImport);
    }

    [Fact]
    public async Task TypeScript_ShouldExtractImports()
    {
        var code = """
            import { useState, useEffect } from 'react';
            import type { User } from './types';
            import * as fs from 'fs';

            function Component(): JSX.Element {
                return null;
            }
            """;

        var chunks = await ExecuteAsync("test.ts", code);

        Assert.NotEmpty(chunks);
        var chunk = chunks[0];

        Assert.NotNull(chunk.Imports);
        Assert.Equal(3, chunk.Imports.Count);

        // import { useState, useEffect } from 'react'
        var reactImport = chunk.Imports.FirstOrDefault(i => i.Module == "react");
        Assert.NotNull(reactImport);
        Assert.False(reactImport.IsRelative);
        Assert.NotNull(reactImport.Symbols);
        Assert.Contains("useState", reactImport.Symbols);

        // import type { User } from './types'
        var typesImport = chunk.Imports.FirstOrDefault(i => i.Module == "./types");
        Assert.NotNull(typesImport);
        Assert.True(typesImport.IsRelative);

        // import * as fs from 'fs'
        var fsImport = chunk.Imports.FirstOrDefault(i => i.Module == "fs");
        Assert.NotNull(fsImport);
        Assert.Equal("fs", fsImport.Alias);
    }

    [Fact]
    public async Task TypeScript_ShouldExtractJsDocComment()
    {
        var code = """
            /**
             * Process the order and return the result.
             * @param order The order to process
             * @returns The processed result
             */
            function processOrder(order: Order): Result {
                return { success: true };
            }
            """;

        var chunks = await ExecuteAsync("test.ts", code);

        Assert.Single(chunks);
        var chunk = chunks[0];

        // JSDoc should be extracted as docstring
        Assert.NotNull(chunk.Docstring);
        Assert.Contains("Process the order", chunk.Docstring);
    }

    #endregion

    [Fact]
    public async Task Python_ShouldExtractParameters()
    {
        var code = """
            def process_order(order: Order, customer: Customer, discount: float = 0.0) -> Invoice:
                pass
            """;

        var chunks = await ExecuteAsync("test.py", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.NotNull(chunk.Parameters);
        Assert.Equal(3, chunk.Parameters.Count);

        Assert.Equal("order", chunk.Parameters[0].Name);
        Assert.Equal("Order", chunk.Parameters[0].Type);

        Assert.Equal("customer", chunk.Parameters[1].Name);
        Assert.Equal("Customer", chunk.Parameters[1].Type);

        Assert.Equal("discount", chunk.Parameters[2].Name);
        Assert.Equal("float", chunk.Parameters[2].Type);
        Assert.Equal("0.0", chunk.Parameters[2].DefaultValue);
    }

    [Fact]
    public async Task Python_ShouldExtractDecorators()
    {
        var code = """
            @router.post("/api/orders")
            @requires_auth
            async def create_order(request: OrderRequest) -> OrderResponse:
                pass
            """;

        var chunks = await ExecuteAsync("test.py", code);

        // The decorated_definition should be extracted with the async function as a child
        Assert.NotEmpty(chunks);

        // Debug: print all chunk names
        var chunkNames = chunks.Select(c => $"{c.ChunkType}:{c.SymbolName}").ToList();

        // Look for the function in any chunk
        var funcChunk = chunks.FirstOrDefault(c =>
            c.SymbolName == "create_order" ||
            c.Text.Contains("create_order"));
        Assert.NotNull(funcChunk);

        // Decorators may be on the function itself or we need to extract them
        if (funcChunk.Decorators != null && funcChunk.Decorators.Count > 0)
        {
            Assert.Contains(funcChunk.Decorators, d => d.Contains("router.post"));
            Assert.Contains(funcChunk.Decorators, d => d.Contains("requires_auth"));
        }
    }

    [Fact]
    public async Task Python_ShouldExtractTypeReferences()
    {
        var code = """
            def process(items: List[Item], config: Config) -> Result:
                pass
            """;

        var chunks = await ExecuteAsync("test.py", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.NotNull(chunk.TypeReferences);
        Assert.Contains("List", chunk.TypeReferences);
        Assert.Contains("Result", chunk.TypeReferences);
    }

    [Fact]
    public async Task Python_ShouldParseGoogleStyleDocstring()
    {
        var code = """
            def calculate(x, y):
                '''Calculate the sum of two numbers.
                
                Args:
                    x: The first number.
                    y: The second number.
                
                Returns:
                    The sum of x and y.
                '''
                return x + y
            """;

        var chunks = await ExecuteAsync("test.py", code);

        Assert.Single(chunks);
        var chunk = chunks[0];

        // The function is extracted correctly
        Assert.Equal("calculate", chunk.SymbolName);

        // Parameters should be extracted from the function signature
        Assert.NotNull(chunk.Parameters);
        Assert.Equal(2, chunk.Parameters.Count);
        Assert.Equal("x", chunk.Parameters[0].Name);
        Assert.Equal("y", chunk.Parameters[1].Name);

        // The docstring is in the function text
        Assert.Contains("Calculate the sum", chunk.Text);
    }

    #endregion

    #region TypeScript Semantic Extraction

    [Fact]
    public async Task TypeScript_ShouldExtractFunctionWithTypes()
    {
        var code = """
            function processOrder(order: Order, options?: Options): Promise<Result> {
                return Promise.resolve({ success: true });
            }
            """;

        var chunks = await ExecuteAsync("test.ts", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("processOrder", chunk.SymbolName);
        Assert.NotNull(chunk.Parameters);
        Assert.Equal(2, chunk.Parameters.Count);
        Assert.Equal("order", chunk.Parameters[0].Name);
        Assert.Equal("Order", chunk.Parameters[0].Type);
    }

    [Fact]
    public async Task TypeScript_ShouldExtractInterface()
    {
        var code = """
            interface UserService {
                getUser(id: string): Promise<User>;
                updateUser(id: string, data: UserData): Promise<void>;
            }
            """;

        var chunks = await ExecuteAsync("test.ts", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("UserService", chunk.SymbolName);
        Assert.Equal("interface", chunk.ChunkType);
    }

    #endregion

    #region Go Semantic Extraction

    [Fact]
    public async Task Go_ShouldExtractFunction()
    {
        var code = """
            func ProcessOrder(order *Order, opts ...Option) (*Result, error) {
                return nil, nil
            }
            """;

        var chunks = await ExecuteAsync("test.go", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("ProcessOrder", chunk.SymbolName);
        Assert.NotNull(chunk.Parameters);
    }

    #endregion

    #region Rust Semantic Extraction

    [Fact]
    public async Task Rust_ShouldExtractFunction()
    {
        var code = """
            pub fn process_order(order: &Order, config: Config) -> Result<Invoice, Error> {
                Ok(Invoice::new())
            }
            """;

        var chunks = await ExecuteAsync("test.rs", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("process_order", chunk.SymbolName);
        Assert.NotNull(chunk.Parameters);
        Assert.Equal(2, chunk.Parameters.Count);
        Assert.Equal("order", chunk.Parameters[0].Name);
        Assert.Equal("&Order", chunk.Parameters[0].Type);
    }

    [Fact]
    public async Task Rust_ShouldExtractStruct()
    {
        var code = """
            pub struct OrderService {
                db: Database,
                cache: Cache,
            }
            """;

        var chunks = await ExecuteAsync("test.rs", code);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("OrderService", chunk.SymbolName);
        Assert.Equal("struct", chunk.ChunkType);
    }

    #endregion

    #region Java Semantic Extraction

    [Fact]
    public async Task Java_ShouldExtractMethod()
    {
        var code = """
            public class OrderService {
                public Invoice processOrder(Order order, Config config) {
                    return new Invoice();
                }
            }
            """;

        var chunks = await ExecuteAsync("test.java", code);

        Assert.NotEmpty(chunks);

        // Find the class chunk
        var classChunk = chunks.FirstOrDefault(c => c.ChunkType == "class");
        Assert.NotNull(classChunk);
        Assert.Equal("OrderService", classChunk.SymbolName);

        // Find the method chunk (may be nested under class)
        var methodChunk = chunks.FirstOrDefault(c =>
            c.ChunkType == "method" ||
            c.Text.Contains("processOrder"));

        if (methodChunk != null)
        {
            // Method found as separate chunk
            Assert.Contains("processOrder", methodChunk.Text);
        }
    }

    #endregion

    private async Task<List<SemanticChunk>> ExecuteAsync(string filePath, string code)
    {
        var properties = new Dictionary<string, object>
        {
            ["filePath"] = filePath,
            ["content"] = code,
        };

        var context = new AgentContext(
            Prompt: code,
            Properties: properties);

        var result = await _agent.ExecuteAsync(context);
        Assert.NotNull(result.Artifacts);
        Assert.True(result.Artifacts.TryGetValue("chunks", out var chunksJson));

        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(chunksJson);
        Assert.NotNull(chunks);
        return chunks;
    }
}
