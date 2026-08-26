using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using WorkChatDock.Models;

namespace WorkChatDock.Services;

public sealed class NotificationMonitorService : IDisposable
{
    private static readonly Regex UnreadTitlePattern = new(
        @"(?:^|\s)[\(（\[](?<count>\d{1,3})[\)）\]]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly object _sync = new();
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private readonly Dictionary<string, HashSet<string>> _currentNotificationIds =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _acknowledgedNotificationIds =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _acknowledgedTitleSignatures =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lastCounts = new(StringComparer.OrdinalIgnoreCase);
    private List<AppDefinition> _apps = [];
    private Timer? _timer;
    private bool _nativeNotificationAccess;
    private bool _nativeNotificationSubscribed;
    private bool _disposed;

    public string ModeText { get; private set; } = "窗口状态检测";
    public event Action<IReadOnlyDictionary<string, int>>? UnreadChanged;

    public async Task InitializeAsync(IEnumerable<AppDefinition> apps)
    {
        _apps = apps.ToList();
        var pollInterval = TimeSpan.FromSeconds(2);
        try
        {
            var access = await UserNotificationListener.Current.RequestAccessAsync();
            _nativeNotificationAccess = access == UserNotificationListenerAccessStatus.Allowed;
            if (_nativeNotificationAccess)
            {
                try
                {
                    UserNotificationListener.Current.NotificationChanged += OnNativeNotificationChanged;
                    _nativeNotificationSubscribed = true;
                    ModeText = "Windows 通知监听";
                    pollInterval = TimeSpan.FromSeconds(15);
                }
                catch
                {
                    // Some unpackaged desktop environments report access as allowed but
                    // fail to register the WinRT event with ERROR_NOT_FOUND (0x80070490).
                    // Native polling may still work, so retain it without crashing startup.
                    _nativeNotificationSubscribed = false;
                    ModeText = "Windows 通知轮询";
                }
            }
            else
            {
                ModeText = "窗口状态检测";
            }
        }
        catch
        {
            _nativeNotificationAccess = false;
            _nativeNotificationSubscribed = false;
            ModeText = "窗口状态检测";
        }

        _timer = new Timer(async _ => await PollSafelyAsync(), null, TimeSpan.Zero, pollInterval);
    }

    public void MarkAcknowledged(string appId)
    {
        lock (_sync)
        {
            if (_currentNotificationIds.TryGetValue(appId, out var ids))
            {
                if (!_acknowledgedNotificationIds.TryGetValue(appId, out var acknowledged))
                {
                    acknowledged = [];
                    _acknowledgedNotificationIds[appId] = acknowledged;
                }

                acknowledged.UnionWith(ids);
            }

            var app = _apps.FirstOrDefault(item => string.Equals(item.Id, appId,
                StringComparison.OrdinalIgnoreCase));
            if (app is not null)
            {
                _acknowledgedTitleSignatures[appId] = GetWindowTitleSignature(app);
            }
        }

        _ = PollSafelyAsync();
    }

    private async Task PollSafelyAsync()
    {
        if (_disposed || !await _pollGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var counts = _nativeNotificationAccess
                ? await PollWindowsNotificationsAsync()
                : PollWindowTitles();
            PublishIfChanged(counts);
        }
        catch
        {
            var counts = PollWindowTitles();
            PublishIfChanged(counts);
        }
        finally
        {
            _pollGate.Release();
        }
    }

    private void OnNativeNotificationChanged(UserNotificationListener sender,
        UserNotificationChangedEventArgs eventArgs) => _ = PollSafelyAsync();

    private async Task<Dictionary<string, int>> PollWindowsNotificationsAsync()
    {
        var result = _apps.ToDictionary(app => app.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var currentIds = _apps.ToDictionary(app => app.Id, _ => new HashSet<string>(),
            StringComparer.OrdinalIgnoreCase);
        var notifications = await UserNotificationListener.Current
            .GetNotificationsAsync(NotificationKinds.Toast);

        foreach (var notification in notifications)
        {
            var displayName = notification.AppInfo.DisplayInfo.DisplayName ?? string.Empty;
            var appUserModelId = notification.AppInfo.AppUserModelId ?? string.Empty;
            var app = _apps.FirstOrDefault(candidate =>
                candidate.NotificationNames.Any(name =>
                    displayName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    appUserModelId.Contains(name, StringComparison.OrdinalIgnoreCase)));
            if (app is null)
            {
                continue;
            }

            currentIds[app.Id].Add($"{appUserModelId}:{notification.Id}");
        }

        lock (_sync)
        {
            foreach (var app in _apps)
            {
                _currentNotificationIds[app.Id] = currentIds[app.Id];
                if (!_acknowledgedNotificationIds.TryGetValue(app.Id, out var acknowledged))
                {
                    acknowledged = [];
                    _acknowledgedNotificationIds[app.Id] = acknowledged;
                }

                acknowledged.IntersectWith(currentIds[app.Id]);
                result[app.Id] = currentIds[app.Id].Count(id => !acknowledged.Contains(id));
            }
        }

        return result;
    }

    private Dictionary<string, int> PollWindowTitles()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        lock (_sync)
        {
            foreach (var app in _apps)
            {
                var signature = GetWindowTitleSignature(app);
                if (_acknowledgedTitleSignatures.TryGetValue(app.Id, out var acknowledged) &&
                    string.Equals(acknowledged, signature, StringComparison.Ordinal))
                {
                    result[app.Id] = 0;
                    continue;
                }

                var counts = UnreadTitlePattern.Matches(signature)
                    .Select(match => int.TryParse(match.Groups["count"].Value, out var value) ? value : 0)
                    .ToArray();
                result[app.Id] = counts.Length == 0 ? 0 : Math.Max(1, counts.Max());
            }
        }

        return result;
    }

    private static string GetWindowTitleSignature(AppDefinition app)
    {
        var titles = new List<string>();
        foreach (var processName in app.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
                        {
                            titles.Add(process.MainWindowTitle);
                        }
                    }
                    catch
                    {
                        // Ignore processes that exited during the poll.
                    }
                }
            }
        }

        return string.Join(" | ", titles.Distinct(StringComparer.Ordinal));
    }

    private void PublishIfChanged(Dictionary<string, int> counts)
    {
        bool changed;
        lock (_sync)
        {
            changed = counts.Count != _lastCounts.Count ||
                      counts.Any(pair => !_lastCounts.TryGetValue(pair.Key, out var previous) ||
                                         previous != pair.Value);
            if (changed)
            {
                _lastCounts.Clear();
                foreach (var pair in counts)
                {
                    _lastCounts[pair.Key] = pair.Value;
                }
            }
        }

        if (changed)
        {
            UnreadChanged?.Invoke(counts);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        if (_nativeNotificationSubscribed)
        {
            try
            {
                UserNotificationListener.Current.NotificationChanged -= OnNativeNotificationChanged;
            }
            catch
            {
                // Shutdown should remain best effort when the WinRT registration vanished.
            }
        }

        _timer?.Dispose();
        _pollGate.Dispose();
    }
}
