using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Editing;
using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Materials;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Rooms;
using FloorPlanner.Web.Editor.Tools;

namespace FloorPlanner.Web.Editor;

/// <summary>
/// Everything the editor UI needs: the document with undo history, the viewport, selection and the active tool.
/// Components subscribe to <see cref="Changed"/> and re-render.
/// </summary>
public sealed class EditorState
{
    private const double EndpointSnapPixels = 12;
    private const double PieceWallSnapPixels = 12;

    // Keep wall snapping from grabbing pieces meant to stand 0.5 m off a wall when zoomed far out.
    private const double PieceWallSnapMax = 400;

    public History<FloorPlan> History { get; } = new(FloorPlan.Empty);
    public FloorPlan Plan => History.Current;

    /// <summary>A temporary plan shown during a drag. It is committed to history when the drag ends.</summary>
    public FloorPlan? Draft { get; private set; }
    public FloorPlan DisplayPlan => Draft ?? Plan;

    public Viewport Viewport { get; private set; } = Viewport.Default;
    public double GridSize { get; set; } = BuildRules.GridSize;
    /// <summary>The selected wall or placement.</summary>
    public Guid? SelectedId { get; set; }

    public PieceCatalog Catalog { get; set; } = PieceCatalog.Empty;
    public string? CatalogError { get; set; }
    public Vec2 Cursor { get; private set; }

    public ITool ActiveTool { get; private set; } = new WallTool();

    /// <summary>Whether the bill-of-materials panel is slid out.</summary>
    public bool BomOpen { get; private set; }

    // Rooms only depend on the walls, and the wall list is immutable, so recompute only when it changes.
    private IReadOnlyList<Wall>? _roomsFor;
    private IReadOnlyList<Room> _rooms = [];
    private FloorPlan? _bomFor;
    private BillOfMaterials? _bom;

    /// <summary>The rooms of <see cref="DisplayPlan"/>.</summary>
    public IReadOnlyList<Room> Rooms
    {
        get
        {
            if (!ReferenceEquals(_roomsFor, DisplayPlan.Walls))
            {
                _roomsFor = DisplayPlan.Walls;
                _rooms = RoomFinder.Find(DisplayPlan.Walls);
            }
            return _rooms;
        }
    }

    /// <summary>The bill of materials for <see cref="DisplayPlan"/>, so it follows a drag live.</summary>
    public BillOfMaterials Bom
    {
        get
        {
            if (!ReferenceEquals(_bomFor, DisplayPlan) || _bom is null)
            {
                _bomFor = DisplayPlan;
                _bom = BomCalculator.Calculate(DisplayPlan, Rooms, Catalog);
            }
            return _bom;
        }
    }

    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    public void SetTool(ITool tool)
    {
        ActiveTool.Cancel(this);
        ActiveTool = tool;
        NotifyChanged();
    }

    public void Apply(FloorPlan next)
    {
        Draft = null;
        History.Push(next);
        NotifyChanged();
    }

    public void SetDraft(FloorPlan? draft)
    {
        Draft = draft;
        NotifyChanged();
    }

    public void Load(FloorPlan plan)
    {
        Draft = null;
        SelectedId = null;
        History.Reset(plan);
        NotifyChanged();
    }

    public void Undo() { Draft = null; History.Undo(); NotifyChanged(); }
    public void Redo() { Draft = null; History.Redo(); NotifyChanged(); }

    public void ToggleBom() { BomOpen = !BomOpen; NotifyChanged(); }

    public void SetViewport(Viewport viewport) { Viewport = viewport; NotifyChanged(); }
    public void SetCursor(Vec2 world) { Cursor = world; NotifyChanged(); }

    /// <summary>Converts a distance in screen pixels to millimetres at the current zoom.</summary>
    public double PixelsToWorld(double pixels) => pixels / Viewport.Scale;

    /// <summary>
    /// Where a free piece centred near <paramref name="desired"/> should go: on the 0.5 m grid, then pushed flush
    /// against nearby wall faces. <paramref name="noSnap"/> (Ctrl) places it exactly where asked.
    /// </summary>
    public Vec2 PositionPiece(Vec2 desired, CatalogItem item, double rotation, bool noSnap)
    {
        if (noSnap) return desired;

        var onGrid = GeometryMath.SnapToGrid(desired, BuildRules.PlacementGridSize);
        var tolerance = Math.Min(PixelsToWorld(PieceWallSnapPixels), PieceWallSnapMax);
        return Plan.SnapPieceToWalls(onGrid, item.Size, rotation, tolerance);
    }

    /// <summary>Along-wall step for doors: 2 m sections, 1 m with Shift, free with Ctrl.</summary>
    public static double DoorStep(PointerInput input) =>
        input.Ctrl ? 1 : input.Shift ? BuildRules.HalfSection : BuildRules.FullSection;

    /// <summary>Snaps to a nearby wall endpoint if there is one, otherwise to the grid.</summary>
    public Vec2 Snap(Vec2 world) =>
        Plan.NearestEndpoint(world, PixelsToWorld(EndpointSnapPixels)) ?? GeometryMath.SnapToGrid(world, GridSize);
}
