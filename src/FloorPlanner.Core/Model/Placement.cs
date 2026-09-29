using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Model;

/// <summary>A catalog piece placed in the plan.</summary>
/// <param name="ItemId">The <see cref="Catalog.CatalogItem.Id"/>. Looked up at render time, so catalog size fixes apply to existing plans.</param>
/// <param name="Position">Centre of the piece. Ignored while <paramref name="Anchor"/> points at an existing wall.</param>
/// <param name="Rotation">Degrees, clockwise (screen y points down).</param>
/// <param name="Anchor">Set for wall pieces (doors), so they follow their wall when it moves.</param>
public sealed record Placement(Guid Id, string ItemId, Vec2 Position, double Rotation = 0, WallAnchor? Anchor = null)
{
    public static Placement Create(string itemId, Vec2 position, double rotation = 0, WallAnchor? anchor = null) =>
        new(Guid.NewGuid(), itemId, position, rotation, anchor);
}

/// <summary>Attaches a piece to a wall: its centre sits <see cref="Offset"/> mm from the wall's start point.</summary>
public sealed record WallAnchor(Guid WallId, double Offset);

/// <summary>Where a placement actually is once its anchor has been resolved.</summary>
public readonly record struct Pose(Vec2 Center, double Rotation);
