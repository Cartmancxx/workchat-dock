using Microsoft.Win32;
using WorkChatDock.Interop;
using WorkChatDock.Models;

namespace WorkChatDock.Services;

public sealed class TrayVisibilityService
{
    private const string NotifyIconSettingsPath = @"Control Panel\NotifyIconSettings";

    public Task<TrayVisibilityResult> ApplyAsync(IEnumerable<AppDefinition> apps,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Apply(apps, cancellationToken), cancellationToken);

    private static TrayVisibilityResult Apply(IEnumerable<AppDefinition> apps,
        CancellationToken cancellationToken)
    {
        var appList = apps.Where(app => app.Enabled && !string.IsNullOrWhiteSpace(app.ExecutablePath)).ToList();
        var promoted = 0;
        var hidden = 0;

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(NotifyIconSettingsPath, writable: true);
            if (root is null)
            {
                return new(false, 0, 0);
            }

            foreach (var keyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var key = root.OpenSubKey(keyName, writable: true);
                    if (key is null) continue;
                    var executablePath = key.GetValue("ExecutablePath")?.ToString() ?? string.Empty;
                    var tooltip = key.GetValue("InitialTooltip")?.ToString() ?? string.Empty;

                    if (IsWorkChatDock(executablePath, tooltip))
                    {
                        var isCurrent = string.Equals(Normalize(executablePath), Normalize(CurrentExecutable.Path),
                            StringComparison.OrdinalIgnoreCase);
                        if (SetPromotion(key, isCurrent ? 1 : 0))
                        {
                            if (isCurrent) promoted++;
                            else hidden++;
                        }
                        continue;
                    }

                    if (appList.Any(app => Matches(app, executablePath, tooltip)) && SetPromotion(key, 0))
                    {
                        hidden++;
                    }
                }
                catch
                {
                    // One stale or protected tray record should not block the rest.
                }
            }

            if (promoted + hidden > 0)
            {
                NativeMethods.SendMessageTimeout(
                    NativeMethods.HwndBroadcast,
                    NativeMethods.WmSettingChange,
                    UIntPtr.Zero,
                    "TraySettings",
                    NativeMethods.SmtoAbortIfHung,
                    1000,
                    out _);
            }

            return new(true, promoted, hidden);
        }
        catch
        {
            return new(false, promoted, hidden);
        }
    }

    private static bool IsWorkChatDock(string path, string tooltip) =>
        string.Equals(Path.GetFileName(path), "WorkChatDock.exe", StringComparison.OrdinalIgnoreCase) ||
        tooltip.Contains("WorkChat Dock", StringComparison.OrdinalIgnoreCase);

    private static bool Matches(AppDefinition app, string path, string tooltip)
    {
        if (!string.IsNullOrWhiteSpace(app.ExecutablePath) &&
            string.Equals(Normalize(path), Normalize(app.ExecutablePath), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(path);
        if (!app.ExecutableNames.Any(name =>
                string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return app.Keywords.Any(keyword =>
                   path.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                   tooltip.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
               app.ExecutableNames.Length == 1;
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim('"', ' '))); }
        catch { return path; }
    }

    private static bool SetPromotion(RegistryKey key, int value)
    {
        var current = key.GetValue("IsPromoted");
        if (current is int number && number == value)
        {
            return false;
        }

        key.SetValue("IsPromoted", value, RegistryValueKind.DWord);
        return true;
    }
}

public readonly record struct TrayVisibilityResult(bool Supported, int PromotedCount, int HiddenCount);
