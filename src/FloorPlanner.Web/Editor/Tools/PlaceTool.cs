using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;

namespace FloorPlanner.Web.Editor.Tools;

/// <summary>
/// Places copies of one catalog piece. Free pieces go anywhere on a 0.5 m grid and rotate with R;
/// wall pieces (doors) only go into walls, lined up with the wall sections.
/// Free pieces rotate with the scroll wheel while they follow the cursor.
/// </summary>
public sealed class PlaceTool(CatalogItem item) : ITool
{
    private const double WallSnapPixels = 24;

    // One rotation step per mouse-wheel notch; trackpads send many small deltas, so accumulate those.
    private const double WheelStepPixels = 50;
    private double _wheelAccumulated;

    private double _rotation;
    private PointerInput? _lastInput;
    private Placement? _preview;
    private bool _valid;

    public CatalogItem Item { get; } = item;

    public string Name => $"Place {Item.Name}";
    public string Hint => Item.Placement == PlacementKind.Wall
        ? $"Move {Item.Name} onto a wall · Shift = 1 m steps · Ctrl = no snapping · Click to place · Esc to finish"
        : $"Click to place {Item.Name} · Scroll to rotate · Ctrl = no snapping · Esc to finish";

    public Placement? PreviewPlacement => _preview;
    public bool PreviewValid => _valid;

    public void PointerDown(EditorState state, PointerInput input)
    {
        if (input.Button == PointerInput.Secondary)
        {
            state.SetTool(new SelectTool());
            return;
        }
        if (input.Button != PointerInput.Primary) return;

        Update(state, input);
        if (_preview is { } preview && _valid)
            state.Apply(state.Plan.AddPlacement(preview with { Id = Guid.NewGuid() }));
    }

    public void PointerMove(EditorState state, PointerInput input) => Update(state, input);

    public void KeyDown(EditorState state, string key)
    {
        if (key == "Escape") state.SetTool(new SelectTool());
    }

    // Doors take their direction from the wall, so for them the wheel keeps zooming.
    public bool Wheel(EditorState state, double deltaY)
    {
        if (Item.Placement != PlacementKind.Free) return false;

        _wheelAccumulated += deltaY;
        if (Math.Abs(_wheelAccumulated) < WheelStepPixels) return true;

        _rotation = Angles.Normalize(_rotation + Math.Sign(_wheelAccumulated) * BuildRules.AngleStepDegrees);
        _wheelAccumulated = 0;
        // Rotating changes which edge faces a wall, so redo the snapping.
        if (_lastInput is { } last) Update(state, last);
        state.NotifyChanged();
        return true;
    }

    public void Cancel(EditorState state) => _preview = null;

    private void Update(EditorState state, PointerInput input)
    {
        _lastInput = input;
        if (Item.Placement == PlacementKind.Free)
        {
            var position = state.PositionPiece(input.World, Item, _rotation, noSnap: input.Ctrl);
            _preview = new Placement(Guid.Empty, Item.Id, position, _rotation);
            _valid = true;
            return;
        }

        var anchor = state.Plan.SnapToWall(input.World, Item.Size.X, state.PixelsToWorld(WallSnapPixels), EditorState.DoorStep(input));
        _preview = new Placement(Guid.Empty, Item.Id, input.World, 0, anchor);
        _valid = anchor is not null;
    }
}
