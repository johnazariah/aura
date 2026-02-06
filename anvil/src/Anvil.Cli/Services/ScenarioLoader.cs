using System.IO.Abstractions;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Anvil.Cli.Services;

/// <summary>
/// Loads and parses YAML scenario files.
/// </summary>
public sealed class ScenarioLoader(
    IFileSystem fileSystem,
    ILogger<ScenarioLoader> logger) : IScenarioLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Scenario>> LoadAllAsync(string scenariosPath, CancellationToken ct = default)
    {
        // If it's a file, load just that file
        if (fileSystem.File.Exists(scenariosPath))
        {
            var scenario = await LoadAsync(scenariosPath, ct);
            logger.LogInformation("Loaded 1 scenario from {Path}", scenariosPath);
            return [scenario];
        }

        if (!fileSystem.Directory.Exists(scenariosPath))
        {
            logger.LogWarning("Scenarios directory not found: {Path}", scenariosPath);
            return [];
        }

        var yamlFiles = fileSystem.Directory
            .GetFiles(scenariosPath, "*.yaml", SearchOption.AllDirectories)
            .Concat(fileSystem.Directory.GetFiles(scenariosPath, "*.yml", SearchOption.AllDirectories))
            .ToList();

        if (yamlFiles.Count == 0)
        {
            logger.LogWarning("No scenario files found in {Path}", scenariosPath);
            return [];
        }

        var scenarios = new List<Scenario>();
        foreach (var file in yamlFiles)
        {
            ct.ThrowIfCancellationRequested();
            var scenario = await LoadAsync(file, ct);
            scenarios.Add(scenario);
        }

        logger.LogInformation("Loaded {Count} scenarios from {Path}", scenarios.Count, scenariosPath);
        return scenarios;
    }

    /// <inheritdoc />
    public async Task<Scenario> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!fileSystem.File.Exists(filePath))
        {
            throw new ScenarioNotFoundException(filePath);
        }

        string yaml;
        try
        {
            yaml = await fileSystem.File.ReadAllTextAsync(filePath, ct);
        }
        catch (Exception ex) when (ex is not ScenarioNotFoundException)
        {
            throw new ScenarioParseException(filePath, ex);
        }

        ScenarioDto dto;
        try
        {
            dto = _deserializer.Deserialize<ScenarioDto>(yaml);
        }
        catch (Exception ex)
        {
            throw new ScenarioParseException(filePath, ex);
        }

        var errors = ValidateDto(dto);
        if (errors.Count > 0)
        {
            throw new ScenarioValidationException(filePath, errors);
        }

        var scenario = MapToScenario(dto, filePath);
        logger.LogDebug("Loaded scenario '{Name}' from {Path}", scenario.Name, filePath);
        return scenario;
    }

    private static List<string> ValidateDto(ScenarioDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("'name' is required");
        if (string.IsNullOrWhiteSpace(dto.Description))
            errors.Add("'description' is required");
        if (string.IsNullOrWhiteSpace(dto.Language))
            errors.Add("'language' is required");
        if (string.IsNullOrWhiteSpace(dto.Repository))
            errors.Add("'repository' is required");
        if (dto.Story is null)
            errors.Add("'story' is required");
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Story.Title))
                errors.Add("'story.title' is required");
            if (string.IsNullOrWhiteSpace(dto.Story.Description))
                errors.Add("'story.description' is required");
        }
        if (dto.Expectations is null || dto.Expectations.Count == 0)
            errors.Add("'expectations' must contain at least one expectation");

        return errors;
    }

    private static Scenario MapToScenario(ScenarioDto dto, string filePath)
    {
        return new Scenario
        {
            Name = dto.Name!,
            Description = dto.Description!,
            Language = dto.Language!,
            Repository = dto.Repository!,
            Story = new StoryDefinition
            {
                Title = dto.Story!.Title!,
                Description = dto.Story.Description!
            },
            Expectations = dto.Expectations!
                .Select(e => new Expectation
                {
                    Type = e.Type ?? "compiles",
                    Description = e.Description ?? "",
                    Path = e.Path,
                    Pattern = e.Pattern,
                    MinAuraToolRatio = e.MinAuraToolRatio,
                    MaxStepsToTarget = e.MaxStepsToTarget
                })
                .ToList(),
            TimeoutSeconds = dto.TimeoutSeconds ?? 300,
            Executor = dto.Executor,
            Tags = dto.Tags ?? [],
            FilePath = filePath
        };
    }

    /// <summary>
    /// Internal DTO for YAML deserialization.
    /// </summary>
    private sealed class ScenarioDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Language { get; set; }
        public string? Repository { get; set; }
        public StoryDto? Story { get; set; }
        public List<ExpectationDto>? Expectations { get; set; }
        public int? TimeoutSeconds { get; set; }
        public string? Executor { get; set; }
        public List<string>? Tags { get; set; }
    }

    private sealed class StoryDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    private sealed class ExpectationDto
    {
        public string? Type { get; set; }
        public string? Description { get; set; }
        public string? Path { get; set; }
        public string? Pattern { get; set; }
        public double? MinAuraToolRatio { get; set; }
        public int? MaxStepsToTarget { get; set; }
    }
}
