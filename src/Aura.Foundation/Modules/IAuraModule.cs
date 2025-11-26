namespace Aura.Foundation.Modules;

using Aura.Foundation.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Contract for composable Aura modules.
/// Modules are independent and depend only on the Foundation.
/// </summary>
public interface IAuraModule
{
    /// <summary>
    /// Unique identifier for this module (e.g., "developer", "research").
    /// </summary>
    string ModuleId { get; }
    
    /// <summary>
    /// Human-readable name.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what this module provides.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Other modules this depends on. Empty means only Foundation is required.
    /// Modules should generally have no dependencies on other modules.
    /// </summary>
    IReadOnlyList<string> Dependencies => [];
    
    /// <summary>
    /// Register services with the DI container.
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    
    /// <summary>
    /// Register agents from this module with the agent registry.
    /// </summary>
    void RegisterAgents(IAgentRegistry registry, IConfiguration configuration);
}
