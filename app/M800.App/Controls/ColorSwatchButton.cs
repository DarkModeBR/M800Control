using System.Windows;
using System.Windows.Media;
using M800.Core.Protocol;

namespace M800.App.Controls;

/// <summary>Botão que mostra uma cor e abre o seletor ao clicar.</summary>
public sealed class ColorSwatchButton : System.Windows.Controls.Button
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Rgb), typeof(ColorSwatchButton),
        new FrameworkPropertyMetadata(Rgb.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((ColorSwatchButton)d).UpdateSwatch()));

    private readonly System.Windows.Controls.Border _swatch = new()
    {
        Width = 30,
        Height = 20,
        CornerRadius = new CornerRadius(4),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x60, 0x80, 0x80, 0x80)),
    };

    public ColorSwatchButton()
    {
        Style = (Style)Application.Current.FindResource(typeof(System.Windows.Controls.Button));
        Padding = new Thickness(6);
        Content = _swatch;
        Click += async (_, _) =>
        {
            if (await ColorPickerDialog.ShowAsync(Color) is Rgb picked)
                Color = picked;
        };
        UpdateSwatch();
    }

    public Rgb Color
    {
        get => (Rgb)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private void UpdateSwatch()
    {
        _swatch.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(Color.R, Color.G, Color.B));
        ToolTip = $"{Color.ToHex()} — clique para mudar";
    }
}
