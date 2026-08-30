using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SCPU.Simulator.Desktop.Controls;

/// <summary>Renders a 16-bit value as four hexadecimal seven-segment digits.</summary>
public sealed class SevenSegmentDisplay : Control
{
    public static readonly StyledProperty<ushort> ValueProperty =
        AvaloniaProperty.Register<SevenSegmentDisplay, ushort>(nameof(Value));

    private static readonly byte[] SegmentMasks =
    [
        0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07,
        0x7F, 0x6F, 0x77, 0x7C, 0x39, 0x5E, 0x79, 0x71
    ];

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#F3C969"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#263448"));

    static SevenSegmentDisplay() => AffectsRender<SevenSegmentDisplay>(ValueProperty);

    public ushort Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var digitWidth = Math.Min(42d, Bounds.Width / 4d - 6d);
        var digitHeight = Math.Min(68d, Bounds.Height);
        var totalWidth = digitWidth * 4 + 18;
        var originX = Math.Max(0, (Bounds.Width - totalWidth) / 2);
        var originY = Math.Max(0, (Bounds.Height - digitHeight) / 2);

        for (var index = 0; index < 4; index++)
        {
            var shift = (3 - index) * 4;
            DrawDigit(context, originX + index * (digitWidth + 6), originY,
                digitWidth, digitHeight, SegmentMasks[(Value >> shift) & 0xF]);
        }
    }

    private static void DrawDigit(DrawingContext context, double x, double y, double width, double height, byte mask)
    {
        var thickness = Math.Max(3, width * 0.13);
        var horizontalWidth = width - thickness * 2;
        var halfHeight = (height - thickness * 3) / 2;
        var segments = new[]
        {
            new Rect(x + thickness, y, horizontalWidth, thickness),
            new Rect(x + width - thickness, y + thickness, thickness, halfHeight),
            new Rect(x + width - thickness, y + thickness * 2 + halfHeight, thickness, halfHeight),
            new Rect(x + thickness, y + height - thickness, horizontalWidth, thickness),
            new Rect(x, y + thickness * 2 + halfHeight, thickness, halfHeight),
            new Rect(x, y + thickness, thickness, halfHeight),
            new Rect(x + thickness, y + thickness + halfHeight, horizontalWidth, thickness)
        };

        for (var segment = 0; segment < segments.Length; segment++)
            context.DrawRectangle((mask & (1 << segment)) != 0 ? ActiveBrush : InactiveBrush,
                null, segments[segment], thickness / 2, thickness / 2);
    }
}
