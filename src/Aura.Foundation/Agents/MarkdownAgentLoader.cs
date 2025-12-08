// <copyright file="MarkdownAgentLoader.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Loads agents from markdown files following the Aura agent format.
/// </summary>
public sealed partial class MarkdownAgentLoader : IAgentLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<MarkdownAgentLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownAgentLoader"/> class.
    /// </summary>
    /// <param name="fileSystem">File system abstraction.</param>
    /// <param name="agentFactory">Factory to create agents from definitions.</param>
    /// <param name="logger">Logger instance.</param>
    public MarkdownAgentLoader(
        IFileSystem fileSystem,
        IAgentFactory agentFactory,
        ILogger<MarkdownAgentLoader> logger)
    {
        _fileSystem = fileSystem;
        _agentFactory = agentFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IAgent?> LoadAsync(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("Agent file does not exist: {Path}", filePath);
            return null;
        }

        var content = await _fileSystem.File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var agentId = _fileSystem.Path.GetFileNameWithoutExtension(filePath);

        var definition = Parse(agentId, content);
        if (definition is null)
        {
            _logger.LogWarning("Failed to parse agent from {Path}", filePath);
            return null;
        }

        _logger.LogDebug("Loaded agent {AgentId} from {Path}", definition.AgentId, filePath);
        return _agentFactory.CreateAgent(definition);
    }

    /// <summary>
    /// Parses agent definition from markdown content.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="content">The markdown content.</param>
    /// <returns>Parsed definition or null if invalid.</returns>
    public AgentDefinition? Parse(string agentId, string content)
    {
        try
        {
            // Extract sections
            var metadataSection = ExtractSection(content, "Metadata");
            var capabilitiesSection = ExtractSection(content, "Capabilities");
            var languagesSection = ExtractSection(content, "Languages");
            var tagsSection = ExtractSection(content, "Tags");
            var toolsSection = ExtractSection(content, "Tools Available");
            var systemPromptSection = ExtractSection(content, "System Prompt");

            if (metadataSection is null || systemPromptSection is null)
            {
                _logger.LogWarning("Agent {AgentId} missing required sections (Metadata, System Prompt)", agentId);
                return null;
            }

            // Parse metadata
            var metadata = ParseMetadata(metadataSection);
            var name = metadata.GetValueOrDefault("name", agentId);
            var description = metadata.GetValueOrDefault("description", string.Empty);
            var provider = metadata.GetValueOrDefault("provider", AgentDefinition.DefaultProvider);
            var model = metadata.TryGetValue("model", out var m) ? m : AgentDefinition.DefaultModel;
            var temperatureStr = metadata.GetValueOrDefault("temperature", AgentDefinition.DefaultTemperature.ToString(CultureInfo.InvariantCulture));
            var priorityStr = metadata.GetValueOrDefault("priority", AgentDefinition.DefaultPriority.ToString(CultureInfo.InvariantCulture));

            if (!double.TryParse(temperatureStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature))
            {
                temperature = AgentDefinition.DefaultTemperature;
            }

            if (!int.TryParse(priorityStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
            {
                priority = AgentDefinition.DefaultPriority;
            }

            // Parse capabilities (list items)
            var capabilities = ParseListItems(capabilitiesSection ?? string.Empty);

            // Validate capabilities against fixed vocabulary
            foreach (var cap in capabilities)
            {
                if (!Capabilities.IsValid(cap))
                {
                    _logger.LogWarning("Agent {AgentId} has unknown capability: {Capability}. Valid capabilities: {ValidCapabilities}",
                        agentId, cap, string.Join(", ", Capabilities.All));
                }
            }

            // Parse languages (list items, empty = polyglot)
            var languages = ParseListItems(languagesSection ?? string.Empty);

            // Parse tags (list items, open vocabulary for user filtering)
            var tags = ParseListItems(tagsSection ?? string.Empty);

            // Parse tools (extract tool names from the section)
            var tools = ParseToolNames(toolsSection ?? string.Empty);

            return new AgentDefinition(
                AgentId: agentId,
                Name: name,
                Description: description,
                Provider: provider,
                Model: model,
                Temperature: temperature,
                SystemPrompt: systemPromptSection.Trim(),
                Capabilities: capabilities,
                Priority: priority,
                Languages: languages,
                Tags: tags,
                Tools: tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing agent {AgentId}", agentId);
            return null;
        }
    }

    private static string? ExtractSection(string content, string sectionName)
    {
        // Match ## Section Name and capture until next ## or end
        var pattern = $@"##\s+{Regex.Escape(sectionName)}\s*\n(.*?)(?=\n##\s|\z)";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static Dictionary<string, string> ParseMetadata(string section)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Match lines like: - **Key**: Value or - **Key**: `Value`
        var lines = section.Split('\n');
        foreach (var line in lines)
        {
            var match = MetadataLineRegex().Match(line);
            if (match.Success)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim().Trim('`');
                metadata[key] = value;
            }
        }

        return metadata;
    }

    private static List<string> ParseListItems(string section)
    {
        var items = new List<string>();

        // Match lines starting with - or *
        var lines = section.Split('\n');
        foreach (var line in lines)
        {
            var match = ListItemRegex().Match(line);
            if (match.Success)
            {
                var item = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    items.Add(item);
                }
            }
        }

        return items;
    }

    private static List<string> ParseToolNames(string section)
    {
        var tools = new List<string>();

        // Match tool function signatures like: **tool_name(...)** or tool_name(
        var matches = ToolNameRegex().Matches(section);
        foreach (Match match in matches)
        {
            var toolName = match.Groups[1].Value;
            if (!tools.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            {
                tools.Add(toolName);
            }
        }

        return tools;
    }

    [GeneratedRegex(@"^\s*-\s*\*\*([^*]+)\*\*:\s*(.+)$")]
    private static partial Regex MetadataLineRegex();

    [GeneratedRegex(@"^\s*[-*]\s+(.+)$")]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"\*\*(\w+)\s*\(")]
    private static partial Regex ToolNameRegex();
}
