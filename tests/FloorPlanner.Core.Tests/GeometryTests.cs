using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Tests;

public class GeometryTests
{
    private const double Step = Model.BuildRules.AngleStepDegrees;

    [Theory]
    [InlineData(149, 251, 100, 300)]
    [InlineData(-49, 50, 0, 100)]
    public void SnapToGrid_rounds_to_nearest_grid_point(double x, double y, double ex, double ey) =>
        Assert.Equal(new Vec2(ex, ey), GeometryMath.SnapToGrid(new Vec2(x, y), 100));

    [Fact]
    public void DistanceToSegment_uses_perpendicular_distance_inside_segment() =>
        Assert.Equal(50, GeometryMath.DistanceToSegment(new(500, 50), new(0, 0), new(1000, 0)), 6);

    [Fact]
    public void DistanceToSegment_uses_endpoint_distance_outside_segment() =>
        Assert.Equal(5, GeometryMath.DistanceToSegment(new(1003, 4), new(0, 0), new(1000, 0)), 6);

    [Theory]
    [InlineData(4900, 300, 4000, 0)]      // nearly horizontal, 4.9 m -> 4 m
    [InlineData(5100, 300, 6000, 0)]      // 5.1 m -> 6 m
    [InlineData(300, 40, 2000, 0)]        // too short -> one section
    [InlineData(-200, 3100, 0, 4000)]     // nearly straight down (y grows downwards) -> 90°
    public void ConstrainWall_snaps_to_two_metre_sections(double x, double y, double ex, double ey)
    {
        var end = GeometryMath.ConstrainWall(Vec2.Zero, new(x, y), Step, 2000);

        Assert.Equal(ex, end.X, 6);
        Assert.Equal(ey, end.Y, 6);
    }

    [Fact]
    public void ConstrainWall_uses_one_metre_sections_when_asked() =>
        Assert.Equal(new Vec2(3000, 0), GeometryMath.ConstrainWall(Vec2.Zero, new(3200, 100), Step, 1000));

    [Fact]
    public void ConstrainWall_snaps_diagonals_to_22_5_degree_steps()
    {
        // 20° from horizontal -> 22.5°.
        var target = new Vec2(Math.Cos(Angles.ToRadians(20)), Math.Sin(Angles.ToRadians(20))) * 4000;

        var end = GeometryMath.ConstrainWall(Vec2.Zero, target, Step, 2000);

        Assert.Equal(22.5, Angles.Of(end), 6);
        Assert.Equal(4000, end.Length, 6);
    }

    [Fact]
    public void ConstrainWall_octagon_closes_back_on_start()
    {
        // Eight 2 m walls turning 45° each time should end where they started (within merge tolerance).
        var point = Vec2.Zero;
        for (var i = 0; i < 8; i++)
        {
            var heading = Angles.ToRadians(i * 45);
            point = GeometryMath.ConstrainWall(point, point + new Vec2(Math.Cos(heading), Math.Sin(heading)) * 2000, Step, 2000);
        }

        Assert.True(point.Length < Model.BuildRules.MergeTolerance);
    }

    [Fact]
    public void InRotatedRect_respects_rotation()
    {
        // A 2 m x 0.5 m piece turned 90° is tall and narrow.
        Assert.True(GeometryMath.InRotatedRect(new(0, 900), Vec2.Zero, new(2000, 500), 90));
        Assert.False(GeometryMath.InRotatedRect(new(900, 0), Vec2.Zero, new(2000, 500), 90));
    }
}
