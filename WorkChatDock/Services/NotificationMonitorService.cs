using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using WorkChatDock.Interop;
using WorkChatDock.Models;

namespace WorkChatDock.Services;

public sealed class NotificationMonitorService : IDisposable
{
    private static readonly Regex[] UnreadTitlePatterns =
    [
        new(@"[\(（\[【]\s*(?<count>\d{1,4})\s*[\)）\]】]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"(?<count>\d{1,4})\s*(?:条|則|封)?\s*(?:新消息|新訊息|未读|未讀|消息|訊息|通知|unread|new\s+messages?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        new(@"(?:未读|未讀|消息|訊息|通知|unread|new\s+messages?)\s*[:：]?\s*(?<count>\d{1,4})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
    ];

    private static readonly Regex UnreadMarkerPattern = new(
        @"(?:^|\s)(?:●|•)\s*|\bnew\s+message\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
    private string? _testAppId;
    private DateTimeOffset _testUntil;
    private int _testCount;
    private bool _disposed;

    public string ModeText { get; private set; } = "窗口状态检测";
    public event Action<IReadOnlyDictionary<string, int>>? UnreadChanged;

    public async Task InitializeAsync(IEnumerable<AppDefinition> apps)
    {
        _apps = apps.ToList();
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
                    ModeText = "Windows 通知 + 窗口检测";
                }
                catch
                {
                    // Some unpackaged desktop environments report access as allowed but
                    // fail to register the WinRT event with ERROR_NOT_FOUND (0x80070490).
                    // Native polling may still work, so retain it without crashing startup.
                    _nativeNotificationSubscribed = false;
                    ModeText = "通知轮询 + 窗口检测";
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

        // Always poll quickly. Some desktop clients update only a hidden window title and
        // some WinRT notification providers do not raise NotificationChanged reliably.
        _timer = new Timer(async _ => await PollSafelyAsync(), null, TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
    }

    public void TriggerTestNotification(string appId, int count = 3,
        TimeSpan? duration = null)
    {
        lock (_sync)
        {
            _testAppId = appId;
            _testCount = Math.Max(1, count);
            _testUntil = DateTimeOffset.Now + (duration ?? TimeSpan.FromSeconds(8));
        }

        _ = PollSafelyAsync();
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
            // Do not make the detectors mutually exclusive. Unpackaged desktop apps can
            // receive an "Allowed" WinRT result but still expose no toast collection.
            var counts = PollWindowTitles();
            if (_nativeNotificationAccess)
            {
                try
                {
                    var nativeCounts = await PollWindowsNotificationsAsync();
                    MergeMaximum(counts, nativeCounts);
                }
                catch
                {
                    // Hidden-window detection remains active when WinRT is unavailable.
                }
            }

            ApplyTestOverride(counts);
            PublishIfChanged(counts);
        }
        catch
        {
            var counts = PollWindowTitles();
            ApplyTestOverride(counts);
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
            string displayName;
            string appUserModelId;
            try
            {
                displayName = notification.AppInfo.DisplayInfo.DisplayName ?? string.Empty;
                appUserModelId = notification.AppInfo.AppUserModelId ?? string.Empty;
            }
            catch
            {
                continue;
            }

            var app = _apps.FirstOrDefault(candidate =>
                IsNotificationIdentityMatch(candidate, displayName, appUserModelId));
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

                result[app.Id] = ExtractUnreadCount(signature);
            }
        }

        return result;
    }

    private static string GetWindowTitleSignature(AppDefinition app)
    {
        var titles = new List<string>();
        var processIds = new HashSet<uint>();
        foreach (var processName in app.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        processIds.Add((uint)process.Id);
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

        // Process.MainWindowTitle ignores hidden/minimized-to-tray windows. Chat apps
        // commonly keep their unread title on such a top-level window, so enumerate all
        // top-level handles belonging to every matching process.
        if (processIds.Count > 0)
        {
            NativeMethods.EnumWindows((handle, _) =>
            {
                try
                {
                    NativeMethods.GetWindowThreadProcessId(handle, out var processId);
                    if (!processIds.Contains(processId)) return true;

                    var length = NativeMethods.GetWindowTextLength(handle);
                    if (length <= 0) return true;
                    var buffer = new StringBuilder(length + 1);
                    NativeMethods.GetWindowText(handle, buffer, buffer.Capacity);
                    var title = buffer.ToString();
                    if (!string.IsNullOrWhiteSpace(title)) titles.Add(title);
                }
                catch
                {
                    // A window can disappear while it is being enumerated.
                }

                return true;
            }, IntPtr.Zero);
        }

        return string.Join(" | ", titles.Distinct(StringComparer.Ordinal));
    }

    public static int ExtractUnreadCount(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return 0;

        var maximum = 0;
        foreach (var pattern in UnreadTitlePatterns)
        {
            foreach (Match match in pattern.Matches(title))
            {
                if (int.TryParse(match.Groups["count"].Value, out var value))
                {
                    maximum = Math.Max(maximum, value);
                }
            }
        }

        return maximum > 0 ? maximum : UnreadMarkerPattern.IsMatch(title) ? 1 : 0;
    }

    private static bool IsNotificationIdentityMatch(AppDefinition app, params string[] identities)
    {
        var normalizedIdentities = identities
            .Select(NormalizeIdentity)
            .Where(value => value.Length > 0)
            .ToArray();
        if (normalizedIdentities.Length == 0) return false;

        var hints = (app.NotificationNames ?? [])
            .Concat(app.Keywords ?? [])
            .Concat(app.ProcessNames ?? [])
            .Concat((app.ExecutableNames ?? []).Select(Path.GetFileNameWithoutExtension))
            .Append(app.DisplayName)
            .SelectMany(value => (value ?? string.Empty).Split(['/', '|'],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(NormalizeIdentity)
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalizedIdentities.Any(identity => hints.Any(hint =>
            identity.Contains(hint, StringComparison.Ordinal) ||
            hint.Contains(identity, StringComparison.Ordinal)));
    }

    private static string NormalizeIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static void MergeMaximum(Dictionary<string, int> target,
        IReadOnlyDictionary<string, int> source)
    {
        foreach (var pair in source)
        {
            target[pair.Key] = Math.Max(target.GetValueOrDefault(pair.Key), pair.Value);
        }
    }

    private void ApplyTestOverride(Dictionary<string, int> counts)
    {
        lock (_sync)
        {
            if (_testAppId is null) return;
            if (DateTimeOffset.Now >= _testUntil)
            {
                _testAppId = null;
                _testCount = 0;
                return;
            }

            counts[_testAppId] = Math.Max(counts.GetValueOrDefault(_testAppId), _testCount);
        }
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
