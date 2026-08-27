using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using WorkChatDock.Models;
using WorkChatDock.Services;

namespace WorkChatDock.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ConfigService _configService;
    private readonly AppDiscoveryService _discoveryService;
    private readonly AppLauncherService _launcherService;
    private readonly NotificationMonitorService _notificationMonitor;
    private readonly ShortcutService _shortcutService;
    private readonly TrayVisibilityService _trayVisibilityService;
    private readonly DispatcherTimer _runningTimer;
    private DockConfig _config = new();
    private bool _isBusy;
    private string _statusMessage = "正在初始化…";
    private string _notificationMode = "正在检测";
    private int _notificationTestIndex;

    public MainViewModel(
        ConfigService configService,
        AppDiscoveryService discoveryService,
        AppLauncherService launcherService,
        NotificationMonitorService notificationMonitor,
        ShortcutService shortcutService,
        TrayVisibilityService trayVisibilityService)
    {
        _configService = configService;
        _discoveryService = discoveryService;
        _launcherService = launcherService;
        _notificationMonitor = notificationMonitor;
        _shortcutService = shortcutService;
        _trayVisibilityService = trayVisibilityService;
        _notificationMonitor.UnreadChanged += OnUnreadChanged;

        _runningTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _runningTimer.Tick += (_, _) => RefreshRunningStates();
    }

    public ObservableCollection<AppItemViewModel> Apps { get; } = [];
    public ObservableCollection<AppItemViewModel> DockApps { get; } = [];

    public bool ManageTrayIcons
    {
        get => _config.ManageTrayIcons;
        set
        {
            if (_config.ManageTrayIcons == value) return;
            _config.ManageTrayIcons = value;
            OnPropertyChanged();
            _ = SaveTrayPreferenceAsync(value);
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string NotificationMode
    {
        get => _notificationMode;
        private set
        {
            if (_notificationMode == value) return;
            _notificationMode = value;
            OnPropertyChanged();
        }
    }

    public AppItemViewModel? CurrentUnreadApp => Apps
        .Where(app => app.HasUnread)
        .OrderByDescending(app => app.LastNotificationTime)
        .FirstOrDefault();

    public event Action? TrayVisualChanged;
    public event Action<string>? NoticeRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync(bool createShortcut, bool enableNotifications = true)
    {
        IsBusy = true;
        _config = await _configService.LoadAsync();
        Apps.Clear();
        foreach (var definition in _config.Apps)
        {
            var item = new AppItemViewModel(definition);
            item.PropertyChanged += OnAppItemPropertyChanged;
            Apps.Add(item);
        }

        await RescanAsync(force: false);
        RefreshRunningStates();

        if (createShortcut && _config.CreateDesktopShortcut)
        {
            try
            {
                _shortcutService.EnsureLaunchAllShortcut();
            }
            catch
            {
                // The button in the control panel lets the user retry later.
            }
        }

        if (enableNotifications)
        {
            await _notificationMonitor.InitializeAsync(_config.Apps);
            NotificationMode = _notificationMonitor.ModeText;
        }
        else
        {
            NotificationMode = "冒烟测试";
        }
        _runningTimer.Start();
        IsBusy = false;
        StatusMessage = $"已定位 {Apps.Count(app => app.IsFound)}/{Apps.Count} 个办公软件";
        RefreshDockApps();
        if (ManageTrayIcons)
        {
            _ = ApplyTrayVisibilityAsync(showStatus: false);
        }
        TrayVisualChanged?.Invoke();
    }

    public async Task RescanAsync(bool force = true)
    {
        IsBusy = true;
        StatusMessage = "正在自动索引软件位置…";
        var found = Apps.Count(app => !force && app.IsFound);
        var targets = Apps.Where(app => force || !app.IsFound).ToList();
        using var gate = new SemaphoreSlim(4, 4);
        var tasks = targets.Select(async app =>
        {
            await gate.WaitAsync();
            try
            {
                var wasFound = app.IsFound;
                var path = await _discoveryService.DiscoverAsync(app.Definition);
                return (App: app, Path: path, WasFound: wasFound);
            }
            catch
            {
                return (App: app, Path: (string?)null, WasFound: false);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        foreach (var result in await Task.WhenAll(tasks))
        {
            if (string.IsNullOrWhiteSpace(result.Path)) continue;
            result.App.ExecutablePath = result.Path;
            result.App.Definition.ExecutablePathIsManual = false;
            if (_config.AutoEnableDiscoveredApps && !result.WasFound && !result.App.Definition.IsCustom)
            {
                result.App.Enabled = true;
            }
            found++;
        }

        _config.AutoEnableDiscoveredApps = false;
        await _configService.SaveAsync(_config);
        RefreshRunningStates();
        IsBusy = false;
        StatusMessage = $"自动索引完成：找到 {found}/{Apps.Count} 个软件";
        RefreshDockApps();
        if (ManageTrayIcons)
        {
            _ = ApplyTrayVisibilityAsync(showStatus: false);
        }
        TrayVisualChanged?.Invoke();
    }

    public async Task LaunchAllAsync()
    {
        StatusMessage = "正在依次启动办公软件…";
        var launched = 0;
        var failed = new List<string>();
        foreach (var app in Apps.Where(app => app.Enabled))
        {
            if (!app.IsFound)
            {
                app.ExecutablePath = await _discoveryService.DiscoverAsync(app.Definition);
            }

            if (await LaunchAppAsync(app, showNoticeOnFailure: false))
            {
                launched++;
            }
            else
            {
                failed.Add(app.DisplayName);
            }

            await Task.Delay(Math.Clamp(_config.LaunchDelayMilliseconds, 100, 3000));
        }

        await _configService.SaveAsync(_config);
        StatusMessage = failed.Count == 0
            ? $"已处理 {launched}/{Apps.Count(app => app.Enabled)} 个软件"
            : $"已打开 {launched} 个；未打开：{string.Join("、", failed)}";
        RefreshRunningStates();
    }

    public async Task<bool> LaunchAppAsync(AppItemViewModel app, bool showNoticeOnFailure = true)
    {
        if (!app.IsFound)
        {
            app.ExecutablePath = await _discoveryService.DiscoverAsync(app.Definition);
        }

        var launched = await _launcherService.LaunchOrActivateAsync(app.Definition);
        if (launched)
        {
            _notificationMonitor.MarkAcknowledged(app.Id);
            app.UnreadCount = 0;
            TrayVisualChanged?.Invoke();
            await Task.Delay(250);
            app.IsRunning = _launcherService.IsRunning(app.Definition);
        }
        else if (showNoticeOnFailure)
        {
            NoticeRequested?.Invoke($"尚未找到 {app.DisplayName}，请手动选择它的 exe 文件。");
        }

        return launched;
    }

    public async Task SetExecutablePathAsync(AppItemViewModel app, string path)
    {
        app.ExecutablePath = path;
        app.Definition.ExecutablePathIsManual = true;
        app.Enabled = true;
        await _configService.SaveAsync(_config);
        StatusMessage = $"已保存 {app.DisplayName} 的位置";
        RefreshDockApps();
        TrayVisualChanged?.Invoke();
    }

    public async Task AddCustomAppAsync(string displayName, string executablePath)
    {
        var fileName = Path.GetFileName(executablePath);
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        var idBase = new string(displayName.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(idBase)) idBase = "custom-app";
        var id = idBase;
        var suffix = 2;
        while (_config.Apps.Any(app => string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"{idBase}-{suffix++}";
        }

        var definition = new AppDefinition
        {
            Id = id,
            DisplayName = displayName.Trim(),
            AccentColor = ColorFor(displayName),
            Keywords = [displayName.Trim(), processName],
            ProcessNames = [processName],
            ExecutableNames = [fileName],
            NotificationNames = [displayName.Trim(), processName],
            SearchRoots = [Path.GetDirectoryName(executablePath) ?? string.Empty],
            ExecutablePath = executablePath,
            ExecutablePathIsManual = true,
            Enabled = true,
            IsCustom = true
        };
        _config.Apps.Add(definition);
        var item = new AppItemViewModel(definition);
        item.PropertyChanged += OnAppItemPropertyChanged;
        Apps.Add(item);
        await _configService.SaveAsync(_config);
        RefreshDockApps();
        StatusMessage = $"已添加 {definition.DisplayName}";
        TrayVisualChanged?.Invoke();
    }

    public async Task RemoveCustomAppAsync(AppItemViewModel app)
    {
        if (!app.Definition.IsCustom) return;
        app.PropertyChanged -= OnAppItemPropertyChanged;
        Apps.Remove(app);
        _config.Apps.Remove(app.Definition);
        await _configService.SaveAsync(_config);
        RefreshDockApps();
        StatusMessage = $"已移除 {app.DisplayName}";
        TrayVisualChanged?.Invoke();
    }

    public void CreateDesktopShortcut()
    {
        _shortcutService.CreateLaunchAllShortcut();
        StatusMessage = $"桌面快捷方式已创建：{Path.GetFileName(_shortcutService.ShortcutPath)}";
        NoticeRequested?.Invoke("桌面已创建“一键打开办公软件”。");
    }

    public void TriggerNotificationTest()
    {
        var candidates = DockApps.Count > 0 ? DockApps : Apps;
        if (candidates.Count == 0)
        {
            NoticeRequested?.Invoke("请先添加或定位至少一个聊天软件。");
            return;
        }

        var app = candidates[_notificationTestIndex++ % candidates.Count];
        _notificationMonitor.TriggerTestNotification(app.Id, 3, TimeSpan.FromSeconds(8));
        StatusMessage = $"提醒测试中：托盘聚合图标应切换为 {app.DisplayName} 并闪烁 8 秒";
    }

    public async Task ToggleStartWithWindowsAsync(bool enabled)
    {
        _config.StartWithWindows = enabled;
        StartupService.SetEnabled(enabled);
        await _configService.SaveAsync(_config);
    }

    private void RefreshRunningStates()
    {
        foreach (var app in Apps)
        {
            app.IsRunning = _launcherService.IsRunning(app.Definition);
        }
    }

    private void RefreshDockApps()
    {
        var desired = Apps.Where(app => app.Enabled && app.IsFound).ToList();
        if (DockApps.SequenceEqual(desired)) return;
        DockApps.Clear();
        foreach (var app in desired) DockApps.Add(app);
    }

    private async Task SaveTrayPreferenceAsync(bool enabled)
    {
        try
        {
            await _configService.SaveAsync(_config);
            if (enabled)
            {
                await ApplyTrayVisibilityAsync(showStatus: true);
            }
        }
        catch
        {
            // Best effort: a later settings change retries persistence.
        }
    }

    private async Task ApplyTrayVisibilityAsync(bool showStatus)
    {
        var result = await _trayVisibilityService.ApplyAsync(_config.Apps);
        if (showStatus)
        {
            StatusMessage = result.Supported
                ? "已固定 WorkChat Dock，并收纳其他聊天软件托盘图标"
                : "当前 Windows 未提供通知区域图标设置";
        }
    }

    private static string ColorFor(string value)
    {
        var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(value);
        var colors = new[] { "#3B82F6", "#8B5CF6", "#06B6D4", "#10B981", "#F97316", "#EC4899" };
        return colors[(hash & int.MaxValue) % colors.Length];
    }

    private void OnUnreadChanged(IReadOnlyDictionary<string, int> counts)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            foreach (var app in Apps)
            {
                app.UnreadCount = counts.TryGetValue(app.Id, out var count) ? count : 0;
            }

            TrayVisualChanged?.Invoke();
        });
    }

    private async void OnAppItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.Enabled))
        {
            RefreshDockApps();
            try
            {
                await _configService.SaveAsync(_config);
            }
            catch
            {
                // A later settings change will retry persistence.
            }
        }
    }

    public void Dispose()
    {
        _runningTimer.Stop();
        _notificationMonitor.UnreadChanged -= OnUnreadChanged;
        foreach (var app in Apps)
        {
            app.PropertyChanged -= OnAppItemPropertyChanged;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
