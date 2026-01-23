// <copyright file="DeveloperDbContextFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for <see cref="DeveloperDbContext"/>.
/// Used by EF Core tools for migrations.
/// </summary>
public sealed class DeveloperDbContextFactory : IDesignTimeDbContextFactory<DeveloperDbContext>
{
    /// <inheritdoc/>
    public DeveloperDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DeveloperDbContext>();

        // Use a placeholder connection string for design-time operations
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=aura_dev;Username=postgres;Password=postgres");

        return new DeveloperDbContext(optionsBuilder.Options);
    }
}
