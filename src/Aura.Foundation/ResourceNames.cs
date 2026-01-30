namespace Aura.Foundation;

/// <summary>
/// Common resource names used across Aura services. Centralizes string constants for Aspire orchestration, connection strings, and other infrastructure identifiers.
/// </summary>
public static class ResourceNames
{
    /// <summary>
    /// PostgreSQL server resource name for Aspire orchestration.
    /// </summary>
    public const string Postgres = "postgres";

    /// <summary>
    /// Aura database resource name (database within PostgreSQL).
    /// Also used as the connection string name in appsettings.
    /// </summary>
    public const string AuraDb = "auradb";

    /// <summary>
    /// Aura API service resource name for Aspire orchestration.
    /// </summary>
    public const string AuraApi = "aura-api";
}
