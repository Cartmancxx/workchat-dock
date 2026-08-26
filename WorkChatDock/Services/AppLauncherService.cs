using System.Diagnostics;
using WorkChatDock.Interop;
using WorkChatDock.Models;

namespace WorkChatDock.Services;

public sealed class AppLauncherService
{
    public bool IsRunning(AppDefinition app)
    {
        var processes = FindRunningProcesses(app).ToList();
        try
        {
            return processes.Any(process =>
            {
                try { return !process.HasExited; }
                catch { return false; }
            });
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    public Task<bool> LaunchOrActivateAsync(AppDefinition app)
    {
        return Task.Run(() =>
        {
            var running = FindRunningProcesses(app).ToList();
            try
            {
                var window = FindBestWindow(running);
                if (window != IntPtr.Zero)
                {
                    NativeMethods.ShowWindowAsync(window, NativeMethods.SwRestore);
                    NativeMethods.SetForegroundWindow(window);
                    return true;
                }

                if (running.Count > 0 && TryActivateWithProtocol(app))
                {
                    return true;
                }

                var launchPath = ResolveLaunchPath(app);
                if (!string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = launchPath,
                            WorkingDirectory = Path.GetDirectoryName(launchPath) ?? string.Empty,
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                return false;
            }
            finally
            {
                foreach (var process in running) process.Dispose();
            }
        });
    }

    private static IEnumerable<Process> FindRunningProcesses(AppDefinition app)
    {
        foreach (var processName in app.ProcessNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                continue;
            }

            foreach (var process in processes)
            {
                yield return process;
            }
        }
    }

    private static IntPtr FindBestWindow(IReadOnlyCollection<Process> processes)
    {
        foreach (var process in processes)
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
            catch
            {
                // Process may have exited between enumeration and inspection.
            }
        }

        var processIds = new HashSet<uint>(processes.Select(process => (uint)process.Id));
        var result = IntPtr.Zero;
        NativeMethods.EnumWindows((window, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out var processId);
            if (processIds.Contains(processId) && NativeMethods.IsWindowVisible(window))
            {
                result = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static string? ResolveLaunchPath(AppDefinition app)
    {
        var configuredPath = app.ExecutablePath;
        if (!string.Equals(app.Id, "dingtalk", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        try
        {
            var executableDirectory = Directory.GetParent(configuredPath);
            var installRoot = executableDirectory?.Parent?.Parent;
            var launcherPath = installRoot is null
                ? null
                : Path.Combine(installRoot.FullName, "DingtalkLauncher.exe");
            return File.Exists(launcherPath) ? launcherPath : configuredPath;
        }
        catch
        {
            return configuredPath;
        }
    }

    private static bool TryActivateWithProtocol(AppDefinition app)
    {
        if (!string.Equals(app.Id, "dingtalk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "dingtalk://dingtalkclient/action/openapp",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
