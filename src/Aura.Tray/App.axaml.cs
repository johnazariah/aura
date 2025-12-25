using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;

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
            ToolTipText = "Aura",
            Menu = menu,
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) => ShowStatusWindow();

        // Set initial icon based on status
        UpdateTrayIcon(ServiceStatus.Unknown);
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
            UpdateTrayIcon(e.OverallStatus);
            _statusWindow?.UpdateStatus(e);
        });
    }

    private void UpdateTrayIcon(ServiceStatus status)
    {
        if (_trayIcon == null) return;

        // Update tooltip with current status
        var statusText = status switch
        {
            ServiceStatus.AllHealthy => "All systems operational",
            ServiceStatus.Degraded => "Some services degraded",
            ServiceStatus.Offline => "Services offline",
            _ => "Checking status..."
        };

        _trayIcon.ToolTipText = $"Aura - {statusText}";

        // Note: For production, you'd want actual icon files for each state
        // For now, we'll just update the tooltip
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
