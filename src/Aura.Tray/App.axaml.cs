using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _serviceMonitor = new ServiceMonitor();
            _serviceMonitor.StatusChanged += OnStatusChanged;
            _serviceMonitor.Start();

            CreateTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon()
    {
        var menu = new NativeMenu();

        var statusItem = new NativeMenuItem("Aura") { IsEnabled = false };
        menu.Add(statusItem);
        menu.Add(new NativeMenuItemSeparator());

        var showStatusItem = new NativeMenuItem("Show Status...");
        showStatusItem.Click += (_, _) => ShowStatusWindow();
        menu.Add(showStatusItem);

        menu.Add(new NativeMenuItemSeparator());

        var viewLogsItem = new NativeMenuItem("View Logs...");
        viewLogsItem.Click += (_, _) => ViewLogs();
        menu.Add(viewLogsItem);

        menu.Add(new NativeMenuItemSeparator());

        var isAutoStartEnabled = AutoStartManager.IsAutoStartEnabled();
        _autoStartItem = new NativeMenuItem(isAutoStartEnabled ? "✓ Start with System" : "Start with System");
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        menu.Add(_autoStartItem);

        menu.Add(new NativeMenuItemSeparator());

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
        UpdateTrayIcon(ServiceStatus.Unknown);
    }

    private void ToggleAutoStart()
    {
        var isCurrentlyEnabled = AutoStartManager.IsAutoStartEnabled();

        if (isCurrentlyEnabled)
        {
            AutoStartManager.DisableAutoStart();
        }
        else
        {
            AutoStartManager.EnableAutoStart();
        }

        if (_autoStartItem != null)
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

        var statusText = status switch
        {
            ServiceStatus.AllHealthy => "All systems operational",
            ServiceStatus.Degraded => "Some services degraded",
            ServiceStatus.Offline => "Services offline",
            _ => "Checking status..."
        };

        _trayIcon.ToolTipText = $"Aura - {statusText}";
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
