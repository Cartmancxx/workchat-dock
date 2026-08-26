namespace WorkChatDock.Models;

public sealed class DockConfig
{
    public int Version { get; set; } = 3;
    public int LaunchDelayMilliseconds { get; set; } = 450;
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ManageTrayIcons { get; set; } = true;
    public bool AutoEnableDiscoveredApps { get; set; }
    public List<AppDefinition> Apps { get; set; } = CreateDefaults();

    public static List<AppDefinition> CreateDefaults() => AppCatalog.CreateDefaults();
}
