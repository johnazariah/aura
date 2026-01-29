// <copyright file="AuraDbContextFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for <see cref="AuraDbContext"/>.
/// Used by EF Core tools for migrations.
/// </summary>
public sealed class AuraDbContextFactory : IDesignTimeDbContextFactory<AuraDbContext>
{
    /// <inheritdoc/>
    public AuraDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuraDbContext>();

        // Use a placeholder connection string for design-time operations
        // pgvector extension must be available in the database
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5433;Database=aura_dev;Username=postgres;Password=postgres",
            o => o.UseVector());

        return new AuraDbContext(optionsBuilder.Options);
    }
}
