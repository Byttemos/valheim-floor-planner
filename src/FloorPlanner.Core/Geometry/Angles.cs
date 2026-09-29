namespace FloorPlanner.Core.Geometry;

public static class Angles
{
    public static double ToRadians(double degrees) => degrees * Math.PI / 180;
    public static double ToDegrees(double radians) => radians * 180 / Math.PI;

    /// <summary>Direction of <paramref name="v"/> in degrees (clockwise on screen, since y points down).</summary>
    public static double Of(Vec2 v) => ToDegrees(Math.Atan2(v.Y, v.X));

    /// <summary>Normalises to [0, 360).</summary>
    public static double Normalize(double degrees) => ((degrees % 360) + 360) % 360;
}
