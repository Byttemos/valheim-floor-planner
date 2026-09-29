using FloorPlanner.Core.Model;

namespace FloorPlanner.Web.Editor;

/// <summary>
/// An editing mode (select, draw wall, ...). The canvas forwards input here; tools change the plan via <see cref="EditorState"/>.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Hint { get; }

    /// <summary>A wall that is being drawn but not committed yet, rendered as a ghost.</summary>
    Wall? Preview => null;

    /// <summary>A piece about to be placed, rendered as a ghost. Invalid previews (e.g. a door away from walls) are shown in red.</summary>
    Placement? PreviewPlacement => null;
    bool PreviewValid => true;

    void PointerDown(EditorState state, PointerInput input);
    void PointerMove(EditorState state, PointerInput input);
    void PointerUp(EditorState state, PointerInput input) { }
    void KeyDown(EditorState state, string key) { }

    /// <summary>
    /// Scroll input, in pixels (positive = scrolling down). Return true to consume it; otherwise the canvas zooms.
    /// </summary>
    bool Wheel(EditorState state, double deltaY) => false;

    /// <summary>Called when the user switches away from this tool.</summary>
    void Cancel(EditorState state) { }
}
