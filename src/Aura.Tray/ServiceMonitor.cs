using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aura.Tray;

public enum ServiceStatus
{
    Unknown,
    AllHealthy,
    Degraded,
    Offline
}

public class ComponentStatus
{
    public string Name { get; init; } = "";
    public bool IsHealthy { get; init; }
    public string StatusText { get; init; } = "";
    public string? Details { get; init; }
}

public class ServiceStatusEventArgs : EventArgs
{
    public ServiceStatus OverallStatus { get; init; }
    public ComponentStatus ApiStatus { get; init; } = new();
    public ComponentStatus OllamaStatus { get; init; } = new();
    public ComponentStatus DatabaseStatus { get; init; } = new();
    public ComponentStatus RagStatus { get; init; } = new();
    public ComponentStatus McpStatus { get; init; } = new();
    public DateTime LastChecked { get; init; } = DateTime.Now;
}

public class ServiceMonitor : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Timer _pollTimer;
    private readonly string _apiBaseUrl;
    private readonly string _ollamaUrl;
    private bool _isRunning;

    public event EventHandler<ServiceStatusEventArgs>? StatusChanged;

    public ServiceStatusEventArgs CurrentStatus { get; private set; } = new()
    {
        OverallStatus = ServiceStatus.Unknown,
        ApiStatus = new ComponentStatus { Name = "API", StatusText = "Checking..." },
        OllamaStatus = new ComponentStatus { Name = "Ollama", StatusText = "Checking..." },
        DatabaseStatus = new ComponentStatus { Name = "PostgreSQL", StatusText = "Checking..." },
        RagStatus = new ComponentStatus { Name = "RAG Index", StatusText = "Checking..." },
        McpStatus = new ComponentStatus { Name = "MCP Server", StatusText = "Checking..." }
    };

    public ServiceMonitor(string apiBaseUrl = "http://localhost:5300", string ollamaUrl = "http://localhost:11434")
    {
        _apiBaseUrl = apiBaseUrl;
        _ollamaUrl = ollamaUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _pollTimer = new Timer(async _ => await CheckStatusAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _isRunning = true;
        _pollTimer.Change(0, 10_000);
    }

    public void Stop()
    {
        _isRunning = false;
        _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async Task CheckStatusAsync()
    {
        if (!_isRunning) return;

        var apiStatus = await CheckApiAsync();
        var ollamaStatus = await CheckOllamaAsync();
        var dbStatus = await CheckDatabaseAsync();
        var ragStatus = await CheckRagAsync();
        var mcpStatus = await CheckMcpAsync();

        var allHealthy = apiStatus.IsHealthy && ollamaStatus.IsHealthy && dbStatus.IsHealthy && ragStatus.IsHealthy;
        var anyHealthy = apiStatus.IsHealthy || ollamaStatus.IsHealthy || dbStatus.IsHealthy;

        var overallStatus = allHealthy
            ? ServiceStatus.AllHealthy
            : anyHealthy
                ? ServiceStatus.Degraded
                : ServiceStatus.Offline;

        CurrentStatus = new ServiceStatusEventArgs
        {
            OverallStatus = overallStatus,
            ApiStatus = apiStatus,
            OllamaStatus = ollamaStatus,
            DatabaseStatus = dbStatus,
            RagStatus = ragStatus,
            McpStatus = mcpStatus,
            LastChecked = DateTime.Now
        };

        StatusChanged?.Invoke(this, CurrentStatus);
    }

    private async Task<ComponentStatus> CheckApiAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health");
            if (response.IsSuccessStatusCode)
            {
                return new ComponentStatus
                {
                    Name = "API",
                    IsHealthy = true,
                    StatusText = "Running",
                    Details = $"{_apiBaseUrl}"
                };
            }

            return new ComponentStatus
            {
                Name = "API",
                IsHealthy = false,
                StatusText = $"Error ({(int)response.StatusCode})",
                Details = response.ReasonPhrase
            };
        }
        catch (HttpRequestException)
        {
            return new ComponentStatus { Name = "API", IsHealthy = false, StatusText = "Offline", Details = "Cannot connect to API server" };
        }
        catch (TaskCanceledException)
        {
            return new ComponentStatus { Name = "API", IsHealthy = false, StatusText = "Timeout", Details = "API server not responding" };
        }
    }

    private async Task<ComponentStatus> CheckOllamaAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_ollamaUrl}/api/tags");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                var modelCount = data.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array
                    ? models.GetArrayLength()
                    : 0;

                return new ComponentStatus
                {
                    Name = "Ollama",
                    IsHealthy = true,
                    StatusText = "Running",
                    Details = $"{modelCount} model(s) available"
                };
            }

            return new ComponentStatus { Name = "Ollama", IsHealthy = false, StatusText = $"Error ({(int)response.StatusCode})" };
        }
        catch (HttpRequestException)
        {
            return new ComponentStatus { Name = "Ollama", IsHealthy = false, StatusText = "Offline", Details = "Start with 'ollama serve'" };
        }
        catch (TaskCanceledException)
        {
            return new ComponentStatus { Name = "Ollama", IsHealthy = false, StatusText = "Timeout" };
        }
    }

    private async Task<ComponentStatus> CheckDatabaseAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health/db");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                var healthy = data.TryGetProperty("healthy", out var h) && h.GetBoolean();
                var details = data.TryGetProperty("details", out var d) ? d.GetString() : null;

                return new ComponentStatus
                {
                    Name = "PostgreSQL",
                    IsHealthy = healthy,
                    StatusText = healthy ? "Connected" : "Error",
                    Details = details
                };
            }

            return new ComponentStatus { Name = "PostgreSQL", IsHealthy = false, StatusText = "Unknown", Details = "Cannot check - API error" };
        }
        catch
        {
            return new ComponentStatus { Name = "PostgreSQL", IsHealthy = false, StatusText = "Unknown", Details = "Cannot check - API offline" };
        }
    }

    private async Task<ComponentStatus> CheckRagAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health/rag");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                var healthy = data.TryGetProperty("healthy", out var h) && h.GetBoolean();
                var totalChunks = data.TryGetProperty("totalChunks", out var tc) ? tc.GetInt32() : 0;
                var totalDocs = data.TryGetProperty("totalDocuments", out var td) ? td.GetInt32() : 0;

                return new ComponentStatus
                {
                    Name = "RAG Index",
                    IsHealthy = healthy,
                    StatusText = healthy ? "Indexed" : "Not available",
                    Details = healthy ? $"{totalChunks} chunks, {totalDocs} documents" : "Ollama or database unavailable"
                };
            }

            return new ComponentStatus { Name = "RAG Index", IsHealthy = false, StatusText = "Not available" };
        }
        catch
        {
            return new ComponentStatus { Name = "RAG Index", IsHealthy = false, StatusText = "Unknown", Details = "Cannot check - API offline" };
        }
    }

    private async Task<ComponentStatus> CheckMcpAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health/mcp");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                var details = data.TryGetProperty("details", out var d) ? d.GetString() : null;

                return new ComponentStatus
                {
                    Name = "MCP Server",
                    IsHealthy = true,
                    StatusText = "Ready",
                    Details = details
                };
            }

            return new ComponentStatus { Name = "MCP Server", IsHealthy = false, StatusText = "Error" };
        }
        catch
        {
            return new ComponentStatus { Name = "MCP Server", IsHealthy = false, StatusText = "Offline", Details = "Cannot check - API offline" };
        }
    }

    public static string GetLogPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Find today's log file
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Aura", "logs");
            var today = DateTime.Now.ToString("yyyyMMdd");
            var todayLog = Path.Combine(logDir, $"aura-{today}.log");
            if (File.Exists(todayLog)) return todayLog;

            // Fallback to most recent log
            if (Directory.Exists(logDir))
            {
                var latest = Directory.GetFiles(logDir, "aura-*.log")
                    .OrderDescending()
                    .FirstOrDefault();
                if (latest != null) return latest;
            }

            return Path.Combine(logDir, "aura-.log");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "Aura", "logs");
            return Directory.Exists(dataHome)
                ? Directory.GetFiles(dataHome, "aura-*.log").OrderDescending().FirstOrDefault()
                  ?? Path.Combine(dataHome, "aura-.log")
                : Path.Combine(dataHome, "aura-.log");
        }
        else
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(dataHome, "Aura", "logs", "aura-.log");
        }
    }

    public void Dispose()
    {
        Stop();
        _pollTimer.Dispose();
        _httpClient.Dispose();
    }
}
