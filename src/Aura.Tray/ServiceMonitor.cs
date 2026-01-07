using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
    public string? Version { get; init; }
    public string? Details { get; init; }
}

public class ServiceStatusEventArgs : EventArgs
{
    public ServiceStatus OverallStatus { get; init; }
    public ComponentStatus ApiStatus { get; init; } = new();
    public ComponentStatus OllamaStatus { get; init; } = new();
    public ComponentStatus PostgresStatus { get; init; } = new();
    public ComponentStatus RagStatus { get; init; } = new();
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
        PostgresStatus = new ComponentStatus { Name = "PostgreSQL", StatusText = "Checking..." },
        RagStatus = new ComponentStatus { Name = "RAG Index", StatusText = "Checking..." }
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
        // Check immediately, then every 10 seconds
        _pollTimer.Change(0, 10000);
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
        var postgresStatus = await CheckPostgresAsync();
        var ragStatus = await CheckRagAsync();

        // Determine overall status
        var allHealthy = apiStatus.IsHealthy && ollamaStatus.IsHealthy && postgresStatus.IsHealthy;
        var anyHealthy = apiStatus.IsHealthy || ollamaStatus.IsHealthy || postgresStatus.IsHealthy;

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
            PostgresStatus = postgresStatus,
            RagStatus = ragStatus,
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
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var health = doc.RootElement;

                var status = health.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()
                    : "Healthy";

                return new ComponentStatus
                {
                    Name = "API",
                    IsHealthy = true,
                    StatusText = status ?? "Running",
                    Details = $"Endpoint: {_apiBaseUrl}"
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
            return new ComponentStatus
            {
                Name = "API",
                IsHealthy = false,
                StatusText = "Offline",
                Details = "Cannot connect to API server"
            };
        }
        catch (TaskCanceledException)
        {
            return new ComponentStatus
            {
                Name = "API",
                IsHealthy = false,
                StatusText = "Timeout",
                Details = "API server not responding"
            };
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
                using var doc = JsonDocument.Parse(content);
                var data = doc.RootElement;

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

            return new ComponentStatus
            {
                Name = "Ollama",
                IsHealthy = false,
                StatusText = $"Error ({(int)response.StatusCode})"
            };
        }
        catch (HttpRequestException)
        {
            return new ComponentStatus
            {
                Name = "Ollama",
                IsHealthy = false,
                StatusText = "Offline",
                Details = "Ollama not running - start with 'ollama serve'"
            };
        }
        catch (TaskCanceledException)
        {
            return new ComponentStatus
            {
                Name = "Ollama",
                IsHealthy = false,
                StatusText = "Timeout"
            };
        }
    }

    private async Task<ComponentStatus> CheckPostgresAsync()
    {
        try
        {
            // Check via API's database health endpoint
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var health = doc.RootElement;

                // Look for database check in health response
                if (health.TryGetProperty("checks", out var checks) && checks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var check in checks.EnumerateArray())
                    {
                        if (check.TryGetProperty("name", out var name) &&
                            name.GetString()?.Contains("database", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var checkStatus = check.TryGetProperty("status", out var s) ? s.GetString() : null;
                            var isHealthy = checkStatus?.Equals("Healthy", StringComparison.OrdinalIgnoreCase) == true;

                            return new ComponentStatus
                            {
                                Name = "PostgreSQL",
                                IsHealthy = isHealthy,
                                StatusText = isHealthy ? "Connected" : "Error",
                                Details = check.TryGetProperty("description", out var desc) ? desc.GetString() : null
                            };
                        }
                    }
                }

                // If no explicit database check, assume healthy if API is healthy
                return new ComponentStatus
                {
                    Name = "PostgreSQL",
                    IsHealthy = true,
                    StatusText = "Connected",
                    Details = "Database accessible via API"
                };
            }

            return new ComponentStatus
            {
                Name = "PostgreSQL",
                IsHealthy = false,
                StatusText = "Unknown",
                Details = "Cannot determine status - API offline"
            };
        }
        catch
        {
            return new ComponentStatus
            {
                Name = "PostgreSQL",
                IsHealthy = false,
                StatusText = "Unknown",
                Details = "Cannot determine status - API offline"
            };
        }
    }

    private async Task<ComponentStatus> CheckRagAsync()
    {
        try
        {
            // Get aggregate RAG stats (system-wide, not per-repo)
            // Per-repo stats are shown in the VS Code extension
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/rag/stats");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var data = doc.RootElement;

                var totalDocs = data.TryGetProperty("totalDocuments", out var docs) ? docs.GetInt32() : 0;
                var totalChunks = data.TryGetProperty("totalChunks", out var chunks) ? chunks.GetInt32() : 0;

                if (totalDocs > 0)
                {
                    return new ComponentStatus
                    {
                        Name = "RAG Index",
                        IsHealthy = true,
                        StatusText = "Active",
                        Details = $"{totalDocs:N0} files, {totalChunks:N0} chunks indexed"
                    };
                }

                return new ComponentStatus
                {
                    Name = "RAG Index",
                    IsHealthy = true,
                    StatusText = "Empty",
                    Details = "No repositories indexed yet"
                };
            }

            return new ComponentStatus
            {
                Name = "RAG Index",
                IsHealthy = false,
                StatusText = "Unavailable",
                Details = "RAG service not responding"
            };
        }
        catch
        {
            return new ComponentStatus
            {
                Name = "RAG Index",
                IsHealthy = false,
                StatusText = "Unknown",
                Details = "Cannot check RAG status"
            };
        }
    }

    public async Task StartServiceAsync()
    {
        try
        {
            var installerPath = GetInstallerPath();
            if (installerPath != null)
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "start",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(startInfo);

                // Give it a moment then check status
                await Task.Delay(2000);
                await CheckStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start service: {ex.Message}");
        }
    }

    public async Task StopServiceAsync()
    {
        try
        {
            var installerPath = GetInstallerPath();
            if (installerPath != null)
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "stop",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(startInfo);

                await Task.Delay(2000);
                await CheckStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to stop service: {ex.Message}");
        }
    }

    public async Task RestartServiceAsync()
    {
        await StopServiceAsync();
        await Task.Delay(1000);
        await StartServiceAsync();
    }

    private static string? GetInstallerPath()
    {
        var basePath = AppContext.BaseDirectory;
        var installerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Aura.Installer.exe"
            : "Aura.Installer";

        var installerPath = Path.Combine(basePath, installerName);
        return File.Exists(installerPath) ? installerPath : null;
    }

    public static string GetLogPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Aura", "logs", "service.log"
            );
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", "Aura", "service.log"
            );
        }
        else
        {
            return "/var/log/Aura/service.log";
        }
    }

    public void Dispose()
    {
        Stop();
        _pollTimer.Dispose();
        _httpClient.Dispose();
    }
}
