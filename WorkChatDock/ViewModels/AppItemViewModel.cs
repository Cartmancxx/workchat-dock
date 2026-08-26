using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WorkChatDock.Models;
using WorkChatDock.Services;

namespace WorkChatDock.ViewModels;

public sealed class AppItemViewModel : INotifyPropertyChanged
{
    private bool _isRunning;
    private int _unreadCount;
    private ImageSource? _iconSource;

    public AppItemViewModel(AppDefinition definition)
    {
        Definition = definition;
        AccentBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(definition.AccentColor)!;
        RefreshIcon();
    }

    public AppDefinition Definition { get; }
    public string Id => Definition.Id;
    public string DisplayName => Definition.DisplayName;
    public Brush AccentBrush { get; }

    public bool Enabled
    {
        get => Definition.Enabled;
        set
        {
            if (Definition.Enabled == value) return;
            Definition.Enabled = value;
            OnPropertyChanged();
        }
    }

    public string? ExecutablePath
    {
        get => Definition.ExecutablePath;
        set
        {
            if (string.Equals(Definition.ExecutablePath, value, StringComparison.OrdinalIgnoreCase)) return;
            Definition.ExecutablePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFound));
            OnPropertyChanged(nameof(PathText));
            OnPropertyChanged(nameof(StatusText));
            RefreshIcon();
        }
    }

    public bool IsFound => !string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath);
    public string PathText => IsFound ? ExecutablePath! : "尚未定位程序文件";

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (_unreadCount == value) return;
            if (value > _unreadCount)
            {
                LastNotificationTime = DateTimeOffset.Now;
            }
            _unreadCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnread));
            OnPropertyChanged(nameof(UnreadText));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool HasUnread => UnreadCount > 0;
    public string UnreadText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();
    public DateTimeOffset LastNotificationTime { get; private set; }

    public string StatusText => HasUnread
        ? $"{UnreadCount} 条新消息"
        : IsRunning
            ? "运行中"
            : IsFound
                ? "待启动"
                : "需要选择位置";

    public ImageSource? IconSource
    {
        get => _iconSource;
        private set
        {
            _iconSource = value;
            OnPropertyChanged();
        }
    }

    public void RefreshIcon()
    {
        IconSource = IconFactory.CreateImageSource(Definition);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
