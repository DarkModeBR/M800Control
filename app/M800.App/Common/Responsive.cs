using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace M800.App.Common;

/// <summary>Comportamentos que adaptam o layout a janelas pequenas.</summary>
public static class Responsive
{
    // ---------- ScrollViewer de página: abaixo do mínimo, o conteúdo para de encolher e aparece a rolagem ----------

    /// <summary>Largura mínima do conteúdo; abaixo dela aparece a barra de rolagem horizontal.</summary>
    public static readonly DependencyProperty MinContentWidthProperty = DependencyProperty.RegisterAttached(
        "MinContentWidth", typeof(double), typeof(Responsive), new PropertyMetadata(0.0, OnScrollSettingChanged));

    /// <summary>
    /// Altura mínima do conteúdo para páginas que ocupam a altura toda (ex.: Macros).
    /// Com valor, o conteúdo preenche a altura visível e só rola abaixo desse mínimo.
    /// </summary>
    public static readonly DependencyProperty MinContentHeightProperty = DependencyProperty.RegisterAttached(
        "MinContentHeight", typeof(double), typeof(Responsive), new PropertyMetadata(0.0, OnScrollSettingChanged));

    public static double GetMinContentWidth(DependencyObject d) => (double)d.GetValue(MinContentWidthProperty);
    public static void SetMinContentWidth(DependencyObject d, double value) => d.SetValue(MinContentWidthProperty, value);
    public static double GetMinContentHeight(DependencyObject d) => (double)d.GetValue(MinContentHeightProperty);
    public static void SetMinContentHeight(DependencyObject d, double value) => d.SetValue(MinContentHeightProperty, value);

    private static void OnScrollSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer)
            return;
        viewer.SizeChanged -= OnScrollViewerSizeChanged;
        viewer.SizeChanged += OnScrollViewerSizeChanged;
        UpdateScroll(viewer);
    }

    private static void OnScrollViewerSizeChanged(object sender, SizeChangedEventArgs e) => UpdateScroll((ScrollViewer)sender);

    private static void UpdateScroll(ScrollViewer viewer)
    {
        if (viewer.Content is not FrameworkElement content || viewer.ActualWidth <= 0)
            return;
        double minWidth = GetMinContentWidth(viewer) + content.Margin.Left + content.Margin.Right;
        bool scrollX = viewer.ActualWidth < minWidth;
        viewer.HorizontalScrollBarVisibility = scrollX ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        content.Width = scrollX ? GetMinContentWidth(viewer) : double.NaN;

        double minContentHeight = GetMinContentHeight(viewer);
        if (minContentHeight <= 0)
            return;
        double minHeight = minContentHeight + content.Margin.Top + content.Margin.Bottom;
        bool scrollY = viewer.ActualHeight < minHeight;
        viewer.VerticalScrollBarVisibility = scrollY ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        content.Height = scrollY ? minContentHeight : double.NaN;
    }

    // ---------- Cartões de configuração: o controle desce para baixo do texto quando falta espaço ----------

    private const double MinHeaderWidth = 240, Hysteresis = 24;

    private static readonly DependencyProperty NaturalContentWidthProperty = DependencyProperty.RegisterAttached(
        "NaturalContentWidth", typeof(double), typeof(Responsive), new PropertyMetadata(0.0));

    /// <summary>
    /// Liga o comportamento em todos os cartões do app (chamado uma vez ao iniciar). Usa o SizeChanged por classe
    /// porque o WPF só entrega Loaded a elementos cuja subárvore tem manipuladores de Loaded.
    /// </summary>
    public static void EnableAdaptiveCards() =>
        EventManager.RegisterClassHandler(typeof(CardControl), FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler(OnCardSizeChanged));

    private static void OnCardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var card = (CardControl)sender;
        if (card.Template?.FindName("ContentPresenter", card) is not ContentPresenter content
            || card.Template.FindName("ControlIcon", card) is not FrameworkElement icon
            || VisualTreeHelper.GetParent(content) is not Grid grid
            || card.Content is null)
            return;

        bool narrow = GetIsNarrow(card);
        if (!narrow)
            card.SetValue(NaturalContentWidthProperty, content.ActualWidth);
        double contentWidth = (double)card.GetValue(NaturalContentWidthProperty);
        double iconWidth = icon.Visibility == Visibility.Visible ? icon.ActualWidth + icon.Margin.Right : 0;
        double inner = card.ActualWidth - card.Padding.Left - card.Padding.Right - card.BorderThickness.Left - card.BorderThickness.Right;
        double headerSpace = inner - iconWidth - contentWidth - 14;
        bool shouldBeNarrow = narrow ? headerSpace < MinHeaderWidth + Hysteresis : headerSpace < MinHeaderWidth;
        if (shouldBeNarrow == narrow)
            return;

        SetIsNarrow(card, shouldBeNarrow);
        if (grid.RowDefinitions.Count < 2)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        Grid.SetRow(content, shouldBeNarrow ? 1 : 0);
        Grid.SetColumn(content, shouldBeNarrow ? 1 : 2);
        Grid.SetColumnSpan(content, shouldBeNarrow ? 2 : 1);
        content.Margin = shouldBeNarrow ? new Thickness(0, 12, 0, 0) : new Thickness(14, 0, 0, 0);
        content.HorizontalAlignment = shouldBeNarrow ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
    }

    // ---------- Indicador genérico: IsNarrow fica verdadeiro abaixo da largura dada (para gatilhos no XAML) ----------

    public static readonly DependencyProperty NarrowBelowProperty = DependencyProperty.RegisterAttached(
        "NarrowBelow", typeof(double), typeof(Responsive), new PropertyMetadata(0.0, OnNarrowBelowChanged));

    public static readonly DependencyProperty IsNarrowProperty = DependencyProperty.RegisterAttached(
        "IsNarrow", typeof(bool), typeof(Responsive), new PropertyMetadata(false));

    public static double GetNarrowBelow(DependencyObject d) => (double)d.GetValue(NarrowBelowProperty);
    public static void SetNarrowBelow(DependencyObject d, double value) => d.SetValue(NarrowBelowProperty, value);
    public static bool GetIsNarrow(DependencyObject d) => (bool)d.GetValue(IsNarrowProperty);
    public static void SetIsNarrow(DependencyObject d, bool value) => d.SetValue(IsNarrowProperty, value);

    private static void OnNarrowBelowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;
        element.SizeChanged -= OnNarrowElementSizeChanged;
        element.SizeChanged += OnNarrowElementSizeChanged;
    }

    private static void OnNarrowElementSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (e.WidthChanged)
            SetIsNarrow(element, element.ActualWidth < GetNarrowBelow(element));
    }

    // ---------- Grade de duas colunas que empilha (coluna da direita vai para baixo) ----------

    /// <summary>Abaixo desta largura, a grade de duas colunas vira duas linhas.</summary>
    public static readonly DependencyProperty StackBelowProperty = DependencyProperty.RegisterAttached(
        "StackBelow", typeof(double), typeof(Responsive), new PropertyMetadata(0.0, OnStackBelowChanged));

    /// <summary>Alturas das duas linhas quando empilhada, ex.: "Auto,Auto" (padrão) ou "220,*".</summary>
    public static readonly DependencyProperty StackedRowsProperty = DependencyProperty.RegisterAttached(
        "StackedRows", typeof(string), typeof(Responsive), new PropertyMetadata("Auto,Auto"));

    public static double GetStackBelow(DependencyObject d) => (double)d.GetValue(StackBelowProperty);
    public static void SetStackBelow(DependencyObject d, double value) => d.SetValue(StackBelowProperty, value);
    public static string GetStackedRows(DependencyObject d) => (string)d.GetValue(StackedRowsProperty);
    public static void SetStackedRows(DependencyObject d, string value) => d.SetValue(StackedRowsProperty, value);

    private sealed record WideLayout(GridLength[] Columns, Dictionary<UIElement, Thickness> Margins);

    private static readonly DependencyProperty WideLayoutProperty = DependencyProperty.RegisterAttached(
        "WideLayout", typeof(WideLayout), typeof(Responsive), new PropertyMetadata(null));

    private static void OnStackBelowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid grid)
            return;
        grid.SizeChanged -= OnStackGridSizeChanged;
        grid.SizeChanged += OnStackGridSizeChanged;
    }

    private static void OnStackGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var grid = (Grid)sender;
        if (!e.WidthChanged || grid.ColumnDefinitions.Count != 2)
            return;
        bool stacked = GetIsNarrow(grid);
        bool shouldStack = grid.ActualWidth < GetStackBelow(grid);
        if (shouldStack == stacked)
            return;
        SetIsNarrow(grid, shouldStack);

        if (grid.GetValue(WideLayoutProperty) is not WideLayout wide)
        {
            wide = new WideLayout(
                grid.ColumnDefinitions.Select(c => c.Width).ToArray(),
                grid.Children.OfType<FrameworkElement>().ToDictionary(c => (UIElement)c, c => c.Margin));
            grid.SetValue(WideLayoutProperty, wide);
        }

        if (grid.RowDefinitions.Count == 0)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
        }
        var rows = GetStackedRows(grid).Split(',').Select(ParseLength).ToArray();
        for (int i = 0; i < 2; i++)
            grid.RowDefinitions[i].Height = shouldStack ? rows[i] : new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[0].Width = shouldStack ? new GridLength(1, GridUnitType.Star) : wide.Columns[0];
        grid.ColumnDefinitions[1].Width = shouldStack ? new GridLength(0) : wide.Columns[1];

        foreach (var (child, margin) in wide.Margins)
        {
            var element = (FrameworkElement)child;
            if (shouldStack)
            {
                if (Grid.GetColumn(child) == 1)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, 1);
                }
                // o espaço entre as colunas vira espaço entre as linhas
                element.Margin = new Thickness(margin.Left, margin.Top, 0, margin.Bottom + margin.Right);
            }
            else
            {
                if (Grid.GetRow(child) == 1)
                {
                    Grid.SetRow(child, 0);
                    Grid.SetColumn(child, 1);
                }
                element.Margin = margin;
            }
        }
    }

    private static GridLength ParseLength(string text)
    {
        text = text.Trim();
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return GridLength.Auto;
        if (text.EndsWith('*'))
            return new GridLength(text.Length == 1 ? 1 : double.Parse(text[..^1], System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Star);
        return new GridLength(double.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
    }
}
