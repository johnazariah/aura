namespace Aura.Foundation.Modules;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Discovers and loads Aura modules.
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// Get all discovered modules.
    /// </summary>
    IReadOnlyList<IAuraModule> GetAllModules();

    /// <summary>
    /// Get a module by its ID.
    /// </summary>
    IAuraModule? GetModule(string moduleId);

    /// <summary>
    /// Get modules that are enabled in configuration.
    /// </summary>
    IReadOnlyList<IAuraModule> GetEnabledModules(IConfiguration configuration);
}

/// <summary>
/// Default implementation that discovers modules via assembly scanning.
/// </summary>
public class ModuleLoader : IModuleLoader
{
    private readonly Dictionary<string, IAuraModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ModuleLoader> _logger;

    public ModuleLoader(IEnumerable<IAuraModule> modules, ILogger<ModuleLoader> logger)
    {
        _logger = logger;

        foreach (var module in modules)
        {
            if (_modules.TryAdd(module.ModuleId, module))
            {
                _logger.LogInformation("Registered module: {ModuleId} ({Name})",
                    module.ModuleId, module.Name);
            }
            else
            {
                _logger.LogWarning("Duplicate module ID: {ModuleId}, skipping", module.ModuleId);
            }
        }
    }

    public IReadOnlyList<IAuraModule> GetAllModules() =>
        _modules.Values.ToList();

    public IAuraModule? GetModule(string moduleId) =>
        _modules.GetValueOrDefault(moduleId);

    public IReadOnlyList<IAuraModule> GetEnabledModules(IConfiguration configuration)
    {
        var enabledIds = configuration
            .GetSection("Aura:Modules:Enabled")
            .Get<string[]>() ?? ["developer"];

        var enabled = new List<IAuraModule>();

        foreach (var moduleId in enabledIds)
        {
            if (_modules.TryGetValue(moduleId, out var module))
            {
                enabled.Add(module);
                _logger.LogInformation("Enabling module: {ModuleId}", moduleId);
            }
            else
            {
                _logger.LogWarning("Module '{ModuleId}' not found, skipping", moduleId);
            }
        }

        return enabled;
    }
}
