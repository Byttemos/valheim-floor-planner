using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;

namespace FloorPlanner.Web.Editor.Tools;

/// <summary>Click a wall or piece to select it, drag to move it, R to rotate a placed piece, Delete to remove.</summary>
public sealed class SelectTool : ITool
{
    private const double HitTolerancePixels = 6;
    private const double WallSnapPixels = 24;

    private Wall? _draggingWall;
    private Placement? _draggingPlacement;
    private Vec2 _dragOrigin;

    public string Name => "Select";
    public string Hint => "Click to select · Drag to move (Ctrl = no snapping) · R to rotate a piece · Delete to remove";

    public void PointerDown(EditorState state, PointerInput input)
    {
        if (input.Button != PointerInput.Primary) return;

        // Pieces are drawn on top of walls, so they win the hit test.
        _draggingPlacement = state.Plan.HitTestPlacement(input.World, state.Catalog);
        _draggingWall = _draggingPlacement is null
            ? state.Plan.HitTestWall(input.World, state.PixelsToWorld(HitTolerancePixels))
            : null;
        _dragOrigin = input.World;

        state.SelectedId = _draggingPlacement?.Id ?? _draggingWall?.Id;
        state.NotifyChanged();
    }

    public void PointerMove(EditorState state, PointerInput input)
    {
        if (_draggingWall is { } wall)
        {
            var delta = Snapped(input.World, BuildRules.GridSize) - Snapped(_dragOrigin, BuildRules.GridSize);
            state.SetDraft(state.Plan.ReplaceWall(wall with { Start = wall.Start + delta, End = wall.End + delta }));
        }
        else if (_draggingPlacement is { } placement)
        {
            state.SetDraft(state.Plan.ReplacePlacement(Move(state, placement, input)));
        }
    }

    public void PointerUp(EditorState state, PointerInput input)
    {
        _draggingWall = null;
        _draggingPlacement = null;
        if (state.Draft is { } draft) state.Apply(draft);
    }

    public void KeyDown(EditorState state, string key)
    {
        if (state.SelectedId is not { } id) return;

        switch (key)
        {
            case "Delete" or "Backspace":
                state.SelectedId = null;
                state.Apply(state.Plan.Remove(id));
                break;
            case "r" or "R" when state.Plan.FindPlacement(id) is { Anchor: null } placement:
                var rotation = Angles.Normalize(placement.Rotation + BuildRules.AngleStepDegrees);
                state.Apply(state.Plan.ReplacePlacement(placement with { Rotation = rotation }));
                break;
        }
    }

    public void Cancel(EditorState state)
    {
        _draggingWall = null;
        _draggingPlacement = null;
        state.SetDraft(null);
    }

    private Placement Move(EditorState state, Placement placement, PointerInput input)
    {
        var item = state.Catalog.Find(placement.ItemId);
        if (item?.Placement == PlacementKind.Wall)
        {
            // Doors slide along walls, and can hop to another wall, but never leave one.
            var anchor = state.Plan.SnapToWall(input.World, item.Size.X, state.PixelsToWorld(WallSnapPixels), EditorState.DoorStep(input));
            return anchor is null ? placement : placement with { Anchor = anchor };
        }

        var desired = placement.Position + (input.World - _dragOrigin);
        return placement with
        {
            Position = item is null ? desired : state.PositionPiece(desired, item, placement.Rotation, noSnap: input.Ctrl),
        };
    }

    private static Vec2 Snapped(Vec2 p, double grid) => GeometryMath.SnapToGrid(p, grid);
}
