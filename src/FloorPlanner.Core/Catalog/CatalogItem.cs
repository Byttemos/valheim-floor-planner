using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Catalog;

public enum PlacementKind
{
    /// <summary>Stands anywhere on the floor (bed, chest, workbench …).</summary>
    Free,

    /// <summary>Sits in a wall and replaces the wall sections it covers (doors).</summary>
    Wall,

    /// <summary>
    /// A building piece the planner counts but doesn't let you place (wall sections, floor tiles, roof). It only
    /// supplies the name and cost for the bill of materials.
    /// </summary>
    Structure,
}

/// <summary>
/// A placeable piece. <see cref="Size"/> is the in-game footprint in millimetres: X = width (along a wall for wall
/// pieces), Y = depth. <see cref="Cost"/> is the build recipe (resource name → amount), or null when unknown.
/// </summary>
public sealed record CatalogItem(
    string Id, string Name, string Category, Vec2 Size, PlacementKind Placement, string? Image,
    IReadOnlyDictionary<string, int>? Cost = null);
