// <copyright file="IntegrationTestCollection.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Collection definition for integration tests that share the same API factory.
/// This ensures the PostgreSQL container is reused across tests in the collection.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<AuraApiFactory>
{
    /// <summary>
    /// The name of this test collection.
    /// </summary>
    public const string Name = "Integration";
}
