using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Materials;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Rooms;
using FloorPlanner.Core.Serialization;

namespace FloorPlanner.Core.Tests;

public class BomTests
{
    private static readonly PieceCatalog Catalog = PieceCatalog.Parse("""
        [
          { "id": "wood-wall",        "name": "Wall",       "width": 2, "depth": 0.1, "placement": "structure", "cost": { "Wood": 2 } },
          { "id": "wood-wall-small",  "name": "Small wall", "width": 1, "depth": 0.1, "placement": "structure", "cost": { "Wood": 1 } },
          { "id": "wood-floor",       "name": "Floor",      "width": 2, "depth": 2,   "placement": "structure", "cost": { "Wood": 2 } },
          { "id": "wood-floor-small", "name": "Small floor","width": 1, "depth": 1,   "placement": "structure", "cost": { "Wood": 1 } },
          { "id": "wood-roof",        "name": "Roof",       "width": 2, "depth": 2,   "placement": "structure", "cost": { "Wood": 2 } },
          { "id": "door",  "name": "Door",  "width": 2,   "depth": 0.2, "placement": "wall", "cost": { "Wood": 4 } },
          { "id": "forge", "name": "Forge", "width": 2,   "depth": 1.5, "cost": { "Wood": 10, "Copper": 6 } },
          { "id": "hearth", "name": "Hearth", "width": 3, "depth": 2 },
        ]
        """);

    /// <summary>A closed 6 × 4 m room: 10 full wall sections, 24 m² of floor.</summary>
    private static FloorPlan Room6x4()
    {
        var plan = FloorPlan.Empty;
        (double X, double Y)[] corners = [(0, 0), (6000, 0), (6000, 4000), (0, 4000)];
        for (var i = 0; i < corners.Length; i++)
        {
            var (a, b) = (corners[i], corners[(i + 1) % corners.Length]);
            plan = plan.AddWall(Wall.Create(new(a.X, a.Y), new(b.X, b.Y)));
        }
        return plan;
    }

    private static BillOfMaterials Bom(FloorPlan plan) => BomCalculator.Calculate(plan, RoomFinder.Find(plan.Walls), Catalog);

    private static int Count(BillOfMaterials bom, string name) =>
        bom.Sections.SelectMany(s => s.Lines).SingleOrDefault(l => l.Name == name)?.Count ?? 0;

    [Fact]
    public void Counts_walls_floor_and_roof_of_a_room()
    {
        var bom = Bom(Room6x4());

        Assert.Equal(10, Count(bom, "Wall"));
        Assert.Equal(6, Count(bom, "Floor"));
        Assert.Equal(8, Count(bom, "Roof"));  // 24 m² + 20 % = 28.8 m² → 8 pieces
        Assert.Equal(24, bom.FloorAreaSquareMetres, 6);
        Assert.Equal(KeyValuePair.Create("Wood", 20 + 12 + 16), Assert.Single(bom.Totals));
    }

    [Fact]
    public void Extra_courses_multiply_walls_and_a_door_replaces_bottom_sections()
    {
        var plan = Room6x4() with { Build = new(WallCourses: 2, Roof: false) };
        var bottom = plan.Walls[0];
        plan = plan.AddPlacement(Placement.Create("door", bottom.Midpoint, anchor: new WallAnchor(bottom.Id, 3000)));

        var bom = Bom(plan);

        // The door sits at 2–4 m on a 6 m wall: 2 m either side, so one full section is replaced.
        Assert.Equal(19, Count(bom, "Wall"));
        Assert.Equal(0, Count(bom, "Roof"));
        Assert.Equal(1, Count(bom, "Door"));
    }

    [Fact]
    public void Off_section_door_leaves_small_walls_either_side()
    {
        var wall = Wall.Create(new(0, 0), new(6000, 0));
        var plan = FloorPlan.Empty.AddWall(wall)
            .AddPlacement(Placement.Create("door", new(2500, 0), anchor: new WallAnchor(wall.Id, 2000)));

        Assert.Equal((1, 2), BomCalculator.WallPieces(plan, Catalog));  // 1 m | door 1–3 m | 3 m
    }

    [Theory]
    [InlineData(24, 6, 0)]
    [InlineData(25, 6, 1)]
    [InlineData(23.99999, 6, 0)]
    [InlineData(6.5, 1, 3)]
    public void Floor_tiles_prefer_full_tiles(double area, int full, int small) =>
        Assert.Equal((full, small), BomCalculator.FloorTiles(area));

    [Fact]
    public void Pieces_are_grouped_and_unknown_costs_are_reported()
    {
        var plan = FloorPlan.Empty
            .AddPlacement(Placement.Create("forge", new(0, 0)))
            .AddPlacement(Placement.Create("forge", new(3000, 0)))
            .AddPlacement(Placement.Create("hearth", new(6000, 0)))
            .AddPlacement(Placement.Create("gone-from-catalog", new(9000, 0)));

        var bom = Bom(plan);

        Assert.Equal(2, Count(bom, "Forge"));
        Assert.Contains(KeyValuePair.Create("Wood", 20), bom.Totals);
        Assert.Contains(KeyValuePair.Create("Copper", 12), bom.Totals);
        Assert.Equal(["Hearth", "gone-from-catalog"], bom.UnknownCosts.Select(l => l.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Build_settings_round_trip_and_default_for_old_files()
    {
        var plan = Room6x4() with { Build = new(WallCourses: 3, Roof: false) };
        Assert.Equal(plan.Build, PlanSerializer.Deserialize(PlanSerializer.Serialize(plan)).Build);

        var old = PlanSerializer.Deserialize("""{ "name": "Old", "walls": [] }""");
        Assert.Equal(new BuildSettings(), old.Build);
    }

    [Fact]
    public void Structure_pieces_are_not_placeable() =>
        Assert.DoesNotContain(Catalog.Placeable, i => i.Id.StartsWith("wood-"));
}
