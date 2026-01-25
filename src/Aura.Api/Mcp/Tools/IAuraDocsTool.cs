namespace Aura.Api.Mcp.Tools;

/// <summary>
/// Tool for performing semantic searches in Aura documentation.
/// </summary>
public interface IAuraDocsTool
{
    /// <summary>
    /// Performs a semantic search in Aura documentation.
    /// </summary>
    /// <param name="query">The search query to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results containing relevant documentation.</returns>
    Task<object> SearchDocumentationAsync(string query, CancellationToken ct);
}
