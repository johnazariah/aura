// <copyright file="ResearcherDbContextFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for creating ResearcherDbContext for migrations.
/// </summary>
public class ResearcherDbContextFactory : IDesignTimeDbContextFactory<ResearcherDbContext>
{
    /// <inheritdoc/>
    public ResearcherDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResearcherDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=aura;Username=aura;Password=aura",
            o => o.UseVector());

        return new ResearcherDbContext(optionsBuilder.Options);
    }
}
