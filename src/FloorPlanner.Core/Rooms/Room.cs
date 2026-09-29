using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Rooms;

/// <summary>An area fully enclosed by walls. <see cref="Area"/> is in mm².</summary>
public sealed record Room(IReadOnlyList<Vec2> Outline, double Area, Vec2 Centroid)
{
    public double AreaSquareMetres => Area / 1_000_000;
}
