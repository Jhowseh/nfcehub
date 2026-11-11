using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NFCeHub.Core;

namespace NFCeHub.Manual;

public static class Program
{
    private const double Scale = 2;
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string SampleFolder = @"C:\XML-Contabilidade\Outubro-2026";

    private static readonly Dictionary<string, string> ExtraIcons = new()
    {
        ["User"] = "M19,21 V19 A4,4 0 0 0 15,15 H9 A4,4 0 0 0 5,19 V21 M12,11 A4,4 0 1 0 12,3 A4,4 0 1 0 12,11 Z",
        ["Phone"] = "M7,2 H17 A2,2 0 0 1 19,4 V20 A2,2 0 0 1 17,22 H7 A2,2 0 0 1 5,20 V4 A2,2 0 0 1 7,2 Z M12,18 L12,18.01",
    };

    private static readonly Dictionary<string, string> Markers = new();
    private static string output;
    private static string dialogName;

    [STAThread]
    public static int Main(string[] args)
    {
        output = Path.GetFullPath(args.Length > 0 ? args[0] : "img");
        Directory.CreateDirectory(output);
        UseResourcesOf(typeof(App).Assembly);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var name in new[] { "Icons", "Controls" })
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/NFCeHub;component/Themes/{name}.xaml"),
            });
        }
        LightTheme(app.Resources);
        AppPaths.ConfigFile = SampleConfig();

        CaptureMain();
        CaptureSettings();
        EventManager.RegisterClassHandler(typeof(MessageDialog), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnDialogLoaded));
        dialogName = "import.png";
        Dialogs.ConfirmImport(null, "Notas não lançadas - outubro 2026.pdf", SampleReading());
        dialogName = "export.png";
        var summary = SampleSummary();
        Dialogs.ExportFinished(null, summary,
            Path.Combine(SampleFolder, "Relatorio-NFCe-nao-exportadas-20261111-103512.txt"),
            "178 arquivos XML gravados.");
        SaveAssets();
        WriteMarkers();
        File.Delete(AppPaths.ConfigFile);
        Console.WriteLine("Images: " + output);
        return 0;
    }

    private static void UseResourcesOf(Assembly assembly)
    {
        const BindingFlags shared = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(Application).GetField("_resourceAssembly", shared).SetValue(null, assembly);
        var helper = typeof(UIElement).Assembly.GetType("System.Windows.Navigation.BaseUriHelper");
        helper.GetProperty("ResourceAssembly", shared).SetValue(null, assembly);
    }

    private static void LightTheme(ResourceDictionary resources)
    {
        Theme.Apply(resources);
        typeof(Theme).GetProperty(nameof(Theme.IsDark)).GetSetMethod(true).Invoke(null, new object[] { false });
        var palette = Theme.Palette;
        foreach (var pair in palette)
        {
            resources[pair.Key + "Brush"] = Theme.Brush(pair.Value);
        }
        resources["GradientDisabledBrush"] = Gradient(Theme.Mix(Theme.GradientStart, palette["Bg"], 0.55),
            Theme.Mix(Theme.GradientEnd, palette["Bg"], 0.55));
        resources["TextDisabledBrush"] = Theme.Brush(Theme.Mix(palette["Text"], palette["Bg"], 0.4));
        resources["WhiteDisabledBrush"] = Theme.Brush(Theme.Mix("#FFFFFF", palette["Bg"], 0.4));
    }

    private static LinearGradientBrush Gradient(string start, string end)
    {
        var brush = new LinearGradientBrush(Theme.ToColor(start), Theme.ToColor(end), new Point(0, 0.5), new Point(1, 0.5));
        brush.Freeze();
        return brush;
    }

    private static string SampleConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), "nfcehub-manual-config.ini");
        File.WriteAllText(path, string.Join("\r\n",
            "[database]",
            "host = 192.168.0.10",
            "port = 3050",
            @"path = C:\Syspdv\syspdv_srv.fdb",
            "user = SYSDBA",
            "password = ",
            "",
            "[output]",
            "xml_folder = " + SampleFolder,
            "",
            "[options]",
            "series = 2",
            "include_cancellation = yes",
            "by_month = no",
            "include_unauthorized = no",
            ""), new UTF8Encoding(false));
        return path;
    }

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, Hidden | BindingFlags.Public).GetValue(owner);

    private static void Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);

    private static void CaptureMain()
    {
        var window = new MainWindow();
        var header = Field<Header>(window, "HeaderBar");
        header.SetStatus("", StatusKind.Ok);
        var series = Field<TextBox>(window, "SeriesField");
        var start = Field<TextBox>(window, "StartField");
        var end = Field<TextBox>(window, "EndField");
        foreach (var (s, a, b) in new[] { ("1", "109370", "109420"), ("1", "109455", ""), ("2", "52010", "52100") })
        {
            series.Text = s;
            start.Text = a;
            end.Text = b;
            Call(window, "AddRange");
        }
        series.Text = "2";
        start.Text = "52150";
        end.Text = "52180";

        var host = Detach(window);
        var points = new List<(FrameworkElement Element, string Anchor, string Side)>
        {
            ((FrameworkElement)header.FindName("Dot"), "right", "r"),
            (series, "left", "l"),
            (Field<Button>(window, "AddButton"), "right", "r"),
            (ButtonByText(host, "Importar PDF"), "left", "l"),
            (Field<ScrollViewer>(window, "ListScroll"), "first-row", "l"),
            (Field<TextBox>(window, "FolderField"), "left", "l"),
            (Field<CheckBox>(window, "IncludeCancellation"), "left", "l"),
            (Field<Button>(window, "ExportButton"), "right", "r"),
            (ButtonByText(host, "Abrir pasta"), "left", "l"),
            (ButtonByText(host, "Configurações"), "right", "r"),
        };
        Save(host, "main.png", points);
        window.Close();
    }

    private static void CaptureSettings()
    {
        var window = (SettingsWindow)Activator.CreateInstance(typeof(SettingsWindow), Hidden, null,
            new object[] { null }, CultureInfo.InvariantCulture);
        Call(window, "Notice", "Conectado!", "Green");
        var host = Detach(window);
        var points = new List<(FrameworkElement Element, string Anchor, string Side)>
        {
            (Field<TextBox>(window, "HostField"), "left", "l"),
            (Field<TextBox>(window, "UserField"), "left", "l"),
            (Field<PasswordBox>(window, "PasswordField"), "left", "l"),
            (ButtonByText(host, "Testar conexão"), "left", "l"),
            (ButtonByText(host, "Salvar"), "right", "r"),
        };
        Save(host, "settings.png", points);
        window.Close();
    }

    private static Reading SampleReading()
    {
        var ranges = new List<NoteRange>
        {
            new(1, 109370, 109420),
            new(1, 109455, 109455),
            new(1, 109501, 109530),
            new(2, 52010, 52100),
            new(2, 52133, 52133),
        };
        return new Reading(ranges, ReadingSource.Table, 2);
    }

    private static ExportSummary SampleSummary()
    {
        var results = new List<NoteResult>();
        void Add(int count, NoteStatus status, int files)
        {
            for (var i = 0; i < count; i++)
            {
                var result = new NoteResult(1, 109370 + results.Count) { Status = status };
                for (var f = 0; f < files; f++)
                {
                    result.Files.Add("file.xml");
                }
                results.Add(result);
            }
        }
        Add(160, NoteStatus.Authorized, 1);
        Add(9, NoteStatus.Cancelled, 2);
        Add(3, NoteStatus.Unauthorized, 0);
        Add(2, NoteStatus.NotFound, 0);
        return new ExportSummary(results, SampleFolder, false);
    }

    private static void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        var window = (Window)sender;
        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            var host = Detach(window);
            window.Close();
            Save(host, dialogName, new List<(FrameworkElement, string, string)>());
        }), DispatcherPriority.ContextIdle);
    }

    private static Border Detach(Window window)
    {
        var content = (UIElement)window.Content;
        window.Content = null;
        return new Border { Background = Theme.Resource("Bg"), Child = content };
    }

    private static Button ButtonByText(DependencyObject root, string text) =>
        Descendants(root).OfType<Button>().First(b => b.Content as string == text);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in VisualDescendants(child))
            {
                yield return nested;
            }
        }
    }

    private static void Layout(FrameworkElement element)
    {
        for (var i = 0; i < 3; i++)
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle);
        }
    }

    private static void Save(FrameworkElement host, string name, List<(FrameworkElement Element, string Anchor, string Side)> points)
    {
        Layout(host);
        foreach (var element in VisualDescendants(host))
        {
            TextOptions.SetTextFormattingMode(element, TextFormattingMode.Ideal);
        }
        Layout(host);
        WritePng(Render(host, host.ActualWidth, host.ActualHeight), name);
        if (points.Count == 0)
        {
            return;
        }
        var items = points.Select(p =>
        {
            var e = p.Element;
            var local = p.Anchor switch
            {
                "left" => new Point(0, e.ActualHeight / 2),
                "right" => new Point(e.ActualWidth, e.ActualHeight / 2),
                "first-row" => new Point(0, 26),
                _ => new Point(e.ActualWidth / 2, e.ActualHeight / 2),
            };
            var at = e.TranslatePoint(local, host);
            return string.Format(CultureInfo.InvariantCulture, "[{0:0}, {1:0}, \"{2}\"]", at.X * Scale, at.Y * Scale, p.Side);
        });
        Markers[Path.GetFileNameWithoutExtension(name)] =
            string.Format(CultureInfo.InvariantCulture, "{{\"scale\": {0}, \"points\": [{1}]}}", Scale, string.Join(", ", items));
    }

    private static BitmapSource Render(Visual visual, double width, double height)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * Scale), (int)Math.Ceiling(height * Scale),
            96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void WritePng(BitmapSource bitmap, string name)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name));
        encoder.Save(stream);
        Console.WriteLine("image: " + name);
    }

    private static void SaveAssets()
    {
        var icons = Application.Current.Resources.MergedDictionaries
            .SelectMany(d => d.Keys.OfType<string>()
                .Where(k => k.StartsWith("Icon", StringComparison.Ordinal) && d[k] is Geometry)
                .Select(k => (Name: k.Substring(4), Geometry: (Geometry)d[k])))
            .Concat(ExtraIcons.Select(p => (Name: p.Key, Geometry: Geometry.Parse(p.Value))));
        foreach (var (iconName, geometry) in icons)
        {
            SaveIcon(geometry, $"ic_{iconName.ToLowerInvariant()}.png", Theme.Violet);
        }

        var logo = new Image { Source = Logo.Load(false), Height = 300, Stretch = Stretch.Uniform };
        Layout(logo);
        WritePng(Render(logo, logo.ActualWidth, logo.ActualHeight), "logo.png");

        var bar = new Border { Width = 300, Height = 12, CornerRadius = new CornerRadius(6), Background = Gradient(Theme.GradientStart, Theme.GradientEnd) };
        Layout(bar);
        WritePng(Render(bar, 300, 12), "bar.png");

        var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/Assets/app.ico"),
            BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var largest = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        WritePng(largest, "app_icon.png");
    }

    private static void SaveIcon(Geometry geometry, string name, string color)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = Theme.Brush(color),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        var canvas = new Canvas { Width = 24, Height = 24, Children = { path } };
        var box = new Viewbox { Width = 48, Height = 48, Child = canvas };
        Layout(box);
        WritePng(Render(box, 48, 48), name);
    }

    private static void WriteMarkers()
    {
        var json = "{\n" + string.Join(",\n", Markers.Select(p => $" \"{p.Key}\": {p.Value}")) + "\n}\n";
        File.WriteAllText(Path.Combine(output, "markers.json"), json, new UTF8Encoding(false));
    }
}
