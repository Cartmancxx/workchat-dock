namespace WorkChatDock.Models;

public sealed class AppDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#3B82F6";
    public string[] Keywords { get; set; } = [];
    public string[] ProcessNames { get; set; } = [];
    public string[] ExecutableNames { get; set; } = [];
    public string[] NotificationNames { get; set; } = [];
    public string[] SearchRoots { get; set; } = [];
    public string? ExecutablePath { get; set; }
    public bool ExecutablePathIsManual { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsCustom { get; set; }
}
