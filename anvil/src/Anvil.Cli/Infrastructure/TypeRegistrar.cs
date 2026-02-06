using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Anvil.Cli.Infrastructure;

/// <summary>
/// Type resolver that uses Microsoft.Extensions.DependencyInjection.
/// </summary>
public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        return provider.GetService(type);
    }

    public void Dispose()
    {
        if (provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Type registrar that uses Microsoft.Extensions.DependencyInjection.
/// </summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build()
    {
        return new TypeResolver(services.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        services.AddSingleton(service, _ => factory());
    }
}
