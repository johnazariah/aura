// <copyright file="IPromptRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Prompts;

/// <summary>
/// Registry for loading and rendering prompt templates.
/// </summary>
public interface IPromptRegistry
{
    /// <summary>
    /// Gets a prompt template by name.
    /// </summary>
    /// <param name="name">The prompt name (e.g., "workflow-planning").</param>
    /// <returns>The prompt template, or null if not found.</returns>
    PromptTemplate? GetPrompt(string name);

    /// <summary>
    /// Renders a prompt with the given context.
    /// </summary>
    /// <param name="name">The prompt name.</param>
    /// <param name="context">The context object for template rendering.</param>
    /// <returns>The rendered prompt string.</returns>
    /// <exception cref="InvalidOperationException">If prompt not found.</exception>
    string Render(string name, object context);

    /// <summary>
    /// Gets the RAG queries defined for a prompt.
    /// </summary>
    /// <param name="name">The prompt name.</param>
    /// <returns>The list of RAG queries, or empty if none defined.</returns>
    IReadOnlyList<string> GetRagQueries(string name);

    /// <summary>
    /// Gets all registered prompt names.
    /// </summary>
    IReadOnlyList<string> GetPromptNames();

    /// <summary>
    /// Reloads all prompts from disk.
    /// </summary>
    void Reload();
}
