// <copyright file="AgentMetadataKeys.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Constants for agent metadata keys parsed from markdown frontmatter.
/// These correspond to the YAML-like fields in agent definition files.
/// </summary>
public static class AgentMetadataKeys
{
    /// <summary>Display name of the agent.</summary>
    public const string Name = "name";

    /// <summary>Description of the agent's purpose.</summary>
    public const string Description = "description";

    /// <summary>LLM provider to use (e.g., "ollama", "openai").</summary>
    public const string Provider = "provider";

    /// <summary>Model name to use.</summary>
    public const string Model = "model";

    /// <summary>Temperature setting for LLM generation.</summary>
    public const string Temperature = "temperature";

    /// <summary>Priority for agent selection (lower = higher priority).</summary>
    public const string Priority = "priority";

    /// <summary>Whether reflection is enabled.</summary>
    public const string Reflection = "reflection";

    /// <summary>Custom prompt for reflection.</summary>
    public const string ReflectionPrompt = "reflectionprompt";

    /// <summary>Model to use for reflection.</summary>
    public const string ReflectionModel = "reflectionmodel";
}
