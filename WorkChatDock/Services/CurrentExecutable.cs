using System.Diagnostics;

namespace WorkChatDock.Services;

internal static class CurrentExecutable
{
    internal static string Path
    {
        get
        {
            try
            {
                var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath))
                {
                    return mainModulePath;
                }
            }
            catch
            {
                // Fall back to the runtime-provided path.
            }

            return Environment.ProcessPath
                   ?? throw new InvalidOperationException("找不到 WorkChat Dock 程序路径。");
        }
    }
}
