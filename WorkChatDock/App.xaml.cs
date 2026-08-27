using System.Windows;
using System.Windows.Threading;
using WorkChatDock.Services;
using WorkChatDock.ViewModels;

namespace WorkChatDock;

public partial class App : Application
{
    private readonly SingleInstanceService _singleInstance = new();
    private ConfigService? _configService;
    private NotificationMonitorService? _notificationMonitor;
    private TrayIconService? _trayIcon;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private DockFlyout? _flyout;
    private DispatcherTimer? _flashTimer;
    private bool _flashPhase;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var smokeTest = e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        var previewMode = e.Args.Contains("--preview", StringComparer.OrdinalIgnoreCase);

        var command = e.Args.Contains("--launch-all", StringComparer.OrdinalIgnoreCase)
            ? "launch-all"
            : e.Args.Contains("--show-flyout", StringComparer.OrdinalIgnoreCase)
                ? "show-flyout"
                : e.Args.Contains("--test-notification", StringComparer.OrdinalIgnoreCase)
                    ? "test-notification"
                : "show";
        if (!_singleInstance.TryAcquire())
        {
            await SingleInstanceService.SendCommandAsync(command);
            Shutdown();
            return;
        }

        _singleInstance.CommandReceived += OnCommandReceived;
        try
        {
            _configService = new ConfigService();
            _notificationMonitor = new NotificationMonitorService();
            var shortcutService = new ShortcutService();
            _viewModel = new MainViewModel(
                _configService,
                new AppDiscoveryService(),
                new AppLauncherService(),
                _notificationMonitor,
                shortcutService,
                new TrayVisibilityService());
            _mainWindow = new MainWindow(_viewModel);
            MainWindow = _mainWindow;
            _flyout = new DockFlyout(_viewModel);
            _trayIcon = new TrayIconService();
            WireTrayEvents();

            _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(620) };
            _flashTimer.Tick += (_, _) =>
            {
                _flashPhase = !_flashPhase;
                RefreshTrayIcon();
            };
            _viewModel.TrayVisualChanged += OnTrayVisualChanged;

            using (var icon = IconFactory.CreateAggregateIcon([]))
            {
                _trayIcon.SetIcon((System.Drawing.Icon)icon.Clone(), "WorkChat Dock 正在启动");
            }

            await _viewModel.InitializeAsync(
                // The installer/release script owns the desktop shortcut. Do not rewrite
                // its target from inside an app-container or sandboxed launch context.
                createShortcut: false,
                enableNotifications: !smokeTest && !previewMode);
            RefreshTrayIcon();

            if (smokeTest)
            {
                _mainWindow.Show();
                await Task.Delay(1200);
                ExitApplication();
                return;
            }

            if (previewMode)
            {
                _mainWindow.ShowControlPanel();
                return;
            }

            if (e.Args.Contains("--launch-all", StringComparer.OrdinalIgnoreCase))
            {
                await _viewModel.LaunchAllAsync();
            }
            else if (e.Args.Contains("--test-notification", StringComparer.OrdinalIgnoreCase))
            {
                _viewModel.TriggerNotificationTest();
                _mainWindow.ShowControlPanel();
            }
            else if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase))
            {
                _mainWindow.ShowControlPanel();
            }
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            if (!smokeTest)
            {
                var detail = string.IsNullOrWhiteSpace(exception.Message)
                    ? $"{exception.GetType().Name}（0x{exception.HResult:X8}）"
                    : exception.Message;
                MessageBox.Show($"WorkChat Dock 启动失败：\n{detail}", "WorkChat Dock",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ExitApplication();
        }
    }

    private void WireTrayEvents()
    {
        if (_trayIcon is null) return;
        _trayIcon.Hovered += () => Dispatcher.BeginInvoke(() => _flyout?.ShowNearCursor());
        _trayIcon.LeftClicked += () => Dispatcher.BeginInvoke(async () =>
        {
            var unread = _viewModel?.CurrentUnreadApp;
            if (unread is not null && _viewModel is not null)
            {
                _flyout?.HideFlyout();
                await _viewModel.LaunchAppAsync(unread);
            }
            else
            {
                _flyout?.ShowNearCursor();
            }
        });
        _trayIcon.LaunchAllRequested += () => Dispatcher.BeginInvoke(async () =>
        {
            if (_viewModel is not null) await _viewModel.LaunchAllAsync();
        });
        _trayIcon.OpenSettingsRequested += () => Dispatcher.BeginInvoke(() => _mainWindow?.ShowControlPanel());
        _trayIcon.ExitRequested += () => Dispatcher.BeginInvoke(ExitApplication);
    }

    private void OnCommandReceived(string command)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            if (string.Equals(command, "launch-all", StringComparison.OrdinalIgnoreCase) && _viewModel is not null)
            {
                await _viewModel.LaunchAllAsync();
            }
            else if (string.Equals(command, "show-flyout", StringComparison.OrdinalIgnoreCase))
            {
                _flyout?.ShowNearCursor(holdForPreview: true);
            }
            else if (string.Equals(command, "test-notification", StringComparison.OrdinalIgnoreCase) &&
                     _viewModel is not null)
            {
                _viewModel.TriggerNotificationTest();
            }
            else
            {
                _mainWindow?.ShowControlPanel();
            }
        });
    }

    private void OnTrayVisualChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            var hasUnread = _viewModel?.CurrentUnreadApp is not null;
            if (hasUnread)
            {
                if (_flashTimer?.IsEnabled != true) _flashTimer?.Start();
            }
            else
            {
                _flashTimer?.Stop();
                _flashPhase = false;
            }

            RefreshTrayIcon();
        });
    }

    private void RefreshTrayIcon()
    {
        if (_trayIcon is null || _viewModel is null) return;
        var unread = _viewModel.CurrentUnreadApp;
        if (unread is not null)
        {
            _trayIcon.SetIcon(
                IconFactory.CreateAlertIcon(unread.Definition, _flashPhase),
                $"{unread.DisplayName}：{unread.UnreadCount} 条新消息");
        }
        else
        {
            _trayIcon.SetIcon(
                IconFactory.CreateAggregateIcon(_viewModel.DockApps.Select(app => app.Definition).ToList()),
                "WorkChat Dock · 鼠标移入展开");
        }
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        _flashTimer?.Stop();
        _flyout?.Close();
        _mainWindow?.RequestRealClose();
        Shutdown();
    }

    private static void WriteStartupError(Exception exception)
    {
        try
        {
            var directory = Environment.GetEnvironmentVariable("WORKCHATDOCK_DATA_DIR")
                            ?? Path.Combine(Path.GetTempPath(), "WorkChatDock");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "startup-error.log"), exception.ToString());
        }
        catch
        {
            // Error logging is best effort only.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.TrayVisualChanged -= OnTrayVisualChanged;
            _viewModel.Dispose();
        }
        _notificationMonitor?.Dispose();
        _trayIcon?.Dispose();
        _singleInstance.CommandReceived -= OnCommandReceived;
        _singleInstance.Dispose();
        base.OnExit(e);
    }
}
