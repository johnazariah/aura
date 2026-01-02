// <copyright file="DeveloperAgentProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides hardcoded agents for the Developer module.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DeveloperAgentProvider"/> class.
/// </remarks>
/// <param name="loggerFactory">Logger factory.</param>
/// <param name="llmRegistry">LLM provider registry.</param>
/// <param name="reactExecutor">ReAct executor for agentic workflows.</param>
/// <param name="toolRegistry">Tool registry for accessing tools.</param>
public sealed class DeveloperAgentProvider(
    ILoggerFactory loggerFactory,
    ILlmProviderRegistry llmRegistry,
    IReActExecutor reactExecutor,
    IToolRegistry toolRegistry) : IHardcodedAgentProvider
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILlmProviderRegistry _llmRegistry = llmRegistry;
    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly IToolRegistry _toolRegistry = toolRegistry;

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

        // C# - Sophisticated Roslyn coding agent with agentic tool use
        // Uses Roslyn compiler APIs for semantic analysis - cannot be config-based
        yield return new RoslynCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
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
