// <copyright file="IntegrationTestBase.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that provides access to the HTTP client and factory.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IntegrationTestBase"/> class.
/// </remarks>
/// <param name="factory">The API factory.</param>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
public abstract class IntegrationTestBase(AuraApiFactory factory) : IAsyncLifetime
{

    /// <summary>
    /// Gets the API factory.
    /// </summary>
    protected AuraApiFactory Factory { get; } = factory;

    /// <summary>
    /// Gets the HTTP client for making requests.
    /// </summary>
    protected HttpClient Client { get; } = factory.CreateClient();

    /// <inheritdoc/>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}
