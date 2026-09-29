using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Serialization;

namespace FloorPlanner.Core.Tests;

public class PlacementTests
{
    private static readonly PieceCatalog Catalog = new([
        new CatalogItem("door", "Door", "Doors", new(2000, 200), PlacementKind.Wall, null),
        new CatalogItem("bed", "Bed", "Living", new(1000, 2000), PlacementKind.Free, null),
    ]);

    private static readonly Wall Wall = Wall.Create(new(0, 0), new(8000, 0));
    private static readonly FloorPlan Plan = FloorPlan.Empty.AddWall(Wall);

    [Theory]
    [InlineData(2900, 3000)]  // door edges land on section boundaries: 2–4 m
    [InlineData(4100, 5000)]  // 4–6 m
    [InlineData(-200, 1000)]  // clamped to the wall's start
    [InlineData(8200, 7000)]  // clamped to the wall's end
    public void SnapToWall_aligns_door_to_sections(double x, double expectedOffset)
    {
        var anchor = Plan.SnapToWall(new(x, 50), width: 2000, tolerance: 300, step: BuildRules.FullSection);

        Assert.NotNull(anchor);
        Assert.Equal(Wall.Id, anchor.WallId);
        Assert.Equal(expectedOffset, anchor.Offset, 6);
    }

    [Fact]
    public void SnapToWall_ignores_walls_too_short_or_too_far()
    {
        var shortWall = FloorPlan.Empty.AddWall(Wall.Create(new(0, 0), new(1000, 0)));

        Assert.Null(shortWall.SnapToWall(new(500, 0), 2000, 300, 2000));
        Assert.Null(Plan.SnapToWall(new(3000, 1000), 2000, 300, 2000));
    }

    [Fact]
    public void Anchored_door_follows_its_wall()
    {
        var door = Placement.Create("door", Vec2.Zero, anchor: new WallAnchor(Wall.Id, 3000));
        var plan = Plan.AddPlacement(door);

        var turned = plan.ReplaceWall(Wall with { Start = new(1000, 1000), End = new(1000, 9000) });
        var pose = turned.PoseOf(door);

        Assert.Equal(1000, pose.Center.X, 6);
        Assert.Equal(4000, pose.Center.Y, 6);
        Assert.Equal(90, pose.Rotation, 6);
    }

    [Fact]
    public void Removing_a_wall_removes_its_doors()
    {
        var plan = Plan.AddPlacement(Placement.Create("door", Vec2.Zero, anchor: new WallAnchor(Wall.Id, 3000)));

        Assert.Empty(plan.RemoveWall(Wall.Id).Placements);
    }

    [Fact]
    public void OpeningsIn_reports_door_span()
    {
        var plan = Plan.AddPlacement(Placement.Create("door", Vec2.Zero, anchor: new WallAnchor(Wall.Id, 3000)));

        Assert.Equal([(2000.0, 4000.0)], plan.OpeningsIn(Wall, Catalog));
    }

    [Fact]
    public void HitTestPlacement_uses_catalog_size_and_rotation()
    {
        var bed = Placement.Create("bed", new(5000, 5000), rotation: 90);
        var plan = Plan.AddPlacement(bed);

        Assert.Equal(bed, plan.HitTestPlacement(new(5900, 5000), Catalog));   // 2 m long, now along x
        Assert.Null(plan.HitTestPlacement(new(5000, 5900), Catalog));
    }

    [Fact]
    public void Serializer_round_trips_placements_and_reads_old_files()
    {
        var plan = Plan.AddPlacement(Placement.Create("door", Vec2.Zero, anchor: new WallAnchor(Wall.Id, 3000)));

        Assert.Equal(plan.Placements, PlanSerializer.Deserialize(PlanSerializer.Serialize(plan)).Placements);
        Assert.Empty(PlanSerializer.Deserialize("""{ "name": "old", "walls": [] }""").Placements);
    }
}

public class PieceWallSnappingTests
{
    // Walls are 100 mm thick, so their faces are 50 mm either side of the centre line.
    private static readonly FloorPlan Corner = FloorPlan.Empty
        .AddWall(Wall.Create(new(0, 0), new(8000, 0)))
        .AddWall(Wall.Create(new(0, 0), new(0, 8000)));

    private static readonly Vec2 Bed = new(1000, 2000);

    [Fact]
    public void Piece_near_a_wall_moves_flush_against_its_face()
    {
        // Grid-snapped at y = 1000: top edge at y = 0, i.e. inside the wall. Flush means top edge at y = 50.
        var snapped = Corner.SnapPieceToWalls(new(3000, 1000), Bed, 0, tolerance: 300);

        Assert.Equal(new Vec2(3000, 1050), snapped);
    }

    [Fact]
    public void Piece_in_a_corner_snaps_to_both_walls() =>
        Assert.Equal(new Vec2(550, 1050), Corner.SnapPieceToWalls(new(500, 1000), Bed, 0, tolerance: 300));

    [Fact]
    public void Rotation_changes_which_side_touches_the_wall() =>
        // Rotated 90°, the bed is 2 m wide and 1 m deep: centre 500 + 50 from the wall.
        Assert.Equal(550, Corner.SnapPieceToWalls(new(3000, 600), Bed, 90, tolerance: 300).Y, 6);

    [Fact]
    public void Piece_stays_on_its_own_side_of_the_wall() =>
        Assert.Equal(-1050, Corner.SnapPieceToWalls(new(3000, -1000), Bed, 0, tolerance: 300).Y, 6);

    [Fact]
    public void Far_away_or_angled_pieces_are_left_alone()
    {
        Assert.Equal(new Vec2(3000, 2000), Corner.SnapPieceToWalls(new(3000, 2000), Bed, 0, tolerance: 300));
        Assert.Equal(new Vec2(3000, 1000), Corner.SnapPieceToWalls(new(3000, 1000), Bed, 22.5, tolerance: 300));
    }

    [Fact]
    public void Diagonal_wall_snaps_a_piece_rotated_to_match()
    {
        var plan = FloorPlan.Empty.AddWall(Wall.Create(Vec2.Zero, new Vec2(1, 0).Rotate(45) * 8000));

        var snapped = plan.SnapPieceToWalls(new Vec2(1, 0).Rotate(45) * 4000 + new Vec2(0, 1).Rotate(45) * 900, Bed, 45, 300);

        // Rotated 45° the bed's short axis is across the wall: 1000 mm + 50 mm from the centre line.
        Assert.Equal(1050, Math.Abs(snapped.Dot(new Vec2(0, 1).Rotate(45))), 3);
    }
}
