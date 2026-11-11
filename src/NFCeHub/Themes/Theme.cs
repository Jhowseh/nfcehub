using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace NFCeHub;

public static class Theme
{
    private static readonly Dictionary<string, string> Light = new()
    {
        ["Bg"] = "#FFFFFF", ["Card"] = "#F6F8FA", ["Line"] = "#D1D9E0", ["Text"] = "#1F2328",
        ["Muted"] = "#59636E", ["Faint"] = "#656D76", ["Link"] = "#0969DA", ["Hover"] = "#EEF1F4",
        ["Field"] = "#FFFFFF", ["Green"] = "#1A7F37", ["Red"] = "#CF222E", ["Yellow"] = "#9A6700",
    };

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["Bg"] = "#0D1117", ["Card"] = "#151B23", ["Line"] = "#30363D", ["Text"] = "#E6EDF3",
        ["Muted"] = "#9198A1", ["Faint"] = "#7D8590", ["Link"] = "#4493F8", ["Hover"] = "#1C232D",
        ["Field"] = "#0D1117", ["Green"] = "#3FB950", ["Red"] = "#F85149", ["Yellow"] = "#D29922",
    };

    public const string GradientStart = "#22D3EE";
    public const string GradientEnd = "#8B5CF6";
    public const string Violet = "#8B5CF6";

    public static readonly Dictionary<StatusKind, string> StatusDot = new()
    {
        [StatusKind.Ok] = "#3FB950", [StatusKind.Error] = "#F85149", [StatusKind.Warning] = "#D29922",
        [StatusKind.Neutral] = "#8C959F",
    };

    public static bool IsDark { get; private set; }

    public static Dictionary<string, string> Palette => IsDark ? Dark : Light;

    public static void Apply(ResourceDictionary resources)
    {
        IsDark = WindowsDarkMode();
        var palette = Palette;
        foreach (var pair in palette)
        {
            resources[pair.Key + "Brush"] = Brush(pair.Value);
        }
        resources["VioletBrush"] = Brush(Violet);
        resources["WhiteBrush"] = Brush("#FFFFFF");
        resources["GradientBrush"] = Gradient(GradientStart, GradientEnd);
        resources["GradientHoverBrush"] = Gradient(Mix(GradientStart, "#FFFFFF", 0.15), Mix(GradientEnd, "#FFFFFF", 0.15));
        resources["GradientDisabledBrush"] = Gradient(Mix(GradientStart, palette["Bg"], 0.55), Mix(GradientEnd, palette["Bg"], 0.55));
        resources["TextDisabledBrush"] = Brush(Mix(palette["Text"], palette["Bg"], 0.4));
        resources["WhiteDisabledBrush"] = Brush(Mix("#FFFFFF", palette["Bg"], 0.4));
    }

    public static Brush Resource(string key) => (Brush)Application.Current.Resources[key + "Brush"];

    public static Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
            byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
            byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
    }

    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush(ToColor(hex));
        brush.Freeze();
        return brush;
    }

    public static string Mix(string a, string b, double t)
    {
        var x = ToColor(a);
        var y = ToColor(b);
        byte Lerp(byte p, byte q) => (byte)Math.Round(p + (q - p) * t);
        return $"#{Lerp(x.R, y.R):X2}{Lerp(x.G, y.G):X2}{Lerp(x.B, y.B):X2}";
    }

    private static LinearGradientBrush Gradient(string start, string end)
    {
        var brush = new LinearGradientBrush(ToColor(start), ToColor(end), new Point(0, 0.5), new Point(1, 0.5));
        brush.Freeze();
        return brush;
    }

    private static bool WindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException || e is UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Prepare(Window window)
    {
        window.Background = Resource("Bg");
        window.SourceInitialized += (_, _) =>
        {
            if (!IsDark)
            {
                return;
            }
            var hwnd = new WindowInteropHelper(window).Handle;
            var on = 1;
            foreach (var attribute in new[] { 20, 19 })
            {
                try
                {
                    DwmSetWindowAttribute(hwnd, attribute, ref on, sizeof(int));
                }
                catch (DllNotFoundException)
                {
                }
                catch (EntryPointNotFoundException)
                {
                }
            }
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
