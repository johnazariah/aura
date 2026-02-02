// <copyright file="ReadingStatus.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

/// <summary>
/// Reading progress status for a source.
/// </summary>
public enum ReadingStatus
{
    /// <summary>Not yet started reading.</summary>
    ToRead,

    /// <summary>Currently reading.</summary>
    InProgress,

    /// <summary>Finished reading.</summary>
    Completed,

    /// <summary>Archived or no longer relevant.</summary>
    Archived,
}
