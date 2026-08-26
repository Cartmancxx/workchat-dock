using WorkChatDock.Interop;
using Microsoft.Win32;

namespace WorkChatDock.Services;

public sealed class ShortcutService
{
    public string ShortcutPath => Path.Combine(ResolveDesktopDirectory(), "一键打开办公软件.lnk");

    public void CreateLaunchAllShortcut()
    {
        var executablePath = CurrentExecutable.Path;
        var iconPath = executablePath;
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);
        ShellLinkHelper.Create(
            ShortcutPath,
            executablePath,
            "--launch-all",
            "一键启动 Zalo、钉钉、飞书和京ME",
            iconPath);
    }

    public void EnsureLaunchAllShortcut()
    {
        if (!File.Exists(ShortcutPath))
        {
            CreateLaunchAllShortcut();
        }
    }

    private static string ResolveDesktopDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("WORKCHATDOCK_DESKTOP_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            return desktop;
        }

        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
        desktop = key?.GetValue("Desktop")?.ToString();
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            return Environment.ExpandEnvironmentVariables(desktop);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
    }
}
