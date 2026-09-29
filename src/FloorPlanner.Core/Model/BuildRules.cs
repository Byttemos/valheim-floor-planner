namespace FloorPlanner.Core.Model;

/// <summary>Valheim building constraints. Lengths are in millimetres.</summary>
public static class BuildRules
{
    public const double FullSection = 2000;
    public const double HalfSection = 1000;
    /// <summary>Valheim rotates build pieces in 16 steps per full turn.</summary>
    public const double AngleStepDegrees = 22.5;

    /// <summary>Free-standing pieces snap to a finer grid than walls.</summary>
    public const double PlacementGridSize = 500;
    public const double GridSize = 1000;

    /// <summary>Points closer than this are the same corner (absorbs floating-point error from diagonals).</summary>
    public const double MergeTolerance = 1;

    /// <summary>
    /// How many 2 m and 1 m pieces a wall of the given length needs, using as many full pieces as possible.
    /// </summary>
    public static (int Full, int Half) SectionsFor(double length)
    {
        var halves = (int)Math.Round(length / HalfSection, MidpointRounding.AwayFromZero);
        return (halves / 2, halves % 2);
    }
}
