using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using M800.Core.Protocol;

namespace M800.App.Controls;

/// <summary>
/// Indicador flutuante de DPI (embaixo, no centro, como o de volume do Windows 11).
/// Não rouba o foco e deixa os cliques passarem através dele.
/// </summary>
public sealed class DpiIndicatorWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private readonly Ellipse _dot = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _value = new() { FontSize = 20, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _detail = new() { FontSize = 12 };
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };

    public DpiIndicatorWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Focusable = false;
        IsHitTestVisible = false;

        _value.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _detail.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var texts = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_value);
        texts.Children.Add(_detail);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_dot);
        row.Children.Add(texts);

        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 12, 28, 12),
            Margin = new Thickness(18), // espaço para a sombra
            BorderThickness = new Thickness(1),
            Child = row,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35, Direction = 270 },
        };
        card.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");
        Content = card;

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            FadeOut();
        };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
    }

    public void ShowLevel(int dpi, Rgb color, int position, int count)
    {
        _value.Text = $"{dpi:N0} DPI";
        _detail.Text = $"Nível {position} de {count}";
        _dot.Fill = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Bottom - ActualHeight - 12;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void FadeOut()
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) =>
        {
            if (!_hideTimer.IsEnabled)
                Hide();
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
