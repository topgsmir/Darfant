using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

// Test-only reader: IPersistFile/IShellLinkW keep Persian filenames Unicode,
// independent of the Windows Script Host's active ANSI code page.
public static class DarfantInstallerQAPaths
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetLongPathName(string path, StringBuilder result, uint capacity);

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);
    }

    public static string[] ReadShortcut(string path)
    {
        object link = new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var shell = (IShellLinkW)link;
            var target = new StringBuilder(32768);
            var working = new StringBuilder(32768);
            shell.GetPath(target, target.Capacity, IntPtr.Zero, 4);
            shell.GetWorkingDirectory(working, working.Capacity);
            return new[] { target.ToString(), working.ToString() };
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }
}
