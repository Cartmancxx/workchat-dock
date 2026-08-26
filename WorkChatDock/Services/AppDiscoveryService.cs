using System.Diagnostics;
using Microsoft.Win32;
using WorkChatDock.Interop;
using WorkChatDock.Models;

namespace WorkChatDock.Services;

public sealed class AppDiscoveryService
{
    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public Task<string?> DiscoverAsync(AppDefinition app, CancellationToken cancellationToken = default) =>
        Task.Run(() => Discover(app, cancellationToken), cancellationToken);

    private static string? Discover(AppDefinition app, CancellationToken cancellationToken)
    {
        var candidates = new List<PathCandidate>();

        if (IsExecutable(app.ExecutablePath) &&
            (app.ExecutablePathIsManual || IsExpectedExecutable(app, app.ExecutablePath!)))
        {
            candidates.Add(new(app.ExecutablePath!, 1000));
        }

        AddRunningProcessCandidates(app, candidates);
        var runningPath = BestCandidate(app, candidates);
        if (runningPath is not null)
        {
            return runningPath;
        }

        AddAppPathsCandidates(app, candidates);
        AddShortcutCandidates(app, candidates, cancellationToken);
        AddRegistryCandidates(app, candidates, cancellationToken);
        AddKnownFolderCandidates(app, candidates, cancellationToken);

        return BestCandidate(app, candidates);
    }

    private static string? BestCandidate(AppDefinition app, IEnumerable<PathCandidate> candidates) =>
        candidates
            .Where(candidate => IsExecutable(candidate.Path) &&
                                (candidate.Score >= 500 || IsExpectedExecutable(app, candidate.Path)))
            .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(candidate => candidate.Score + ScorePath(app, candidate.Path))
            .Select(candidate => candidate.Path)
            .FirstOrDefault();

    private static void AddRunningProcessCandidates(AppDefinition app, ICollection<PathCandidate> candidates)
    {
        foreach (var processName in app.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (IsExecutable(path))
                        {
                            candidates.Add(new(path!, 500));
                        }
                    }
                    catch
                    {
                        // Protected helper processes are expected and can be skipped.
                    }
                }
            }
        }
    }

    private static void AddAppPathsCandidates(AppDefinition app, ICollection<PathCandidate> candidates)
    {
        foreach (var executableName in app.ExecutableNames)
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(
                            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}");
                        var path = key?.GetValue(null)?.ToString();
                        if (IsExecutable(path))
                        {
                            candidates.Add(new(path!, 450));
                        }
                    }
                    catch
                    {
                        // Registry view might not exist.
                    }
                }
        }
    }

    private static void AddShortcutCandidates(AppDefinition app, ICollection<PathCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(folder, "*.lnk", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    MaxRecursionDepth = 6
                });
            }
            catch
            {
                continue;
            }

            foreach (var shortcut in shortcuts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ContainsAny(Path.GetFileNameWithoutExtension(shortcut), app.Keywords))
                {
                    continue;
                }

                var target = ShellLinkHelper.Resolve(shortcut);
                if (IsExecutable(target))
                {
                    candidates.Add(new(target!, 400));
                }
            }
        }
    }

    private static void AddRegistryCandidates(AppDefinition app, ICollection<PathCandidate> candidates,
        CancellationToken cancellationToken)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                foreach (var rootPath in UninstallRoots)
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var root = baseKey.OpenSubKey(rootPath);
                        if (root is null)
                        {
                            continue;
                        }

                        foreach (var subKeyName in root.GetSubKeyNames())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            using var subKey = root.OpenSubKey(subKeyName);
                            var displayName = subKey?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                            if (!ContainsAny(displayName, app.Keywords))
                            {
                                continue;
                            }

                            var iconPath = NormalizeRegistryPath(subKey?.GetValue("DisplayIcon")?.ToString());
                            if (IsExecutable(iconPath))
                            {
                                candidates.Add(new(iconPath!, 350));
                            }

                            var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                            AddExecutablesUnderFolder(app, installLocation, candidates, 320, 4, cancellationToken);
                        }
                    }
                    catch
                    {
                        // Invalid or inaccessible entries are ignored.
                    }
                }
    }

    private static void AddKnownFolderCandidates(AppDefinition app, ICollection<PathCandidate> candidates,
        CancellationToken cancellationToken)
    {
        foreach (var configuredRoot in app.SearchRoots ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expanded = Environment.ExpandEnvironmentVariables(configuredRoot);
            foreach (var root in ExpandWildcardRoot(expanded))
            {
                if (IsExecutable(root))
                {
                    candidates.Add(new(root, 330));
                    continue;
                }

                AddExecutablesUnderFolder(app, root, candidates, 280, 5, cancellationToken);
            }
        }
    }

    private static IEnumerable<string> ExpandWildcardRoot(string path)
    {
        if (!path.Contains('*') && !path.Contains('?'))
        {
            yield return path;
            yield break;
        }

        var parent = Path.GetDirectoryName(path);
        var pattern = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(pattern) ||
            !Directory.Exists(parent))
        {
            yield break;
        }

        IEnumerable<string> matches;
        try
        {
            matches = Directory.EnumerateDirectories(parent, pattern, SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var match in matches)
        {
            yield return match;
        }
    }

    private static void AddExecutablesUnderFolder(AppDefinition app, string? folder,
        ICollection<PathCandidate> candidates, int score, int maxDepth, CancellationToken cancellationToken,
        bool stopAfterFirst = false)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        foreach (var executableName in app.ExecutableNames)
        {
            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
                    MaxRecursionDepth = maxDepth
                };

                foreach (var path in Directory.EnumerateFiles(folder, executableName, options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    candidates.Add(new(path, score));
                    if (stopAfterFirst)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // Continue with the next executable hint.
            }
        }
    }

    private static int ScorePath(AppDefinition app, string path)
    {
        var score = app.ExecutableNames.Any(name =>
            string.Equals(name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) ? 100 : 0;
        score += ContainsAny(path, app.Keywords) ? 40 : 0;
        return score;
    }

    private static bool ContainsAny(string text, IEnumerable<string> values) =>
        values.Any(value => !string.IsNullOrWhiteSpace(value) &&
                            text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
        File.Exists(path);

    internal static bool IsExpectedExecutable(AppDefinition app, string path) =>
        app.ExecutableNames.Any(name =>
            string.Equals(name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeRegistryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = Environment.ExpandEnvironmentVariables(value.Trim());
        if (value.StartsWith('"'))
        {
            var closingQuote = value.IndexOf('"', 1);
            return closingQuote > 1 ? value[1..closingQuote] : value.Trim('"');
        }

        var commaIndex = value.LastIndexOf(',');
        if (commaIndex > 2 && int.TryParse(value[(commaIndex + 1)..], out _))
        {
            value = value[..commaIndex];
        }

        return value.Trim('"', ' ');
    }

    private sealed record PathCandidate(string Path, int Score);
}
