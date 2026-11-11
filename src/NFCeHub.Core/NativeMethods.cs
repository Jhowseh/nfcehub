using System.Runtime.InteropServices;

namespace NFCeHub.Core;

internal static class NativeMethods
{
    internal const int MoveFileReplaceExisting = 0x1;

    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
}
