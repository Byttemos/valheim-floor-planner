using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Web.Editor;

/// <summary>A pointer event already translated into world coordinates.</summary>
public readonly record struct PointerInput(Vec2 World, Vec2 Screen, long Button, bool Shift, bool Ctrl)
{
    public const long Primary = 0;
    public const long Middle = 1;
    public const long Secondary = 2;
}
