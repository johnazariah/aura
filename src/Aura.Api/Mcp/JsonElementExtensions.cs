// <copyright file="JsonElementExtensions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;

/// <summary>
/// Safe extension methods for extracting properties from <see cref="JsonElement"/>
/// without throwing <see cref="KeyNotFoundException"/>.
/// <para>
/// <c>JsonElement.GetProperty(name)</c> throws when the key is missing.
/// These helpers use <c>TryGetProperty</c> internally and return safe defaults.
/// </para>
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Gets a string property value, returning <paramref name="defaultValue"/> if the property
    /// is missing or null.
    /// </summary>
    public static string GetStringOrDefault(this JsonElement? element, string propertyName, string defaultValue = "")
    {
        if (element?.TryGetProperty(propertyName, out var prop) == true)
        {
            return prop.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a required string property value, throwing <see cref="ArgumentException"/>
    /// with a clear message if the property is missing or null.
    /// </summary>
    public static string GetRequiredString(this JsonElement? element, string propertyName)
    {
        if (element?.TryGetProperty(propertyName, out var prop) == true)
        {
            return prop.GetString() ?? throw new ArgumentException($"{propertyName} is required");
        }

        throw new ArgumentException($"{propertyName} is required");
    }

    /// <summary>
    /// Gets an integer property value, returning <paramref name="defaultValue"/> if the property
    /// is missing.
    /// </summary>
    public static int GetInt32OrDefault(this JsonElement? element, string propertyName, int defaultValue = 0)
    {
        if (element?.TryGetProperty(propertyName, out var prop) == true)
        {
            return prop.GetInt32();
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a boolean property value, returning <paramref name="defaultValue"/> if the property
    /// is missing.
    /// </summary>
    public static bool GetBoolOrDefault(this JsonElement? element, string propertyName, bool defaultValue = false)
    {
        if (element?.TryGetProperty(propertyName, out var prop) == true)
        {
            return prop.GetBoolean();
        }

        return defaultValue;
    }
}
