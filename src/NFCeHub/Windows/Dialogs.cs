using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NFCeHub.Core;

namespace NFCeHub;

public static class Dialogs
{
    private const int MaxChips = 12;

    public static bool ConfirmImport(Window owner, string fileName, Reading reading)
    {
        var body = new StackPanel();
        var series = reading.Series;
        var tiles = new List<(string, string, Brush)>
        {
            (Number(reading.Total), "NFC-e", Theme.Resource("Text")),
            (Number(reading.Ranges.Count), reading.Ranges.Count == 1 ? "faixa" : "faixas", Theme.Resource("Text")),
            (Number(series.Count), series.Count == 1 ? "série" : "séries", Theme.Resource("Text")),
        };
        body.Children.Add(Tiles(tiles));

        body.Children.Add(SectionLabel("Por série"));
        var rows = series.Select(s =>
        {
            var ofSeries = reading.Ranges.Where(r => r.Series == s).ToList();
            var start = ofSeries.First().Start;
            var end = ofSeries.Last().End;
            var span = start == end ? $"nº {start}" : $"nº {start} a {end}";
            return ($"Série {s}", span, $"{Number(Ranges.CountNotes(ofSeries))} NFC-e");
        });
        body.Children.Add(Table(rows));

        body.Children.Add(SectionLabel(reading.Ranges.Count > MaxChips ? "Primeiras faixas" : "Faixas"));
        body.Children.Add(Chips(reading.Ranges, series.Count > 1));

        if (reading.Source == ReadingSource.AccessKeys)
        {
            body.Children.Add(Note("Info", "Números lidos das chaves de acesso, que têm dígito verificador.", "Muted"));
        }
        if (reading.NfeCount > 0)
        {
            var text = reading.NfeCount == 1
                ? "1 linha de NF-e (modelo 55) foi ignorada: o programa exporta só NFC-e."
                : $"{reading.NfeCount} linhas de NF-e (modelo 55) foram ignoradas: o programa exporta só NFC-e.";
            body.Children.Add(Note("Alert", text, "Yellow"));
        }
        foreach (var warning in reading.Warnings)
        {
            body.Children.Add(Note("Alert", warning, "Yellow"));
        }

        return MessageDialog.Show(owner, DialogKind.Question, "Importar estas notas?", fileName, body,
            new DialogButton("Cancelar", false),
            new DialogButton("Pôr na lista", true, primary: true, icon: "Plus"));
    }

    public static void NothingFound(Window owner, string fileName, Reading reading)
    {
        var body = new StackPanel();
        body.Children.Add(Note("Info",
            "O programa procura chaves de acesso, tabelas com as colunas Série e Número (ou Inicial e Final), " +
            "faixas como \"122 a 150\" e números soltos.", "Muted", top: 0));
        foreach (var warning in reading.Warnings)
        {
            body.Children.Add(Note("Alert", warning, "Yellow"));
        }
        MessageDialog.Show(owner, DialogKind.Warning, "Nenhuma NFC-e encontrada", fileName, body,
            new DialogButton("Entendi", true, primary: true));
    }

    public static bool ExportFinished(Window owner, ExportSummary summary, string report, string filesText)
    {
        var problems = summary.Problems().Count;
        var body = new StackPanel();
        var tiles = NoteStatuses.All
            .Where(s => summary.Count(s) > 0)
            .Select(s =>
            {
                var n = summary.Count(s);
                return (Number(n), StatusLabels.Label(s, n), Theme.Resource(StatusLabels.Color(s)));
            })
            .ToList();
        if (tiles.Count > 0)
        {
            body.Children.Add(Tiles(tiles));
        }

        body.Children.Add(SectionLabel("Pasta"));
        body.Children.Add(FolderCard(summary.Output));

        if (report != null)
        {
            var text = problems == 1
                ? $"A nota sem XML autorizado está listada em {Path.GetFileName(report)}, na mesma pasta."
                : $"As {problems} notas sem XML autorizado estão listadas em {Path.GetFileName(report)}, na mesma pasta.";
            body.Children.Add(Note("Document", text, "Muted"));
        }

        var kind = summary.Interrupted || problems > 0 ? DialogKind.Warning : DialogKind.Success;
        var title = summary.Interrupted ? "Exportação interrompida" : "Exportação concluída";
        return MessageDialog.Show(owner, kind, title, filesText, body,
            new DialogButton("Fechar", false),
            new DialogButton("Abrir pasta", true, primary: true, icon: "Folder"));
    }

    public static void Error(Window owner, string title, string message)
    {
        MessageDialog.Show(owner, DialogKind.Error, title, message, null,
            new DialogButton("Fechar", true, primary: true));
    }

    private static string Number(long n) => Format.Number(n);

    private static Style TextStyle(string key) => (Style)Application.Current.Resources[key];

    private static Border Card(UIElement child, Thickness padding) => new()
    {
        CornerRadius = new CornerRadius(10),
        Background = Theme.Resource("Card"),
        BorderBrush = Theme.Resource("Line"),
        BorderThickness = new Thickness(1),
        Padding = padding,
        Child = child,
    };

    private static UIElement Tiles(IReadOnlyList<(string Value, string Label, Brush Color)> tiles)
    {
        var grid = new UniformGrid { Rows = 1, Columns = tiles.Count };
        for (var i = 0; i < tiles.Count; i++)
        {
            var (value, label, color) = tiles[i];
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = value,
                Style = TextStyle("TextBase"),
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = color,
            });
            content.Children.Add(new TextBlock { Text = label, Style = TextStyle("SmallText") });
            var card = Card(content, new Thickness(14, 10, 14, 12));
            card.Margin = new Thickness(0, 0, i == tiles.Count - 1 ? 0 : 8, 0);
            grid.Children.Add(card);
        }
        return grid;
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        Style = TextStyle("SmallText"),
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(2, 16, 0, 6),
    };

    private static UIElement Table(IEnumerable<(string Name, string Span, string Count)> rows)
    {
        var list = new StackPanel();
        var first = true;
        foreach (var (name, span, count) in rows)
        {
            var row = new DockPanel { LastChildFill = true };
            var countText = new TextBlock
            {
                Text = count,
                Style = TextStyle("TextBase"),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(countText, Dock.Right);
            row.Children.Add(countText);
            var nameText = new TextBlock
            {
                Text = name,
                Style = TextStyle("TextBase"),
                FontWeight = FontWeights.SemiBold,
                Width = 70,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(nameText, Dock.Left);
            row.Children.Add(nameText);
            row.Children.Add(new TextBlock
            {
                Text = span,
                Style = TextStyle("SmallText"),
                FontSize = (double)Application.Current.Resources["BodySize"],
                VerticalAlignment = VerticalAlignment.Center,
            });
            list.Children.Add(new Border
            {
                BorderBrush = Theme.Resource("Line"),
                BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
                Padding = new Thickness(14, 9, 14, 9),
                Child = row,
            });
            first = false;
        }
        return Card(list, new Thickness(0));
    }

    private static UIElement Chips(IReadOnlyList<NoteRange> ranges, bool showSeries)
    {
        var panel = new WrapPanel();
        foreach (var r in ranges.Take(MaxChips))
        {
            var numbers = r.Start == r.End ? $"{r.Start}" : $"{r.Start}–{r.End}";
            panel.Children.Add(Chip(showSeries ? $"S{r.Series} · {numbers}" : numbers, "Text"));
        }
        if (ranges.Count > MaxChips)
        {
            var more = ranges.Count - MaxChips;
            panel.Children.Add(Chip(more == 1 ? "+ 1 faixa" : $"+ {Number(more)} faixas", "Muted"));
        }
        return panel;
    }

    private static Border Chip(string text, string color) => new()
    {
        CornerRadius = new CornerRadius(12),
        Background = Theme.Resource("Card"),
        BorderBrush = Theme.Resource("Line"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 3, 10, 4),
        Margin = new Thickness(0, 0, 6, 6),
        Child = new TextBlock { Text = text, Style = TextStyle("SmallText"), Foreground = Theme.Resource(color), TextWrapping = TextWrapping.NoWrap },
    };

    private static UIElement Note(string icon, string text, string color, double top = 12)
    {
        var row = new DockPanel { Margin = new Thickness(2, top, 0, 0) };
        var glyph = new IconView
        {
            Data = Ui.Geometry(icon),
            Size = 16,
            Foreground = Theme.Resource(color == "Muted" ? "Violet" : color),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 10, 0),
        };
        DockPanel.SetDock(glyph, Dock.Left);
        row.Children.Add(glyph);
        row.Children.Add(new TextBlock
        {
            Text = text,
            Style = TextStyle("SmallText"),
            FontSize = (double)Application.Current.Resources["BodySize"],
            Foreground = Theme.Resource(color == "Muted" ? "Muted" : color),
        });
        return row;
    }

    private static UIElement FolderCard(string folder)
    {
        var row = new DockPanel();
        var glyph = new IconView { Data = Ui.Geometry("Folder"), Size = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(glyph, Dock.Left);
        row.Children.Add(glyph);
        row.Children.Add(new TextBlock
        {
            Text = folder,
            Style = TextStyle("TextBase"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = folder,
        });
        return Card(row, new Thickness(14, 10, 14, 10));
    }
}
