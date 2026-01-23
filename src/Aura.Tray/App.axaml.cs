using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Reflection;

namespace Aura.Tray;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private StatusWindow? _statusWindow;
    private ServiceMonitor? _serviceMonitor;
    private NativeMenuItem? _autoStartItem;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Don't show main window - we're a tray app
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize service monitor
            _serviceMonitor = new ServiceMonitor();
            _serviceMonitor.StatusChanged += OnStatusChanged;
            _serviceMonitor.Start();

            // Create tray icon
            CreateTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon()
    {
        var menu = new NativeMenu();

        // Status header (non-clickable)
        var statusItem = new NativeMenuItem("Aura")
        {
            IsEnabled = false
        };
        menu.Add(statusItem);
        menu.Add(new NativeMenuItemSeparator());

        // Show Status Window
        var showStatusItem = new NativeMenuItem("Show Status...");
        showStatusItem.Click += (_, _) => ShowStatusWindow();
        menu.Add(showStatusItem);

        menu.Add(new NativeMenuItemSeparator());

        // Service controls
        var startServiceItem = new NativeMenuItem("Start Service");
        startServiceItem.Click += async (_, _) => await _serviceMonitor!.StartServiceAsync();
        menu.Add(startServiceItem);

        var stopServiceItem = new NativeMenuItem("Stop Service");
        stopServiceItem.Click += async (_, _) => await _serviceMonitor!.StopServiceAsync();
        menu.Add(stopServiceItem);

        var restartServiceItem = new NativeMenuItem("Restart Service");
        restartServiceItem.Click += async (_, _) => await _serviceMonitor!.RestartServiceAsync();
        menu.Add(restartServiceItem);

        menu.Add(new NativeMenuItemSeparator());

        // Quick actions
        var openVsCodeItem = new NativeMenuItem("Open VS Code");
        openVsCodeItem.Click += (_, _) => OpenVsCode();
        menu.Add(openVsCodeItem);

        var viewLogsItem = new NativeMenuItem("View Logs...");
        viewLogsItem.Click += (_, _) => ViewLogs();
        menu.Add(viewLogsItem);

        menu.Add(new NativeMenuItemSeparator());

        // Auto-start toggle
        var isAutoStartEnabled = AutoStartManager.IsAutoStartEnabled();
        _autoStartItem = new NativeMenuItem(isAutoStartEnabled ? "✓ Start with System" : "Start with System");
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        menu.Add(_autoStartItem);

        menu.Add(new NativeMenuItemSeparator());

        // Exit
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => Exit();
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Aura - Checking...",
            Menu = menu,
            IsVisible = true,
            Icon = LoadTrayIcon("tray-unknown")
        };

        _trayIcon.Clicked += (_, _) => ShowStatusWindow();
    }

    private void ToggleAutoStart()
    {
        var isCurrentlyEnabled = AutoStartManager.IsAutoStartEnabled();

        bool success;
        if (isCurrentlyEnabled)
        {
            success = AutoStartManager.DisableAutoStart();
        }
        else
        {
            success = AutoStartManager.EnableAutoStart();
        }

        if (success && _autoStartItem != null)
        {
            var newState = AutoStartManager.IsAutoStartEnabled();
            _autoStartItem.Header = newState ? "✓ Start with System" : "Start with System";
        }
    }

    private void OnStatusChanged(object? sender, ServiceStatusEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateTrayIcon(e);
            _statusWindow?.UpdateStatus(e);
        });
    }

    private void UpdateTrayIcon(ServiceStatusEventArgs e)
    {
        if (_trayIcon == null) return;

        // Build detailed tooltip showing each component
        var api = e.ApiStatus.IsHealthy ? "✓" : "✗";
        var ollama = e.OllamaStatus.IsHealthy ? "✓" : "✗";
        var db = e.PostgresStatus.IsHealthy ? "✓" : "✗";
        var rag = e.RagStatus.IsHealthy ? "✓" : "○";  // ○ for "empty but ok"
        var mcp = e.McpStatus.IsHealthy ? "✓" : "✗";

        var statusLine = e.OverallStatus switch
        {
            ServiceStatus.AllHealthy => "All systems operational",
            ServiceStatus.Degraded => "Some services degraded",
            ServiceStatus.Offline => "Services offline",
            _ => "Checking..."
        };

        _trayIcon.ToolTipText = $"Aura - {statusLine}\n{api} API  {ollama} Ollama  {db} DB  {rag} RAG  {mcp} MCP";

        // Update icon based on status
        var iconName = e.OverallStatus switch
        {
            ServiceStatus.AllHealthy => "tray-healthy",
            ServiceStatus.Degraded => "tray-degraded",
            ServiceStatus.Offline => "tray-offline",
            _ => "tray-unknown"
        };
        _trayIcon.Icon = LoadTrayIcon(iconName);
    }

    private static WindowIcon? LoadTrayIcon(string iconName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Aura.Tray.Assets.{iconName}.png";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return new WindowIcon(stream);
            }
        }
        catch
        {
            // Ignore icon loading errors
        }
        return null;
    }

    private void ShowStatusWindow()
    {
        if (_statusWindow == null || !_statusWindow.IsVisible)
        {
            _statusWindow = new StatusWindow(_serviceMonitor!);
            _statusWindow.Show();
        }
        else
        {
            _statusWindow.Activate();
        }
    }

    private void OpenVsCode()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "code",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open VS Code: {ex.Message}");
        }
    }

    private void ViewLogs()
    {
        try
        {
            var logPath = ServiceMonitor.GetLogPath();
            if (System.IO.File.Exists(logPath))
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open logs: {ex.Message}");
        }
    }

    private void Exit()
    {
        _serviceMonitor?.Stop();
        _trayIcon?.Dispose();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
