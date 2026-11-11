using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NFCeHub;

public enum StatusKind
{
    Ok,
    Error,
    Warning,
    Neutral,
}

public partial class Header : UserControl
{
    private static readonly Duration PulsePeriod = new(TimeSpan.FromSeconds(2));

    public Header()
    {
        InitializeComponent();
        Loaded += (_, _) => StartPulse();
    }

    public void Setup(double width, string eyebrow, string title, string subtitle, bool withStatus = true)
    {
        Width = width;
        Root.Width = width;
        Root.Height = withStatus ? 122 : 104;
        LogoImage.Height = withStatus ? 96 : 84;
        LogoImage.Source = Logo.Load(Theme.IsDark);
        AddSpaced(Eyebrow, eyebrow);
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        Status.Visibility = withStatus ? Visibility.Visible : Visibility.Collapsed;
        SetStatus("", StatusKind.Ok);
    }

    public static void AddSpaced(Panel panel, string text)
    {
        var resources = Application.Current.Resources;
        foreach (var ch in text.ToUpperInvariant())
        {
            panel.Children.Add(new TextBlock
            {
                Text = ch.ToString(),
                FontFamily = (FontFamily)resources["MonoFont"],
                FontSize = (double)resources["LabelSize"],
                Foreground = Theme.Resource("Muted"),
                Margin = new Thickness(0, 0, 2.5, 0),
            });
        }
    }

    public void SetStatus(string text, StatusKind kind)
    {
        var color = Theme.Brush(Theme.StatusDot[kind]);
        Dot.Fill = color;
        Ring.Stroke = color;
        StatusHit.ToolTip = string.IsNullOrEmpty(text) ? null : text;
    }

    private void StartPulse()
    {
        if (Status.Visibility != Visibility.Visible)
        {
            return;
        }
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(0.4, 1.0, PulsePeriod) { EasingFunction = ease, RepeatBehavior = RepeatBehavior.Forever };
        var fade = new DoubleAnimation(0.8, 0.0, PulsePeriod) { RepeatBehavior = RepeatBehavior.Forever };
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        Ring.BeginAnimation(OpacityProperty, fade);
    }
}
