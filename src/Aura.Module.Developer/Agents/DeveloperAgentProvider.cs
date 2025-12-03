// <copyright file="DeveloperAgentProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides hardcoded agents for the Developer module.
/// </summary>
public sealed class DeveloperAgentProvider : IHardcodedAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly IPromptRegistry _promptRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperAgentProvider"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="llmRegistry">LLM provider registry.</param>
    /// <param name="roslynService">Roslyn workspace service.</param>
    /// <param name="reactExecutor">ReAct executor for agentic workflows.</param>
    /// <param name="toolRegistry">Tool registry for accessing tools.</param>
    /// <param name="promptRegistry">Prompt registry for template rendering.</param>
    public DeveloperAgentProvider(
        ILoggerFactory loggerFactory,
        ILlmProviderRegistry llmRegistry,
        IRoslynWorkspaceService roslynService,
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        IPromptRegistry promptRegistry)
    {
        _loggerFactory = loggerFactory;
        _llmRegistry = llmRegistry;
        _roslynService = roslynService;
        _reactExecutor = reactExecutor;
        _toolRegistry = toolRegistry;
        _promptRegistry = promptRegistry;
    }

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
        yield return new RoslynCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            _loggerFactory.CreateLogger<RoslynCodingAgent>());

        // F# - FSharp.Compiler.Service agent with functional programming focus
        yield return new FSharpCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            _promptRegistry,
            _loggerFactory.CreateLogger<FSharpCodingAgent>());

        // Python - ReAct agent with Python-specific tools
        yield return new PythonCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            _loggerFactory.CreateLogger<PythonCodingAgent>());

        // TypeScript/JavaScript - ReAct agent with TS-specific tools
        yield return new TypeScriptCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            _loggerFactory.CreateLogger<TypeScriptCodingAgent>());

        // Go - ReAct agent with Go-specific tools
        yield return new GoCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            _loggerFactory.CreateLogger<GoCodingAgent>());

        // Note: F#, Python, TypeScript, Go can be migrated to YAML-configured
        // LanguageSpecialistAgents. The hardcoded agents remain for now but
        // their functionality is duplicated in agents/languages/*.yaml.
        //
        // C# uses RoslynCodingAgent (above) which needs Roslyn compiler APIs
        // and cannot be expressed as a simple YAML config.
    }
}
