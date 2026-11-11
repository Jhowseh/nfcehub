using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NFCeHub;

internal static class NativeMethods
{
    private const int SwRestore = 9;
    private const int SwShow = 5;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    public static void SetAppId(string name)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID("PBtec." + name);
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    public static bool BringToFront(string title)
    {
        var hwnd = FindWindow(null, title);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);
        SetForegroundWindow(hwnd);
        return true;
    }

    public static void Raise(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
        SetForegroundWindow(new WindowInteropHelper(window).Handle);
    }
}
