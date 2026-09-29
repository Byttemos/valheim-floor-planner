using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Editing;

/// <summary>
/// Maps between screen pixels and world millimetres: screen = world * Scale + Offset.
/// </summary>
public sealed record Viewport(Vec2 Offset, double Scale)
{
    public const double MinScale = 0.01;
    public const double MaxScale = 2;

    /// <summary>Default: 0.04 px per mm, i.e. 1 metre = 40 px.</summary>
    public static Viewport Default { get; } = new(new Vec2(40, 40), 0.04);

    public Vec2 ScreenToWorld(Vec2 screen) => (screen - Offset) / Scale;
    public Vec2 WorldToScreen(Vec2 world) => world * Scale + Offset;

    public Viewport PanBy(Vec2 screenDelta) => this with { Offset = Offset + screenDelta };

    /// <summary>Zooms by <paramref name="factor"/> while keeping the world point under <paramref name="screenAnchor"/> fixed.</summary>
    public Viewport ZoomAt(Vec2 screenAnchor, double factor)
    {
        var newScale = Math.Clamp(Scale * factor, MinScale, MaxScale);
        var worldAnchor = ScreenToWorld(screenAnchor);
        return new Viewport(screenAnchor - worldAnchor * newScale, newScale);
    }
}
