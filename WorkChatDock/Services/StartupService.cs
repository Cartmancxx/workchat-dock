using Microsoft.Win32;

namespace WorkChatDock.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WorkChatDock";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            var executablePath = CurrentExecutable.Path;
            key.SetValue(ValueName, $"\"{executablePath}\" --background");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
