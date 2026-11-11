using System;
using System.IO;

namespace NFCeHub.Core;

public static class AppPaths
{
    public static string AppDir { get; } = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    public static string ConfigFile { get; set; } = Path.Combine(AppDir, "config.ini");

    public static string LogDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NFCeHub");
}
