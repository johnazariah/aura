// <copyright file="FoundationAgentProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides foundation-level hardcoded agents.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FoundationAgentProvider"/> class.
/// </remarks>
/// <param name="loggerFactory">Logger factory.</param>
public sealed class FoundationAgentProvider(ILoggerFactory loggerFactory) : IHardcodedAgentProvider
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    /// <inheritdoc/>
    public IEnumerable<IAgent> GetAgents()
    {
        // Text ingester for markdown, plain text, etc.
        yield return new TextIngesterAgent(
            _loggerFactory.CreateLogger<TextIngesterAgent>());

        // The fallback ingester is the last resort for any file type
        yield return new FallbackIngesterAgent(
            _loggerFactory.CreateLogger<FallbackIngesterAgent>());
    }
}
