using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Rooms;

namespace FloorPlanner.Core.Tests;

public class RoomFinderTests
{
    private static IEnumerable<Wall> Chain(params (double X, double Y)[] metres) =>
        metres.Zip(metres.Skip(1)).Select(p => Wall.Create(new(p.First.X * 1000, p.First.Y * 1000), new(p.Second.X * 1000, p.Second.Y * 1000)));

    [Fact]
    public void Closed_rectangle_is_one_room()
    {
        var room = Assert.Single(RoomFinder.Find(Chain((0, 0), (6, 0), (6, 4), (0, 4), (0, 0))));

        Assert.Equal(24, room.AreaSquareMetres, 6);
        Assert.Equal(new Vec2(3000, 2000), room.Centroid);
    }

    [Fact]
    public void Drawing_direction_does_not_matter() =>
        Assert.Single(RoomFinder.Find(Chain((0, 0), (0, 4), (6, 4), (6, 0), (0, 0))));

    [Fact]
    public void Open_chain_is_not_a_room() =>
        Assert.Empty(RoomFinder.Find(Chain((0, 0), (6, 0), (6, 4), (0, 4))));

    [Fact]
    public void Dividing_wall_ending_mid_wall_makes_two_rooms()
    {
        var walls = Chain((0, 0), (8, 0), (8, 4), (0, 4), (0, 0)).Concat(Chain((4, 0), (4, 4)));

        var rooms = RoomFinder.Find(walls);

        Assert.Equal(2, rooms.Count);
        Assert.All(rooms, r => Assert.Equal(16, r.AreaSquareMetres, 6));
    }

    [Fact]
    public void Stub_wall_inside_a_room_does_not_change_it()
    {
        var walls = Chain((0, 0), (6, 0), (6, 4), (0, 4), (0, 0)).Concat(Chain((2, 0), (2, 2)));

        Assert.Equal(24, Assert.Single(RoomFinder.Find(walls)).AreaSquareMetres, 6);
    }

    [Fact]
    public void Separate_buildings_are_separate_rooms() =>
        Assert.Equal(2, RoomFinder.Find(Chain((0, 0), (2, 0), (2, 2), (0, 2), (0, 0))
            .Concat(Chain((10, 0), (12, 0), (12, 2), (10, 2), (10, 0)))).Count);

    [Fact]
    public void Diagonal_octagon_drawn_with_the_wall_rules_closes_into_a_room()
    {
        var plan = FloorPlan.Empty;
        var point = Vec2.Zero;
        for (var i = 0; i < 8; i++)
        {
            var heading = Angles.ToRadians(i * 45);
            var end = GeometryMath.ConstrainWall(point, point + new Vec2(Math.Cos(heading), Math.Sin(heading)) * 2000,
                BuildRules.AngleStepDegrees, BuildRules.FullSection);
            end = plan.NearestEndpoint(end, BuildRules.MergeTolerance) ?? end; // what WallTool does
            plan = plan.AddWall(Wall.Create(point, end));
            point = end;
        }

        // Regular octagon with 2 m sides: 2(1 + √2)·s² ≈ 19.31 m²
        Assert.Equal(2 * (1 + Math.Sqrt(2)) * 4, Assert.Single(RoomFinder.Find(plan.Walls)).AreaSquareMetres, 3);
    }
}
