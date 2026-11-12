using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NFCeHub.Core;
using Microsoft.Win32;

namespace NFCeHub;

public sealed class RangeItem
{
    public RangeItem(NoteRange range)
    {
        Range = range;
    }

    public NoteRange Range { get; }

    public string LabelText => Range.Label();

    public string CountText => Format.Plural(Range.Count, "nota", "notas");
}

public partial class MainWindow : Window
{
    private const long MaxNotes = 200_000;
    private static readonly TimeSpan SlideTime = TimeSpan.FromMilliseconds(240);

    private readonly object progressGate = new();
    private readonly DispatcherTimer progressTimer;
    private List<NoteRange> ranges = new();
    private bool busy;
    private bool sliding;
    private CancellationTokenSource stop;
    private (int Done, long Total, string Text) progress;

    public MainWindow()
    {
        InitializeComponent();
        Theme.Prepare(this);
        HeaderBar.Setup(812, "Exportação de XML", "XML das NFC-e",
            "Gera os arquivos XML das NFC-e direto do banco do SysPDV.");
        HeaderBar.SetStatus("Conectando ao banco do SysPDV…", StatusKind.Neutral);

        var settings = AppSettings.Load();
        SeriesField.Text = settings.Series;
        FolderField.Text = settings.XmlFolder;
        IncludeCancellation.IsChecked = settings.IncludeCancellation;
        ByMonth.IsChecked = settings.ByMonth;
        IncludeUnauthorized.IsChecked = settings.IncludeUnauthorized;

        foreach (var field in new[] { SeriesField, StartField, EndField })
        {
            field.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    AddRange();
                }
            };
        }
        progressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background,
            (_, _) => TrackProgress(), Dispatcher);
        progressTimer.Stop();
        RefreshRanges();
        PreviewKeyDown += OnWindowKey;
        Loaded +=(_, _) => Dispatcher.BeginInvoke(new Action(Start), DispatcherPriority.ApplicationIdle);
    }

    private static string Plural(long n, string one, string many) => Format.Plural(n, one, many);

    private void Start()
    {
        StartField.Focus();
        try
        {
            AppSettings.LoadRequired();
        }
        catch (ConfigException)
        {
            HeaderBar.SetStatus("Sem conexão: configure o acesso ao banco do SysPDV", StatusKind.Error);
            Message("Clique em Configurações e informe a senha do banco do SysPDV.", "Yellow");
            OpenSettings();
            return;
        }
        CheckConnection();
    }

    private async void CheckConnection()
    {
        try
        {
            var last = await Task.Run(() =>
            {
                using var db = Database.Open();
                return SysPdv.LastNfce(db);
            });
            if (last != null)
            {
                var date = last.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                HeaderBar.SetStatus(
                    $"Conectado ao SysPDV · Última NFC-e no banco: série {last.Series}, nº {last.Number} ({date}).",
                    StatusKind.Ok);
            }
            else
            {
                HeaderBar.SetStatus("Conectado ao banco do SysPDV", StatusKind.Ok);
            }
            if (MessageText.Foreground == Theme.Resource("Red") || MessageText.Text.Contains("Configurações"))
            {
                Message("");
            }
        }
        catch (Exception error)
        {
            Log.Error("Connection failed: " + error.Message);
            HeaderBar.SetStatus("Sem conexão com o banco do SysPDV", StatusKind.Error);
            Message($"Não foi possível ler o banco: {error.Message}", "Red");
        }
    }

    private void OpenSettings() => SettingsWindow.Open(CheckConnection);

    private void Message(string text, string color = "Muted")
    {
        MessageText.Text = text;
        MessageText.Foreground = Theme.Resource(color);
    }

    private void RefreshRanges()
    {
        ranges = Ranges.Merge(ranges);
        RangeItems.ItemsSource = ranges.Select(r => new RangeItem(r)).ToList();
        EmptyState.Visibility = ranges.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = ranges.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var n = Ranges.CountNotes(ranges);
        TotalText.Text = ranges.Count > 0
            ? $"Total: {Plural(n, "NFC-e", "NFC-e")} em {Plural(ranges.Count, "faixa", "faixas")}"
            : "";
        ClearButton.IsEnabled = ranges.Count > 0 && !busy;
    }

    private NoteRange? TypedRange()
    {
        var s = SeriesField.Text.Trim();
        var a = StartField.Text.Trim();
        var b = EndField.Text.Trim();

        NoteRange? Invalid(string text, TextBox field)
        {
            Message(text, "Yellow");
            field.Focus();
            return null;
        }

        if (!IsDigits(s) || int.Parse(s) > 999)
        {
            return Invalid("Série inválida: use só números (0 a 999).", SeriesField);
        }
        if (!IsDigits(a))
        {
            return Invalid("Digite o número inicial da NFC-e (só números).", StartField);
        }
        if (b.Length > 0 && !IsDigits(b))
        {
            return Invalid("Número final inválido: use só números.", EndField);
        }
        var start = long.Parse(a);
        var end = b.Length > 0 ? long.Parse(b) : start;
        if (end < start)
        {
            return Invalid("O número final é menor que o inicial.", EndField);
        }
        if (end - start + 1 > MaxNotes)
        {
            return Invalid($"Faixa grande demais (máximo de {MaxNotes.ToString("N0", new CultureInfo("pt-BR"))} notas por vez).", EndField);
        }
        return new NoteRange(int.Parse(s), start, end);
    }

    private static bool IsDigits(string text) => text.Length > 0 && text.Length <= 9 && text.All(c => c >= '0' && c <= '9');

    private void AddRangeClick(object sender, RoutedEventArgs e) => AddRange();

    private void AddRange()
    {
        if (busy)
        {
            return;
        }
        var range = TypedRange();
        if (range == null)
        {
            return;
        }
        ranges.Add(range.Value);
        RefreshRanges();
        StartField.Text = "";
        EndField.Text = "";
        StartField.Focus();
        Message($"Adicionado: {range.Value.Label()}.", "Green");
    }

    private void RemoveRangeClick(object sender, RoutedEventArgs e)
    {
        if (busy || ((FrameworkElement)sender).Tag is not RangeItem item)
        {
            return;
        }
        ranges = ranges.Where(r => r != item.Range).ToList();
        RefreshRanges();
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        if (busy || ranges.Count == 0)
        {
            return;
        }
        ranges = new List<NoteRange>();
        RefreshRanges();
        Message("");
    }

    private async void ImportClick(object sender, RoutedEventArgs e)
    {
        if (busy)
        {
            return;
        }
        var dialog = new OpenFileDialog
        {
            Title = "Arquivo com a relação das NFC-e",
            Filter = "Documentos (PDF, Word, texto)|*.pdf;*.doc;*.docx;*.rtf;*.txt;*.csv;*.htm;*.html|PDF|*.pdf|Word|*.doc;*.docx;*.rtf|Texto|*.txt;*.csv|Todos os arquivos|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        var path = dialog.FileName;
        var name = Path.GetFileName(path);
        var series = IsDigits(SeriesField.Text.Trim()) ? int.Parse(SeriesField.Text.Trim()) : 1;
        Message($"Lendo {name}…");
        Cursor = Cursors.Wait;
        Reading reading;
        try
        {
            reading = await Task.Run(() => ListReader.ReadFile(path, series));
        }
        catch (Exception error)
        {
            Log.Error($"Failed to read {path}", error);
            Message($"Não foi possível ler o arquivo: {error.Message}", "Red");
            return;
        }
        finally
        {
            Cursor = null;
        }
        if (reading.Ranges.Count == 0)
        {
            Message("Nenhum número de NFC-e encontrado no arquivo.", "Yellow");
            Dialogs.NothingFound(this, name, reading);
            return;
        }
        if (Dialogs.ConfirmImport(this, name, reading))
        {
            ranges.AddRange(reading.Ranges);
            RefreshRanges();
            Message($"{Plural(reading.Total, "NFC-e importada", "NFC-e importadas")} de {name}.", "Green");
        }
        else
        {
            Message("");
        }
    }

    private void ChooseFolderClick(object sender, RoutedEventArgs e) => ChooseFolder();

    private void ChooseFolder()
    {
        var current = FolderField.Text.Trim();
        var folder = FolderPicker.Pick(this, "Pasta onde salvar os XMLs", current);
        if (!string.IsNullOrEmpty(folder))
        {
            FolderField.Text = Path.GetFullPath(folder);
            SavePreferences();
        }
    }

    private void OpenFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = FolderField.Text.Trim();
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            Message("Escolha primeiro a pasta onde salvar os XMLs.", "Yellow");
            return;
        }
        Process.Start("explorer.exe", $"\"{folder}\"");
    }

    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void AboutClick(object sender, RoutedEventArgs e)
    {
        if (AboutView.Visibility == Visibility.Visible)
        {
            CloseAbout();
        }
        else
        {
            OpenAbout();
        }
    }

    private void AboutBack(object sender, EventArgs e) => CloseAbout();

    private void OpenAbout()
    {
        if (sliding || AboutView.Visibility == Visibility.Visible)
        {
            return;
        }
        Slide(MainContent, AboutView, 1, () =>
        {
            MainContent.Visibility = Visibility.Hidden;
            AboutView.FocusBack();
        });
    }

    private void CloseAbout()
    {
        if (sliding || AboutView.Visibility != Visibility.Visible)
        {
            return;
        }
        MainContent.Visibility = Visibility.Visible;
        Slide(AboutView, MainContent, -1, () =>
        {
            AboutView.Visibility = Visibility.Collapsed;
            StartField.Focus();
        });
    }

    private void Slide(FrameworkElement leaving, FrameworkElement entering, int direction, Action finished)
    {
        sliding = true;
        var width = Stage.ActualWidth;
        entering.Visibility = Visibility.Visible;
        var leavingMove = new TranslateTransform();
        var enteringMove = new TranslateTransform(direction * width, 0);
        leaving.RenderTransform = leavingMove;
        entering.RenderTransform = enteringMove;
        var time = new Duration(SystemParameters.ClientAreaAnimation ? SlideTime : TimeSpan.Zero);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var exit = new DoubleAnimation(0, -direction * width, time) { EasingFunction = ease };
        var enter = new DoubleAnimation(direction * width, 0, time) { EasingFunction = ease };
        enter.Completed += (_, _) =>
        {
            leaving.RenderTransform = null;
            entering.RenderTransform = null;
            sliding = false;
            finished();
        };
        leavingMove.BeginAnimation(TranslateTransform.XProperty, exit);
        enteringMove.BeginAnimation(TranslateTransform.XProperty, enter);
    }

    private void OnWindowKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && AboutView.Visibility == Visibility.Visible)
        {
            CloseAbout();
            e.Handled = true;
        }
    }

    private ExportOptions Options() => new()
    {
        IncludeCancellation = IncludeCancellation.IsChecked == true,
        IncludeUnauthorized = IncludeUnauthorized.IsChecked == true,
        ByMonth = ByMonth.IsChecked == true,
    };

    private void SavePreferences()
    {
        try
        {
            var settings = AppSettings.Load();
            settings.XmlFolder = FolderField.Text.Trim();
            settings.ExportOptions = Options();
            if (IsDigits(SeriesField.Text.Trim()))
            {
                settings.Series = SeriesField.Text.Trim();
            }
            settings.Save();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Log.Warning("Preferences not saved: " + error.Message);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Cursor = value ? Cursors.Wait : null;
        ExportButton.IsEnabled = !value;
        ClearButton.IsEnabled = ranges.Count > 0 && !value;
        StopButton.IsEnabled = value;
        StopButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ExportClick(object sender, RoutedEventArgs e)
    {
        if (busy)
        {
            return;
        }
        if (StartField.Text.Trim().Length > 0)
        {
            var range = TypedRange();
            if (range == null)
            {
                return;
            }
            ranges.Add(range.Value);
            StartField.Text = "";
            EndField.Text = "";
            RefreshRanges();
        }
        if (ranges.Count == 0)
        {
            Message("Informe a série e o número das NFC-e (ou importe um documento).", "Yellow");
            StartField.Focus();
            return;
        }
        if (FolderField.Text.Trim().Length == 0)
        {
            ChooseFolder();
            if (FolderField.Text.Trim().Length == 0)
            {
                Message("Escolha a pasta onde salvar os XMLs.", "Yellow");
                return;
            }
        }
        var output = FolderField.Text.Trim();
        try
        {
            AppSettings.LoadRequired();
        }
        catch (ConfigException error)
        {
            Message(error.Message, "Red");
            OpenSettings();
            return;
        }
        try
        {
            Directory.CreateDirectory(output);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            Message($"Não foi possível usar a pasta: {error.Message}", "Red");
            return;
        }

        SavePreferences();
        var toExport = ranges.ToList();
        var options = Options();
        var total = Ranges.CountNotes(toExport);
        Log.Info($"Exporting {total} note(s) to {output}");
        stop = new CancellationTokenSource();
        var token = stop.Token;
        lock (progressGate)
        {
            progress = (0, total, "Conectando ao banco…");
        }
        SetBusy(true);
        Progress.Value = 0;
        progressTimer.Start();

        try
        {
            var (summary, report) = await Task.Run(() =>
            {
                using var db = Database.Open();
                var result = Exporter.Export(db, toExport, output, options, (done, count, text) =>
                {
                    lock (progressGate)
                    {
                        progress = (done, count, text);
                    }
                }, token);
                return (result, ExportReport.Write(result));
            });
            progressTimer.Stop();
            SetBusy(false);
            Finished(summary, report);
        }
        catch (Exception error)
        {
            progressTimer.Stop();
            SetBusy(false);
            Log.Error("Export failed", error);
            Progress.Value = 0;
            Message($"Não foi possível exportar: {error.Message}", "Red");
        }
    }

    private void TrackProgress()
    {
        (int Done, long Total, string Text) current;
        lock (progressGate)
        {
            current = progress;
        }
        Progress.Value = current.Total > 0 ? (double)current.Done / current.Total : 0;
        Message(current.Done > 0 ? $"{current.Done} de {current.Total} · {current.Text}" : current.Text);
    }

    private void StopClick(object sender, RoutedEventArgs e)
    {
        if (busy)
        {
            stop?.Cancel();
            StopButton.IsEnabled = false;
            Message("Parando…", "Yellow");
        }
    }

    private void Finished(ExportSummary summary, string report)
    {
        if (!summary.Interrupted)
        {
            Progress.Value = 1;
        }
        var parts = NoteStatuses.All.Where(s => summary.Count(s) > 0)
            .Select(s => $"{summary.Count(s)} {StatusLabels.Label(s, summary.Count(s))}").ToList();
        var files = Plural(summary.FilesWritten, "arquivo XML gravado", "arquivos XML gravados");
        var prefix = summary.Interrupted ? "Exportação interrompida. " : "";
        var text = $"{prefix}{files}. " + string.Join(" · ", parts);
        var problems = summary.Problems().Count;
        Log.Info($"Export finished: {summary.FilesWritten} file(s), {problems} problem(s), interrupted={summary.Interrupted}");
        Message(text, problems > 0 || summary.Interrupted ? "Yellow" : "Green");

        if (Dialogs.ExportFinished(this, summary, report, char.ToUpper(files[0]) + files.Substring(1) + "."))
        {
            Process.Start("explorer.exe", $"\"{summary.Output}\"");
        }
    }
}
