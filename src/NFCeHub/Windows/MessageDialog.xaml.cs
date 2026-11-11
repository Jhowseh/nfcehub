using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NFCeHub;

public enum DialogKind
{
    Question,
    Info,
    Success,
    Warning,
    Error,
}

public sealed class DialogButton
{
    public DialogButton(string text, bool result, bool primary = false, string icon = null)
    {
        Text = text;
        Result = result;
        Primary = primary;
        Icon = icon;
    }

    public string Text { get; }

    public bool Result { get; }

    public bool Primary { get; }

    public string Icon { get; }
}

public partial class MessageDialog : Window
{
    private static readonly Dictionary<DialogKind, (string Icon, string Color)> Badges = new()
    {
        [DialogKind.Question] = ("Question", "Link"),
        [DialogKind.Info] = ("Info", "Link"),
        [DialogKind.Success] = ("Check", "Green"),
        [DialogKind.Warning] = ("Alert", "Yellow"),
        [DialogKind.Error] = ("Close", "Red"),
    };

    private bool result;

    private MessageDialog(Window owner, DialogKind kind, string title, string message, UIElement body,
        IReadOnlyList<DialogButton> buttons)
    {
        InitializeComponent();
        Theme.Prepare(this);
        if (owner != null && owner.IsVisible)
        {
            Owner = owner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        var (icon, color) = Badges[kind];
        var brush = (SolidColorBrush)Theme.Resource(color);
        BadgeIcon.Data = Ui.Geometry(icon);
        BadgeIcon.Foreground = brush;
        var tint = new SolidColorBrush(Color.FromArgb(0x2E, brush.Color.R, brush.Color.G, brush.Color.B));
        tint.Freeze();
        BadgeBack.Fill = tint;

        TitleText.Text = title;
        MessageText.Text = message ?? "";
        MessageText.ToolTip = string.IsNullOrEmpty(message) ? null : message;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        BodyHost.Content = body;
        BodyHost.Visibility = body == null ? Visibility.Collapsed : Visibility.Visible;

        for (var i = 0; i < buttons.Count; i++)
        {
            var spec = buttons[i];
            var button = new Button
            {
                Content = spec.Text,
                Style = (Style)FindResource(spec.Primary ? "PrimaryButton" : "PillButton"),
                Margin = new Thickness(i == 0 ? 0 : 10, 0, 0, 0),
                IsDefault = spec.Primary,
            };
            if (spec.Icon != null)
            {
                Ui.SetIcon(button, Ui.Geometry(spec.Icon));
            }
            button.Click += (_, _) =>
            {
                result = spec.Result;
                Close();
            };
            ButtonRow.Children.Add(button);
        }
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                result = false;
                Close();
            }
        };

        Loaded += (_, _) =>
        {
            ButtonRow.Children.OfType<Button>().LastOrDefault()?.Focus();
            if (kind == DialogKind.Warning)
            {
                SystemSounds.Exclamation.Play();
            }
            else if (kind == DialogKind.Error)
            {
                SystemSounds.Hand.Play();
            }
        };
    }

    public static bool Show(Window owner, DialogKind kind, string title, string message, UIElement body,
        params DialogButton[] buttons)
    {
        if (buttons.Length == 0)
        {
            buttons = new[] { new DialogButton("OK", true, primary: true) };
        }
        var dialog = new MessageDialog(owner, kind, title, message, body, buttons);
        dialog.ShowDialog();
        return dialog.result;
    }
}
