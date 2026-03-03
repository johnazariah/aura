using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Aura.Tray;

public partial class StatusWindow : Window
{
    private ServiceMonitor? _serviceMonitor;

    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
    private static readonly IBrush YellowBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
    private static readonly IBrush GrayBrush = new SolidColorBrush(Color.FromRgb(158, 158, 158));

    public StatusWindow()
    {
        InitializeComponent();
    }

    public StatusWindow(ServiceMonitor serviceMonitor) : this()
    {
        _serviceMonitor = serviceMonitor;
        _serviceMonitor.StatusChanged += OnStatusChanged;
        UpdateStatus(_serviceMonitor.CurrentStatus);
    }

    private void OnStatusChanged(object? sender, ServiceStatusEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatus(e));
    }

    public void UpdateStatus(ServiceStatusEventArgs status)
    {
        var overallStatusDot = this.FindControl<Avalonia.Controls.Shapes.Ellipse>("OverallStatusDot");
        var overallStatusText = this.FindControl<TextBlock>("OverallStatusText");

        if (overallStatusDot != null && overallStatusText != null)
        {
            (overallStatusDot.Fill, overallStatusText.Text) = status.OverallStatus switch
            {
                ServiceStatus.AllHealthy => (GreenBrush, "All Systems Operational"),
                ServiceStatus.Degraded => (YellowBrush, "Some Services Degraded"),
                ServiceStatus.Offline => (RedBrush, "Services Offline"),
                _ => (GrayBrush, "Checking Status...")
            };
        }

        UpdateComponentStatus("Api", status.ApiStatus);
        UpdateComponentStatus("Ollama", status.OllamaStatus);
        UpdateComponentStatus("Database", status.DatabaseStatus);
        UpdateComponentStatus("Rag", status.RagStatus);
        UpdateComponentStatus("Mcp", status.McpStatus);

        var lastCheckedText = this.FindControl<TextBlock>("LastCheckedText");
        if (lastCheckedText != null)
        {
            lastCheckedText.Text = $"Last checked: {status.LastChecked:HH:mm:ss}";
        }
    }

    private void UpdateComponentStatus(string prefix, ComponentStatus status)
    {
        var dot = this.FindControl<Avalonia.Controls.Shapes.Ellipse>($"{prefix}StatusDot");
        var text = this.FindControl<TextBlock>($"{prefix}StatusText");
        var details = this.FindControl<TextBlock>($"{prefix}StatusDetails");

        if (dot != null)
        {
            dot.Fill = status.IsHealthy ? GreenBrush : RedBrush;
        }

        if (text != null)
        {
            text.Text = status.StatusText;
            text.Foreground = status.IsHealthy ? GreenBrush : RedBrush;
        }

        if (details != null)
        {
            details.Text = status.Details ?? "";
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        // The ServiceMonitor polls every 10s; clicking Refresh just waits for next poll
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_serviceMonitor != null)
        {
            _serviceMonitor.StatusChanged -= OnStatusChanged;
        }
        base.OnClosed(e);
    }
}
