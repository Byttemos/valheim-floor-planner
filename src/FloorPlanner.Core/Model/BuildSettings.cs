namespace FloorPlanner.Core.Model;

/// <summary>
/// Per-plan choices that affect the bill of materials but aren't drawn: how tall the walls are, and whether to count
/// a roof.
/// </summary>
/// <param name="WallCourses">Wall height in 2 m courses (Valheim wall pieces are 2 m tall).</param>
public sealed record BuildSettings(int WallCourses = 1, bool Roof = true)
{
    public const int MaxWallCourses = 4;
}
