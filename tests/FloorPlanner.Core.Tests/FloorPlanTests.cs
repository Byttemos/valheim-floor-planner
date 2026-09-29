using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Serialization;

namespace FloorPlanner.Core.Tests;

public class FloorPlanTests
{
    private static readonly Wall Horizontal = Wall.Create(new(0, 0), new(4000, 0));
    private static readonly FloorPlan Plan = FloorPlan.Empty.AddWall(Horizontal);

    [Fact]
    public void Edits_return_new_plan_and_leave_original_untouched()
    {
        var removed = Plan.RemoveWall(Horizontal.Id);

        Assert.Empty(removed.Walls);
        Assert.Single(Plan.Walls);
    }

    [Fact]
    public void HitTestWall_finds_wall_within_tolerance() =>
        Assert.Equal(Horizontal, Plan.HitTestWall(new(2000, 80), tolerance: 50));

    [Fact]
    public void HitTestWall_misses_when_too_far() =>
        Assert.Null(Plan.HitTestWall(new(2000, 500), tolerance: 50));

    [Fact]
    public void NearestEndpoint_snaps_to_wall_end() =>
        Assert.Equal(new Vec2(4000, 0), Plan.NearestEndpoint(new(3980, 15), tolerance: 50));

    [Fact]
    public void Serializer_round_trips_plan()
    {
        var restored = PlanSerializer.Deserialize(PlanSerializer.Serialize(Plan));

        Assert.Equal(Plan.Name, restored.Name);
        Assert.Equal(Plan.Walls, restored.Walls);
    }
}

public class BuildRulesTests
{
    [Theory]
    [InlineData(2000, 1, 0)]
    [InlineData(5000, 2, 1)]
    [InlineData(1000, 0, 1)]
    [InlineData(5999.9999, 3, 0)] // float noise from diagonals must not lose a section
    public void SectionsFor_prefers_full_sections(double length, int full, int half) =>
        Assert.Equal((full, half), Model.BuildRules.SectionsFor(length));
}
