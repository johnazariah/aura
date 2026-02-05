// <copyright file="DeveloperAgentProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.IO.Abstractions;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides hardcoded agents for the Developer module.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DeveloperAgentProvider"/> class.
/// </remarks>
/// <param name="loggerFactory">Logger factory.</param>
/// <param name="llmRegistry">LLM provider registry.</param>
/// <param name="refactoringService">Roslyn refactoring service.</param>
/// <param name="workspaceService">Roslyn workspace service.</param>
/// <param name="promptRegistry">Prompt template registry.</param>
/// <param name="fileSystem">File system abstraction.</param>
public sealed class DeveloperAgentProvider(
    ILoggerFactory loggerFactory,
    ILlmProviderRegistry llmRegistry,
    IRoslynRefactoringService refactoringService,
    IRoslynWorkspaceService workspaceService,
    IPromptRegistry promptRegistry,
    IFileSystem fileSystem) : IHardcodedAgentProvider
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILlmProviderRegistry _llmRegistry = llmRegistry;
    private readonly IRoslynRefactoringService _refactoringService = refactoringService;
    private readonly IRoslynWorkspaceService _workspaceService = workspaceService;
    private readonly IPromptRegistry _promptRegistry = promptRegistry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public IEnumerable<IAgent> GetAgents()
    {
        // === Ingesters (for RAG indexing) ===

        // Priority 10: C# ingester using Roslyn - full semantic analysis
        yield return new CSharpIngesterAgent(
            _loggerFactory.CreateLogger<CSharpIngesterAgent>());

        // Priority 20: TreeSitter ingester - AST-based parsing for 30+ languages
        // Covers Python, TypeScript, JavaScript, Go, Rust, Java, C/C++, Ruby, etc.
        yield return new TreeSitterIngesterAgent(
            _loggerFactory.CreateLogger<TreeSitterIngesterAgent>());

        // === Specialist Coding Agents (Priority 10) ===

        // C# - Deterministic Roslyn coding agent that calls Roslyn services directly
        // Uses LLM to extract operations, then executes via Roslyn - guarantees semantic tool usage
        yield return new RoslynCodingAgent(
            _refactoringService,
            _workspaceService,
            _llmRegistry,
            _promptRegistry,
            _fileSystem,
            _loggerFactory.CreateLogger<RoslynCodingAgent>());

        // Note: F#, Python, TypeScript, Go, Rust now use config-based agents
        // defined in agents/languages/*.yaml. The hardcoded agents have been
        // removed to eliminate duplication. When LanguageSpecialistAgent is
        // implemented, it will load these YAML configs at runtime.
        //
        // For now, these languages fall back to the generic "coding-agent.md"
        // which handles multiple languages with appropriate tooling.
    }
}
