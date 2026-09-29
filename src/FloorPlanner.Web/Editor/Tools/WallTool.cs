using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;

namespace FloorPlanner.Web.Editor.Tools;

/// <summary>
/// Click to start a wall, click again to place it and keep going. Right-click or Esc ends the chain.
/// Walls follow <see cref="BuildRules"/>: 2 m sections (1 m with Shift) in fixed angle steps.
/// </summary>
public sealed class WallTool : ITool
{
    private Vec2? _start;
    private Vec2 _cursor;

    public string Name => "Wall";
    public string Hint => _start is null
        ? "Click to start a wall"
        : "Click to place · Shift = 1 m sections · Right-click / Esc to finish";

    public Wall? Preview => _start is { } start ? new Wall(Guid.Empty, start, _cursor) : null;

    public void PointerDown(EditorState state, PointerInput input)
    {
        if (input.Button == PointerInput.Secondary)
        {
            _start = null;
            state.NotifyChanged();
            return;
        }
        if (input.Button != PointerInput.Primary) return;

        var point = Resolve(state, input);
        if (_start is { } start && start != point)
            state.Apply(state.Plan.AddWall(Wall.Create(start, point)));

        _start = point;
        _cursor = point;
        state.NotifyChanged();
    }

    public void PointerMove(EditorState state, PointerInput input) => _cursor = Resolve(state, input);

    public void KeyDown(EditorState state, string key)
    {
        if (key == "Escape") Cancel(state);
    }

    public void Cancel(EditorState state)
    {
        _start = null;
        state.NotifyChanged();
    }

    private Vec2 Resolve(EditorState state, PointerInput input)
    {
        if (_start is not { } start) return state.Snap(input.World);

        var lengthStep = input.Shift ? BuildRules.HalfSection : BuildRules.FullSection;
        var end = GeometryMath.ConstrainWall(start, input.World, BuildRules.AngleStepDegrees, lengthStep);

        // Reuse an existing corner if we landed on it, so rooms close exactly.
        return state.Plan.NearestEndpoint(end, BuildRules.MergeTolerance) ?? end;
    }
}
