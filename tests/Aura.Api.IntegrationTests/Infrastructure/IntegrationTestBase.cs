// <copyright file="IntegrationTestBase.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that provides access to the HTTP client and factory.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationTestBase"/> class.
    /// </summary>
    /// <param name="factory">The API factory.</param>
    protected IntegrationTestBase(AuraApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Gets the API factory.
    /// </summary>
    protected AuraApiFactory Factory { get; }

    /// <summary>
    /// Gets the HTTP client for making requests.
    /// </summary>
    protected HttpClient Client { get; }

    /// <inheritdoc/>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}
