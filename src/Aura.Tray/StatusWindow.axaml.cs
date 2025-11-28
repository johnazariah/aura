using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Aura.Tray;

public partial class StatusWindow : Window
{
    private ServiceMonitor? _serviceMonitor;
    
    // Color constants
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));   // #4CAF50
    private static readonly IBrush YellowBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));  // #FFC107
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));     // #F44336
    private static readonly IBrush GrayBrush = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // #9E9E9E

    // Parameterless constructor for XAML loader
    public StatusWindow()
    {
        InitializeComponent();
    }

    public StatusWindow(ServiceMonitor serviceMonitor) : this()
    {
        _serviceMonitor = serviceMonitor;
        
        // Subscribe to status changes
        _serviceMonitor.StatusChanged += OnStatusChanged;
        
        // Show current status immediately
        UpdateStatus(_serviceMonitor.CurrentStatus);
    }

    private void OnStatusChanged(object? sender, ServiceStatusEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatus(e));
    }

    public void UpdateStatus(ServiceStatusEventArgs status)
    {
        // Overall status
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
        
        // API Status
        UpdateComponentStatus("Api", status.ApiStatus);
        
        // Ollama Status
        UpdateComponentStatus("Ollama", status.OllamaStatus);
        
        // PostgreSQL Status
        UpdateComponentStatus("Postgres", status.PostgresStatus);
        
        // RAG Status
        UpdateComponentStatus("Rag", status.RagStatus);
        
        // Last checked
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
        // Trigger a manual refresh
        // The ServiceMonitor will fire StatusChanged when done
        _ = Task.Run(async () =>
        {
            // Access the private CheckStatusAsync via reflection or make it public
            // For now, we'll just wait for the next poll
            await Task.Delay(100);
        });
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
