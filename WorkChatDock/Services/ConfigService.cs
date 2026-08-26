using System.Text.Json;
using WorkChatDock.Models;

namespace WorkChatDock.Services;

public sealed class ConfigService
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string DataDirectory { get; } = ResolveDataDirectory();

    public string ConfigPath => Path.Combine(DataDirectory, "config.json");

    public async Task<DockConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                await using var stream = File.OpenRead(ConfigPath);
                var loaded = await JsonSerializer.DeserializeAsync<DockConfig>(stream, _jsonOptions);
                if (loaded is not null)
                {
                    MergeNewDefaults(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
            // A damaged config should never prevent the launcher from starting.
        }

        var config = new DockConfig { AutoEnableDiscoveredApps = true };
        await SaveAsync(config);
        return config;
    }

    public async Task SaveAsync(DockConfig config)
    {
        await _saveGate.WaitAsync();
        var temporaryPath = ConfigPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(DataDirectory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, config, _jsonOptions);
            }

            File.Move(temporaryPath, ConfigPath, true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // A stale temporary file is harmless and may be cleaned later.
            }
            _saveGate.Release();
        }
    }

    private static void MergeNewDefaults(DockConfig config)
    {
        var previousVersion = config.Version;
        config.Apps ??= [];
        foreach (var defaultApp in DockConfig.CreateDefaults())
        {
            var existing = config.Apps.FirstOrDefault(app =>
                string.Equals(app.Id, defaultApp.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                config.Apps.Add(defaultApp);
                continue;
            }

            // Keep user choices and discovered paths, while upgrading the built-in
            // detection hints as the catalogue grows between releases.
            existing.DisplayName = defaultApp.DisplayName;
            existing.AccentColor = defaultApp.AccentColor;
            existing.Keywords = Merge(existing.Keywords, defaultApp.Keywords);
            existing.ProcessNames = Merge(existing.ProcessNames, defaultApp.ProcessNames);
            existing.ExecutableNames = Merge(existing.ExecutableNames, defaultApp.ExecutableNames);
            existing.NotificationNames = Merge(existing.NotificationNames, defaultApp.NotificationNames);
            existing.SearchRoots = Merge(existing.SearchRoots, defaultApp.SearchRoots);
            existing.IsCustom = false;
            if (!existing.ExecutablePathIsManual &&
                !string.IsNullOrWhiteSpace(existing.ExecutablePath) &&
                !AppDiscoveryService.IsExpectedExecutable(existing, existing.ExecutablePath))
            {
                existing.ExecutablePath = null;
                existing.Enabled = false;
            }
        }

        if (previousVersion < 3)
        {
            var originalIds = new HashSet<string>(["zalo", "dingtalk", "feishu", "jdme"],
                StringComparer.OrdinalIgnoreCase);
            foreach (var app in config.Apps.Where(app => !app.IsCustom && !originalIds.Contains(app.Id)))
            {
                app.Enabled = false;
            }
        }

        config.Version = 3;
    }

    private static string[] Merge(string[]? current, string[] additions) =>
        (current ?? []).Concat(additions)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string ResolveDataDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("WORKCHATDOCK_DATA_DIR");
        return !string.IsNullOrWhiteSpace(overridePath)
            ? Path.GetFullPath(overridePath)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorkChatDock");
    }
}
