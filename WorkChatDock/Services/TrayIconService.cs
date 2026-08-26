using System.Windows.Forms;
using Drawing = System.Drawing;

namespace WorkChatDock.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Drawing.Icon? _ownedIcon;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Drawing.Color.FromArgb(28, 32, 42),
            ForeColor = Drawing.Color.White,
            Renderer = new ToolStripProfessionalRenderer(new DarkColorTable())
        };
        menu.Items.Add("全部启动", null, (_, _) => LaunchAllRequested?.Invoke());
        menu.Items.Add("打开控制面板", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出 WorkChat Dock", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Text = "WorkChat Dock",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.MouseMove += (_, _) => Hovered?.Invoke();
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                LeftClicked?.Invoke();
            }
        };
        _notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                OpenSettingsRequested?.Invoke();
            }
        };
    }

    public event Action? Hovered;
    public event Action? LeftClicked;
    public event Action? LaunchAllRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;

    public void SetIcon(Drawing.Icon icon, string tooltip)
    {
        var previous = _ownedIcon;
        _ownedIcon = icon;
        _notifyIcon.Icon = _ownedIcon;
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        previous?.Dispose();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _ownedIcon?.Dispose();
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(52, 59, 74);
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(75, 84, 104);
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(28, 32, 42);
        public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(28, 32, 42);
        public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(28, 32, 42);
        public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(28, 32, 42);
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(70, 78, 96);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(28, 32, 42);
    }
}
