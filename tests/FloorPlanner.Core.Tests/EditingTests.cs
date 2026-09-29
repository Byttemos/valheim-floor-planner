using FloorPlanner.Core.Editing;
using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Tests;

public class EditingTests
{
    [Fact]
    public void History_undo_and_redo_walk_snapshots()
    {
        var history = new History<int>(0);
        history.Push(1);
        history.Push(2);

        history.Undo();
        Assert.Equal(1, history.Current);
        history.Redo();
        Assert.Equal(2, history.Current);
    }

    [Fact]
    public void History_push_clears_redo_stack()
    {
        var history = new History<int>(0);
        history.Push(1);
        history.Undo();
        history.Push(5);

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Viewport_screen_world_round_trip()
    {
        var viewport = new Viewport(new Vec2(40, 40), 0.1);
        var world = new Vec2(1234, -567);

        var roundTripped = viewport.ScreenToWorld(viewport.WorldToScreen(world));

        Assert.Equal(world.X, roundTripped.X, 6);
        Assert.Equal(world.Y, roundTripped.Y, 6);
    }

    [Fact]
    public void Viewport_zoom_keeps_anchor_point_fixed()
    {
        var viewport = Viewport.Default;
        var anchor = new Vec2(300, 200);
        var worldBefore = viewport.ScreenToWorld(anchor);

        var zoomed = viewport.ZoomAt(anchor, 2);

        Assert.Equal(worldBefore.X, zoomed.ScreenToWorld(anchor).X, 6);
        Assert.Equal(worldBefore.Y, zoomed.ScreenToWorld(anchor).Y, 6);
    }
}
