using Avalonia;
using Avalonia.Controls;

namespace Churchie.App.Controls;

/// <summary>
/// Holds one child at a fixed shape (16:9 by default, like a projector).
/// It grows with the width of its panel, but never taller than the space it
/// is given (or its MaxHeight); when height is the limit, it narrows and centres.
/// Used for the big Live preview so it fills the panel without black bars.
/// </summary>
public sealed class AspectRatioBox : Decorator
{
    /// <summary>Width divided by height. 16:9 = 1.777…</summary>
    public double Ratio { get; set; } = 16.0 / 9.0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Fit(availableSize);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = Fit(finalSize);
        var left = (finalSize.Width - size.Width) / 2;
        Child?.Arrange(new Rect(new Point(left, 0), size));
        return finalSize;
    }

    private Size Fit(Size space)
    {
        // Inside a ScrollViewer the width can be "infinite": fall back to a sensible size.
        var width = double.IsInfinity(space.Width) ? 640 : space.Width;
        var height = width / Ratio;

        if (!double.IsInfinity(space.Height) && height > space.Height)
        {
            height = space.Height;
            width = height * Ratio;
        }

        return new Size(Math.Max(0, width), Math.Max(0, height));
    }
}
