using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Aura.Tray;

/// <summary>
/// Manages auto-start configuration for the tray app across platforms.
/// </summary>
public static class AutoStartManager
{
    private const string AppName = "AuraTray";

    /// <summary>
    /// Check if auto-start is currently enabled
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsWindowsAutoStartEnabled();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IsMacAutoStartEnabled();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return IsLinuxAutoStartEnabled();
        }

        return false;
    }

    /// <summary>
    /// Enable auto-start on system boot/login
    /// </summary>
    public static bool EnableAutoStart()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return EnableWindowsAutoStart();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return EnableMacAutoStart();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return EnableLinuxAutoStart();
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enable auto-start: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disable auto-start
    /// </summary>
    public static bool DisableAutoStart()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DisableWindowsAutoStart();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return DisableMacAutoStart();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return DisableLinuxAutoStart();
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to disable auto-start: {ex.Message}");
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        var exePath = Environment.ProcessPath;
        return exePath ?? Path.Combine(AppContext.BaseDirectory,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Aura.Tray.exe"
                : "Aura.Tray");
    }

    #region Windows

    private static bool IsWindowsAutoStartEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        return key?.GetValue(AppName) != null;
    }

    private static bool EnableWindowsAutoStart()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null) return false;

        var exePath = GetExecutablePath();
        key.SetValue(AppName, $"\"{exePath}\" --minimized");
        return true;
    }

    private static bool DisableWindowsAutoStart()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        key?.DeleteValue(AppName, false);
        return true;
    }

    #endregion

    #region macOS

    private static string GetMacLaunchAgentPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", "com.Aura.Tray.plist");
    }

    private static bool IsMacAutoStartEnabled()
    {
        return File.Exists(GetMacLaunchAgentPath());
    }

    private static bool EnableMacAutoStart()
    {
        var plistPath = GetMacLaunchAgentPath();
        var exePath = GetExecutablePath();

        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.Aura.Tray</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exePath}</string>
        <string>--minimized</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <false/>
</dict>
</plist>";

        // Ensure directory exists
        var dir = Path.GetDirectoryName(plistPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(plistPath, plistContent);

        // Load the launch agent
        var startInfo = new ProcessStartInfo
        {
            FileName = "launchctl",
            Arguments = $"load \"{plistPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(startInfo)?.WaitForExit();

        return true;
    }

    private static bool DisableMacAutoStart()
    {
        var plistPath = GetMacLaunchAgentPath();

        if (File.Exists(plistPath))
        {
            // Unload the launch agent first
            var startInfo = new ProcessStartInfo
            {
                FileName = "launchctl",
                Arguments = $"unload \"{plistPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo)?.WaitForExit();

            File.Delete(plistPath);
        }

        return true;
    }

    #endregion

    #region Linux

    private static string GetLinuxAutoStartPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configHome, "autostart", "Aura-tray.desktop");
    }

    private static bool IsLinuxAutoStartEnabled()
    {
        return File.Exists(GetLinuxAutoStartPath());
    }

    private static bool EnableLinuxAutoStart()
    {
        var desktopPath = GetLinuxAutoStartPath();
        var exePath = GetExecutablePath();

        var desktopContent = $@"[Desktop Entry]
Type=Application
Name=Aura Tray
Comment=System tray for Aura status
Exec={exePath} --minimized
Icon=Aura
Terminal=false
Categories=Development;
StartupNotify=false
X-GNOME-Autostart-enabled=true
";

        // Ensure directory exists
        var dir = Path.GetDirectoryName(desktopPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(desktopPath, desktopContent);
        return true;
    }

    private static bool DisableLinuxAutoStart()
    {
        var desktopPath = GetLinuxAutoStartPath();

        if (File.Exists(desktopPath))
        {
            File.Delete(desktopPath);
        }

        return true;
    }

    #endregion
}
