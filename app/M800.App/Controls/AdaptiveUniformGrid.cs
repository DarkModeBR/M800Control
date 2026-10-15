using System.Windows;
using System.Windows.Controls.Primitives;

namespace M800.App.Controls;

/// <summary>
/// UniformGrid que escolhe o número de colunas pela largura disponível (cada item com pelo menos
/// <see cref="ItemMinWidth"/>), preferindo divisões exatas: 6 itens viram 6, 3, 2 ou 1 coluna.
/// </summary>
public sealed class AdaptiveUniformGrid : UniformGrid
{
    public static readonly DependencyProperty ItemMinWidthProperty = DependencyProperty.Register(
        nameof(ItemMinWidth), typeof(double), typeof(AdaptiveUniformGrid),
        new FrameworkPropertyMetadata(120.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemMinWidth
    {
        get => (double)GetValue(ItemMinWidthProperty);
        set => SetValue(ItemMinWidthProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        int count = InternalChildren.Cast<UIElement>().Count(c => c.Visibility != Visibility.Collapsed);
        if (count > 0 && !double.IsInfinity(constraint.Width))
        {
            int fit = Math.Clamp((int)(constraint.Width / ItemMinWidth), 1, count);
            while (count % fit != 0)
                fit--;
            Rows = 0;
            Columns = fit;
        }
        return base.MeasureOverride(constraint);
    }
}
