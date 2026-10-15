using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using M800.App.Common;
using M800.Core.Protocol;

namespace M800.App.Controls;

/// <summary>Diálogo de cor: amostras prontas, controles R/G/B e campo #RRGGBB.</summary>
public static class ColorPickerDialog
{
    private static readonly string[] Presets =
    [
        "#FF0000", "#FF6A00", "#FFD800", "#B6FF00", "#00FF00", "#00FF90",
        "#00FFFF", "#0094FF", "#0000FF", "#4800FF", "#B200FF", "#FF00DC",
        "#FF006E", "#FFFFFF", "#60CDFF", "#FF8080", "#80FF80", "#8080FF",
    ];

    public static async Task<Rgb?> ShowAsync(Rgb initial)
    {
        var current = initial;
        bool updating = false;
        var preview = new Border { Height = 56, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 14) };
        var hex = new Wpf.Ui.Controls.TextBox { Width = 130, HorizontalAlignment = HorizontalAlignment.Left };
        var sliders = new[] { MakeSlider(), MakeSlider(), MakeSlider() };

        void Apply(Rgb color, bool fromHex = false)
        {
            updating = true;
            current = color;
            preview.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            sliders[0].Value = color.R;
            sliders[1].Value = color.G;
            sliders[2].Value = color.B;
            if (!fromHex)
                hex.Text = color.ToHex();
            updating = false;
        }

        foreach (var slider in sliders)
        {
            slider.ValueChanged += (_, _) =>
            {
                if (!updating)
                    Apply(new Rgb((byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value));
            };
        }
        hex.TextChanged += (_, _) =>
        {
            if (!updating && Rgb.TryParse(hex.Text, out var parsed))
                Apply(parsed, fromHex: true);
        };

        var swatches = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        foreach (var text in Presets)
        {
            var color = Rgb.Parse(text);
            var swatch = new Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x80, 0x80, 0x80)),
                Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                Cursor = Cursors.Hand,
                ToolTip = text,
            };
            swatch.MouseLeftButtonUp += (_, _) => Apply(color);
            swatches.Children.Add(swatch);
        }

        var panel = new StackPanel { Width = 380 };
        panel.Children.Add(preview);
        panel.Children.Add(swatches);
        panel.Children.Add(Row("Vermelho", sliders[0]));
        panel.Children.Add(Row("Verde", sliders[1]));
        panel.Children.Add(Row("Azul", sliders[2]));
        panel.Children.Add(Row("Código", hex));
        Apply(initial);

        return await Dialogs.ShowContentAsync("Escolher cor", panel, "OK") ? current : null;
    }

    private static Slider MakeSlider() => new()
    {
        Minimum = 0,
        Maximum = 255,
        IsSnapToTickEnabled = true,
        TickFrequency = 1,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static UIElement Row(string label, UIElement control)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var text = new TextBlock { Text = label, Width = 80, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(text, Dock.Left);
        dock.Children.Add(text);
        dock.Children.Add(control);
        return dock;
    }
}
