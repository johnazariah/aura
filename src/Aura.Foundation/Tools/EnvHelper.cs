namespace Aura.Foundation.Tools;

/// <summary>
/// Provides helper methods for working with environment variables.
/// </summary>
public static class EnvHelper
{
    /// <summary>
    /// Retrieves the value of the specified environment variable.
    /// If the environment variable is not set, returns the provided default value.
    /// </summary>
    /// <param name="key">The name of the environment variable to retrieve.</param>
    /// <param name="defaultValue">The value to return if the environment variable is not set.</param>
    /// <returns>The value of the environment variable, or the default value if not set.</returns>
    public static string GetOrDefault(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// Retrieves the value of the specified environment variable.
    /// Throws an exception if the environment variable is not set.
    /// </summary>
    /// <param name="key">The name of the environment variable to retrieve.</param>
    /// <returns>The value of the environment variable.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the environment variable is not set.
    /// </exception>
    public static string RequireEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{key}' is not set.");
        }
        return value;
    }
}
