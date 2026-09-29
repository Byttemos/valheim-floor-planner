namespace FloorPlanner.Core.Geometry;

public static class GeometryMath
{
    /// <summary>Rounds a point to the nearest multiple of <paramref name="gridSize"/>.</summary>
    public static Vec2 SnapToGrid(Vec2 p, double gridSize) =>
        gridSize <= 0 ? p : new(Round(p.X / gridSize) * gridSize, Round(p.Y / gridSize) * gridSize);

    private static double Round(double v) => Math.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>Shortest distance from point <paramref name="p"/> to the segment a–b.</summary>
    public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.Dot(ab);
        if (lengthSquared == 0) return p.DistanceTo(a);

        var t = Math.Clamp((p - a).Dot(ab) / lengthSquared, 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>True if <paramref name="p"/> lies inside a rectangle of <paramref name="size"/> centred on <paramref name="center"/> and rotated by <paramref name="rotation"/> degrees.</summary>
    public static bool InRotatedRect(Vec2 p, Vec2 center, Vec2 size, double rotation)
    {
        var local = (p - center).Rotate(-rotation);
        return Math.Abs(local.X) <= size.X / 2 && Math.Abs(local.Y) <= size.Y / 2;
    }

    /// <summary>
    /// Finds the wall end nearest to <paramref name="target"/> when the wall from <paramref name="origin"/> may only point
    /// in multiples of <paramref name="angleStepDegrees"/> and be a multiple of <paramref name="lengthStep"/> long
    /// (at least one step).
    /// </summary>
    public static Vec2 ConstrainWall(Vec2 origin, Vec2 target, double angleStepDegrees, double lengthStep)
    {
        var delta = target - origin;
        var step = angleStepDegrees * Math.PI / 180;
        var angle = Round(Math.Atan2(delta.Y, delta.X) / step) * step;
        var length = Math.Max(1, Round(delta.Length / lengthStep)) * lengthStep;

        // Tidy up float noise such as cos(90°) = 6e-17, so orthogonal walls land exactly on the grid.
        var end = origin + new Vec2(Math.Cos(angle), Math.Sin(angle)) * length;
        return new(Math.Round(end.X, 6), Math.Round(end.Y, 6));
    }
}
