using System.Text.Json;
using System.IO;
using WorkChatDock.Models;
using WorkChatDock.Services;

var failures = new List<string>();
var checks = new List<object>();

void Check(bool condition, string name, string? details = null)
{
    checks.Add(new { name, passed = condition, details });
    if (!condition) failures.Add(name + (details is null ? string.Empty : $": {details}"));
}

var defaults = DockConfig.CreateDefaults();
Check(defaults.Count >= 20, "mainstream app catalogue", $"actual={defaults.Count}");
Check(defaults.Select(app => app.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == defaults.Count,
    "default ids are unique");
Check(defaults.All(app => app.ExecutableNames.Length > 0 && app.ProcessNames.Length > 0),
    "all apps have discovery hints");
Check(new[] { "wechat", "wecom", "qq", "teams", "slack", "zoom", "telegram", "whatsapp", "signal" }
        .All(id => defaults.Any(app => app.Id == id)),
    "domestic and international catalogue entries exist");

var testData = Path.Combine(AppContext.BaseDirectory, "smoke-data");
Environment.SetEnvironmentVariable("WORKCHATDOCK_DATA_DIR", testData);
var configService = new ConfigService();
var config = await configService.LoadAsync();
Check(File.Exists(configService.ConfigPath), "config file created", configService.ConfigPath);
Check(config.Apps.Count >= 20, "config catalogue migration", $"actual={config.Apps.Count}");
await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => configService.SaveAsync(config)));
await using (var configStream = File.OpenRead(configService.ConfigPath))
{
    var saved = await JsonSerializer.DeserializeAsync<DockConfig>(configStream);
    Check(saved?.Apps.Count == config.Apps.Count, "concurrent config saves remain atomic");
}

var discovery = new AppDiscoveryService();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
foreach (var app in config.Apps.Where(app => new[] { "zalo", "dingtalk", "feishu", "jdme" }.Contains(app.Id)))
{
    var path = await discovery.DiscoverAsync(app, timeout.Token);
    Check(path is not null && File.Exists(path), $"discover {app.DisplayName}", path ?? "not found");
    app.ExecutablePath = path;
}

using (var aggregate = IconFactory.CreateAggregateIcon(config.Apps))
{
    Check(aggregate.Width > 0 && aggregate.Height > 0, "aggregate tray icon generated");
}

using (var alert = IconFactory.CreateAlertIcon(config.Apps[0], true))
{
    Check(alert.Width > 0 && alert.Height > 0, "alert tray icon generated");
}

var testDesktop = Path.Combine(AppContext.BaseDirectory, "smoke-desktop");
Environment.SetEnvironmentVariable("WORKCHATDOCK_DESKTOP_DIR", testDesktop);
var shortcutService = new ShortcutService();
shortcutService.CreateLaunchAllShortcut();
Check(File.Exists(shortcutService.ShortcutPath), "desktop shortcut generated", shortcutService.ShortcutPath);
var shortcutWriteTime = File.GetLastWriteTimeUtc(shortcutService.ShortcutPath);
shortcutService.EnsureLaunchAllShortcut();
Check(File.GetLastWriteTimeUtc(shortcutService.ShortcutPath) == shortcutWriteTime,
    "existing shortcut is not overwritten");

var invalidExecutable = Path.Combine(AppContext.BaseDirectory, "invalid-launch.exe");
await File.WriteAllTextAsync(invalidExecutable, "not a Windows executable");
var invalidApp = new AppDefinition
{
    Id = "invalid",
    DisplayName = "Invalid",
    ProcessNames = ["WorkChatDockLaunchTestProcessThatDoesNotExist"],
    ExecutableNames = ["invalid-launch.exe"],
    ExecutablePath = invalidExecutable
};
var invalidLaunchResult = await new AppLauncherService().LaunchOrActivateAsync(invalidApp);
Check(!invalidLaunchResult, "launch failure is contained without crashing");

var result = new
{
    passed = failures.Count == 0,
    checks,
    failures
};
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
return failures.Count == 0 ? 0 : 1;
