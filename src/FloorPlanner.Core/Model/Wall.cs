using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Model;

/// <summary>A straight wall segment. Coordinates and thickness are in millimetres.</summary>
public sealed record Wall(Guid Id, Vec2 Start, Vec2 End, double Thickness = Wall.DefaultThickness)
{
    public const double DefaultThickness = 100;

    public static Wall Create(Vec2 start, Vec2 end, double thickness = DefaultThickness) =>
        new(Guid.NewGuid(), start, end, thickness);

    public double Length => Start.DistanceTo(End);
    public Vec2 Midpoint => (Start + End) / 2;
}
