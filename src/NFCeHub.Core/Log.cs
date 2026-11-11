using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NFCeHub.Core;

public static class Log
{
    private const long MaxBytes = 1_000_000;
    private const int Backups = 2;
    private static readonly object Gate = new();
    private static string path;

    public static void Setup(string name)
    {
        Directory.CreateDirectory(AppPaths.LogDir);
        path = Path.Combine(AppPaths.LogDir, name + ".log");
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warning(string message) => Write("WARNING", message, null);

    public static void Error(string message, Exception error = null) => Write("ERROR", message, error);

    private static void Write(string level, string message, Exception error)
    {
        if (path == null)
        {
            return;
        }
        var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture) + " " + level + " " +
                   message + (error != null ? Environment.NewLine + error : "") + Environment.NewLine;
        lock (Gate)
        {
            try
            {
                Rotate();
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void Rotate()
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }
        for (var i = Backups; i >= 1; i--)
        {
            var source = i == 1 ? path : path + "." + (i - 1);
            var target = path + "." + i;
            if (File.Exists(target))
            {
                File.Delete(target);
            }
            if (File.Exists(source))
            {
                File.Move(source, target);
            }
        }
    }
}
