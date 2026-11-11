using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NFCeHub;

public static class FolderPicker
{
    private const uint PickFolders = 0x20;
    private const uint ForceFileSystem = 0x40;
    private const uint FileSystemPath = 0x80058000;
    private const int Cancelled = unchecked((int)0x800704C7);

    public static string Pick(Window owner, string title, string initialFolder)
    {
        var dialog = (IFileDialog)new FileOpenDialogCom();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | PickFolders | ForceFileSystem);
            dialog.SetTitle(title);
            if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder) &&
                SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, typeof(IShellItem).GUID, out var folder) == 0)
            {
                dialog.SetFolder(folder);
            }
            var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            var result = dialog.Show(hwnd);
            if (result == Cancelled)
            {
                return null;
            }
            Marshal.ThrowExceptionForHR(result);
            dialog.GetResult(out var item);
            item.GetDisplayName(FileSystemPath, out var pathPointer);
            try
            {
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogCom
    {
    }

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint count, IntPtr filterSpec);

        void SetFileTypeIndex(uint index);

        void GetFileTypeIndex(out uint index);

        void Advise(IntPtr events, out uint cookie);

        void Unadvise(uint cookie);

        void SetOptions(uint options);

        void GetOptions(out uint options);

        void SetDefaultFolder(IShellItem item);

        void SetFolder(IShellItem item);

        void GetFolder(out IShellItem item);

        void GetCurrentSelection(out IShellItem item);

        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

        void GetResult(out IShellItem item);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, [MarshalAs(UnmanagedType.LPStruct)] Guid handler,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr result);

        void GetParent(out IShellItem parent);

        void GetDisplayName(uint name, out IntPtr display);

        void GetAttributes(uint mask, out uint attributes);

        void Compare(IShellItem item, uint hint, out int order);
    }
}
