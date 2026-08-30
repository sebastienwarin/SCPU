using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SCPU.Simulator.Desktop.Controls;

/// <summary>Renders an LED with identical geometry in both states.</summary>
public sealed class LedIndicator : Control
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<LedIndicator, bool>(nameof(IsOn));

    private static readonly IBrush OnBrush = new SolidColorBrush(Color.Parse("#39D5E8"));
    private static readonly IBrush OffBrush = new SolidColorBrush(Color.Parse("#111A28"));
    private static readonly IPen RingPen = new Pen(OnBrush, 2);

    static LedIndicator() => AffectsRender<LedIndicator>(IsOnProperty);

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var diameter = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) - 2);
        var bounds = new Rect((Bounds.Width - diameter) / 2, (Bounds.Height - diameter) / 2, diameter, diameter);
        context.DrawEllipse(IsOn ? OnBrush : OffBrush, RingPen, bounds);
    }
}
