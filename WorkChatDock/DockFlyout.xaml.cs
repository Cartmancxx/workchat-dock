using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WorkChatDock.ViewModels;
using Forms = System.Windows.Forms;

namespace WorkChatDock;

public partial class DockFlyout : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _hideTimer;
    private DateTimeOffset _lastShow;

    public DockFlyout(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        RenderTransform = new TranslateTransform();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsMouseOver)
            {
                Hide();
            }
        };
    }

    public void ShowNearCursor(bool holdForPreview = false)
    {
        _hideTimer.Interval = holdForPreview ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(850);
        _hideTimer.Stop();
        var now = DateTimeOffset.Now;
        if (IsVisible && now - _lastShow < TimeSpan.FromMilliseconds(180))
        {
            BeginAutoHide();
            return;
        }

        _lastShow = now;
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }

        UpdateLayout();
        PositionNearTaskbar();

        var transform = (TranslateTransform)RenderTransform;
        transform.Y = 10;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        BeginAutoHide();
    }

    public void HideFlyout()
    {
        _hideTimer.Stop();
        Hide();
    }

    private void PositionNearTaskbar()
    {
        var cursor = Forms.Control.MousePosition;
        var screen = Forms.Screen.FromPoint(cursor);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPixels = Math.Max(1, ActualWidth * dpi.DpiScaleX);
        var heightPixels = Math.Max(1, ActualHeight * dpi.DpiScaleY);
        const int gap = 7;

        double leftPixels;
        double topPixels;
        if (work.Bottom < bounds.Bottom && cursor.Y >= work.Bottom)
        {
            leftPixels = cursor.X - widthPixels / 2;
            topPixels = work.Bottom - heightPixels - gap;
        }
        else if (work.Top > bounds.Top && cursor.Y <= work.Top)
        {
            leftPixels = cursor.X - widthPixels / 2;
            topPixels = work.Top + gap;
        }
        else if (work.Left > bounds.Left && cursor.X <= work.Left)
        {
            leftPixels = work.Left + gap;
            topPixels = cursor.Y - heightPixels / 2;
        }
        else
        {
            leftPixels = work.Right - widthPixels - gap;
            topPixels = cursor.Y - heightPixels / 2;
        }

        leftPixels = Math.Clamp(leftPixels, work.Left + gap, work.Right - widthPixels - gap);
        topPixels = Math.Clamp(topPixels, work.Top + gap, work.Bottom - heightPixels - gap);
        Left = leftPixels / dpi.DpiScaleX;
        Top = topPixels / dpi.DpiScaleY;
    }

    private void BeginAutoHide()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _hideTimer.Stop();
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => BeginAutoHide();

    private async void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AppItemViewModel app })
        {
            HideFlyout();
            await _viewModel.LaunchAppAsync(app);
        }
    }

    private async void LaunchAllButton_Click(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        await _viewModel.LaunchAllAsync();
    }
}
