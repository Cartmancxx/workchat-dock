using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WorkChatDock.ViewModels;

namespace WorkChatDock;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.NoticeRequested += OnNoticeRequested;
    }

    public void ShowControlPanel()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void RequestRealClose()
    {
        _allowClose = true;
        Close();
    }

    private async void LaunchAllButton_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.LaunchAllAsync();

    private async void RescanButton_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RescanAsync();

    private async void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddAppWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _viewModel.AddCustomAppAsync(dialog.AppDisplayName, dialog.ExecutablePath);
        }
    }

    private void ShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.CreateDesktopShortcut();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "创建快捷方式", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AppLaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AppItemViewModel app })
        {
            await _viewModel.LaunchAppAsync(app);
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AppItemViewModel app })
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"选择 {app.DisplayName} 的程序文件",
            Filter = "Windows 程序 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = app.IsFound ? Path.GetDirectoryName(app.ExecutablePath) : null
        };
        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.SetExecutablePathAsync(app, dialog.FileName);
        }
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AppItemViewModel app } || !app.Definition.IsCustom)
        {
            return;
        }

        var answer = MessageBox.Show(this, $"从 WorkChat Dock 移除 {app.DisplayName}？", "移除软件",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.RemoveCustomAppAsync(app);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _viewModel.NoticeRequested -= OnNoticeRequested;
        }
    }

    private void OnNoticeRequested(string message)
    {
        Dispatcher.BeginInvoke(() =>
            MessageBox.Show(this, message, "WorkChat Dock", MessageBoxButton.OK, MessageBoxImage.Information));
    }
}
