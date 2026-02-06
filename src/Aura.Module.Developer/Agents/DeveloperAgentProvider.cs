// <copyright file="DeveloperAgentProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using Aura.Foundation.Agents;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides hardcoded agents for the Developer module.
/// Currently provides ingester agents only. Coding agents are handled
/// by Copilot Chat via MCP tools.
/// </summary>
public sealed class DeveloperAgentProvider(
    ILoggerFactory loggerFactory) : IHardcodedAgentProvider
{
    /// <inheritdoc/>
    public IEnumerable<IAgent> GetAgents()
    {
        // C# ingester using Roslyn - full semantic analysis
        yield return new CSharpIngesterAgent(
            loggerFactory.CreateLogger<CSharpIngesterAgent>());

        // TreeSitter ingester - AST-based parsing for 30+ languages
        yield return new TreeSitterIngesterAgent(
            loggerFactory.CreateLogger<TreeSitterIngesterAgent>());
    }
}
