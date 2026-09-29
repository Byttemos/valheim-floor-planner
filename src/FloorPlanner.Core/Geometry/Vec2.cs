namespace FloorPlanner.Core.Geometry;

/// <summary>A 2D point or vector in world space. Units are millimetres.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public double Dot(Vec2 other) => X * other.X + Y * other.Y;
    public double DistanceTo(Vec2 other) => (this - other).Length;

    /// <summary>Rotates around the origin by <paramref name="degrees"/> (clockwise on screen).</summary>
    public Vec2 Rotate(double degrees)
    {
        var r = Angles.ToRadians(degrees);
        var (sin, cos) = Math.SinCos(r);
        return new(X * cos - Y * sin, X * sin + Y * cos);
    }
}
