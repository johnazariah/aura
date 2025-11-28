using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.Reflection;

namespace Aura.Tray;

/// <summary>
/// Manages tray icon assets for different status states.
/// Provides cross-platform icon loading from embedded resources.
/// </summary>
public static class TrayIconManager
{
    private static readonly Dictionary<ServiceStatus, Bitmap?> _iconCache = new();
    
    /// <summary>
    /// Get the appropriate icon bitmap for the given service status
    /// </summary>
    public static Bitmap? GetIconForStatus(ServiceStatus status)
    {
        if (_iconCache.TryGetValue(status, out var cached))
        {
            return cached;
        }
        
        var iconName = status switch
        {
            ServiceStatus.AllHealthy => "tray-healthy",
            ServiceStatus.Degraded => "tray-degraded",
            ServiceStatus.Offline => "tray-offline",
            _ => "tray-unknown"
        };
        
        var icon = LoadIconFromResources(iconName);
        _iconCache[status] = icon;
        return icon;
    }
    
    /// <summary>
    /// Get the icon file path for platforms that need file-based icons
    /// </summary>
    public static string? GetIconPathForStatus(ServiceStatus status)
    {
        var iconName = status switch
        {
            ServiceStatus.AllHealthy => "tray-healthy",
            ServiceStatus.Degraded => "tray-degraded",
            ServiceStatus.Offline => "tray-offline",
            _ => "tray-unknown"
        };
        
        // Check for extracted icons in app directory
        var basePath = AppContext.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(basePath, "Assets", $"{iconName}.png"),
            Path.Combine(basePath, "Assets", $"{iconName}.ico"),
            Path.Combine(basePath, $"{iconName}.png"),
            Path.Combine(basePath, $"{iconName}.ico"),
        };
        
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        // Try to extract from resources
        return ExtractIconToTemp(iconName);
    }
    
    private static Bitmap? LoadIconFromResources(string iconName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Aura.Tray.Assets.{iconName}.svg";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                // Note: Avalonia can load SVG via Avalonia.Svg.Skia package
                // For now, return null and rely on file-based icons
                return null;
            }
        }
        catch
        {
            // Ignore resource loading errors
        }
        
        return null;
    }
    
    private static string? ExtractIconToTemp(string iconName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Aura.Tray.Assets.{iconName}.svg";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            
            var tempPath = Path.Combine(Path.GetTempPath(), "Aura", $"{iconName}.svg");
            var dir = Path.GetDirectoryName(tempPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            using var fileStream = File.Create(tempPath);
            stream.CopyTo(fileStream);
            
            return tempPath;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Preload all icons into cache
    /// </summary>
    public static void PreloadIcons()
    {
        foreach (ServiceStatus status in Enum.GetValues<ServiceStatus>())
        {
            GetIconForStatus(status);
        }
    }
}
