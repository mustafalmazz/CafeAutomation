using System.Windows;
using System.Windows.Controls;

namespace PremiumKafeOtomasyon.Controls;

public sealed class AdaptiveTilePanel : Panel
{
    private const double Gap = 14;
    private const double MinimumWidth = 176;
    private int _columns = 1;
    private double _tileWidth;
    private double _rowHeight;
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 880 : availableSize.Width;
        _columns = Math.Max(1, (int)((width + Gap) / (MinimumWidth + Gap)));
        _tileWidth = Math.Max(0, (width - (_columns - 1) * Gap) / _columns);
        _rowHeight = 0;
        foreach (UIElement child in InternalChildren) { child.Measure(new Size(_tileWidth, double.PositiveInfinity)); _rowHeight = Math.Max(_rowHeight, child.DesiredSize.Height); }
        return new Size(width, Math.Ceiling((double)InternalChildren.Count / _columns) * _rowHeight);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count; i++) InternalChildren[i].Arrange(new Rect(i % _columns * (_tileWidth + Gap), i / _columns * _rowHeight, _tileWidth, _rowHeight));
        return finalSize;
    }
}
