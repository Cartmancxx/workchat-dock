using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WorkChatDock.Interop;

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath,
        out Win32FindData findData, uint flags);
    void GetIDList(out IntPtr pidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
    void GetHotkey(out short hotkey);
    void SetHotkey(short hotkey);
    void GetShowCmd(out int showCommand);
    void SetShowCmd(int showCommand);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength,
        out int iconIndex);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRelative, uint reserved);
    void Resolve(IntPtr hwnd, uint flags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct Win32FindData
{
    public uint FileAttributes;
    public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
    public uint FileSizeHigh;
    public uint FileSizeLow;
    public uint Reserved0;
    public uint Reserved1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string FileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string AlternateFileName;
}

internal static class ShellLinkHelper
{
    internal static string? Resolve(string shortcutPath)
    {
        object? shellLink = null;
        try
        {
            shellLink = new ShellLink();
            ((IPersistFile)shellLink).Load(shortcutPath, 0);
            var builder = new StringBuilder(32768);
            ((IShellLinkW)shellLink).GetPath(builder, builder.Capacity, out _, 0);
            var path = Environment.ExpandEnvironmentVariables(builder.ToString());
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shellLink is not null && Marshal.IsComObject(shellLink))
            {
                Marshal.FinalReleaseComObject(shellLink);
            }
        }
    }

    internal static void Create(string shortcutPath, string targetPath, string arguments,
        string description, string iconPath)
    {
        object? shellLink = null;
        try
        {
            shellLink = new ShellLink();
            var link = (IShellLinkW)shellLink;
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetDescription(description);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? AppContext.BaseDirectory);
            link.SetIconLocation(iconPath, 0);
            link.SetShowCmd(1);
            ((IPersistFile)shellLink).Save(shortcutPath, true);
        }
        finally
        {
            if (shellLink is not null && Marshal.IsComObject(shellLink))
            {
                Marshal.FinalReleaseComObject(shellLink);
            }
        }
    }
}
