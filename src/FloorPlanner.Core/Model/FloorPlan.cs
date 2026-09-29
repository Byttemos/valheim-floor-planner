using System.Collections.Immutable;
using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Model;

/// <summary>
/// The whole document. Immutable: every edit returns a new plan, which makes undo/redo a simple stack of snapshots.
/// </summary>
public sealed record FloorPlan(string Name, ImmutableList<Wall> Walls)
{
    public static FloorPlan Empty { get; } = new("Untitled plan", []);

    /// <summary>Placed catalog pieces. Not a constructor parameter, so plan files without it still load.</summary>
    public ImmutableList<Placement> Placements { get; init; } = [];

    /// <summary>Settings for the bill of materials. Also optional in plan files.</summary>
    public BuildSettings Build { get; init; } = new();

    public Wall? FindWall(Guid id) => Walls.Find(w => w.Id == id);
    public Placement? FindPlacement(Guid id) => Placements.Find(p => p.Id == id);

    public FloorPlan AddWall(Wall wall) => this with { Walls = Walls.Add(wall) };

    /// <summary>Removes the wall and any doors sitting in it.</summary>
    public FloorPlan RemoveWall(Guid id) => this with
    {
        Walls = Walls.RemoveAll(w => w.Id == id),
        Placements = Placements.RemoveAll(p => p.Anchor?.WallId == id),
    };

    public FloorPlan AddPlacement(Placement placement) => this with { Placements = Placements.Add(placement) };

    public FloorPlan RemovePlacement(Guid id) => this with { Placements = Placements.RemoveAll(p => p.Id == id) };

    public FloorPlan ReplacePlacement(Placement placement)
    {
        var index = Placements.FindIndex(p => p.Id == placement.Id);
        return index < 0 ? this : this with { Placements = Placements.SetItem(index, placement) };
    }

    /// <summary>Removes whatever <paramref name="id"/> refers to, wall or placement.</summary>
    public FloorPlan Remove(Guid id) => FindWall(id) is not null ? RemoveWall(id) : RemovePlacement(id);

    public FloorPlan ReplaceWall(Wall wall)
    {
        var index = Walls.FindIndex(w => w.Id == wall.Id);
        return index < 0 ? this : this with { Walls = Walls.SetItem(index, wall) };
    }

    /// <summary>Returns the wall closest to <paramref name="p"/>, if any lies within <paramref name="tolerance"/>.</summary>
    public Wall? HitTestWall(Vec2 p, double tolerance) =>
        Walls
            .Select(w => (Wall: w, Distance: GeometryMath.DistanceToSegment(p, w.Start, w.End)))
            .Where(x => x.Distance <= tolerance + x.Wall.Thickness / 2)
            .OrderBy(x => x.Distance)
            .Select(x => x.Wall)
            .FirstOrDefault();

    /// <summary>Returns the wall endpoint closest to <paramref name="p"/> within <paramref name="tolerance"/>, used for snapping.</summary>
    public Vec2? NearestEndpoint(Vec2 p, double tolerance)
    {
        Vec2? best = null;
        var bestDistance = tolerance;
        foreach (var candidate in Walls.SelectMany(w => new[] { w.Start, w.End }))
        {
            var distance = p.DistanceTo(candidate);
            if (distance <= bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Resolves where a placement is. Anchored pieces follow their wall's position and direction.</summary>
    public Pose PoseOf(Placement placement)
    {
        if (placement.Anchor is not { } anchor || FindWall(anchor.WallId) is not { Length: > 0 } wall)
            return new(placement.Position, placement.Rotation);

        var direction = (wall.End - wall.Start) / wall.Length;
        return new(wall.Start + direction * anchor.Offset, Angles.Of(direction));
    }

    /// <summary>The topmost placement under <paramref name="p"/>. Pieces missing from the catalog are ignored.</summary>
    public Placement? HitTestPlacement(Vec2 p, PieceCatalog catalog) =>
        Placements
            .AsEnumerable()
            .Reverse()
            .FirstOrDefault(placement =>
                catalog.Find(placement.ItemId) is { } item &&
                PoseOf(placement) is var pose &&
                GeometryMath.InRotatedRect(p, pose.Center, item.Size, pose.Rotation));

    /// <summary>The stretches of <paramref name="wall"/> taken up by doors, as distances from its start point.</summary>
    public IEnumerable<(double From, double To)> OpeningsIn(Wall wall, PieceCatalog catalog) =>
        Placements
            .Where(p => p.Anchor?.WallId == wall.Id)
            .Select(p => (p.Anchor!.Offset, Width: catalog.Find(p.ItemId)?.Size.X ?? 0))
            .Where(o => o.Width > 0)
            .Select(o => (o.Offset - o.Width / 2, o.Offset + o.Width / 2))
            .OrderBy(o => o.Item1);

    /// <summary>
    /// Finds where a wall piece <paramref name="width"/> wide would sit if dropped near <paramref name="p"/>:
    /// on the nearest wall that is long enough, with its edges on section boundaries (<paramref name="step"/>)
    /// measured from the wall's start.
    /// </summary>
    public WallAnchor? SnapToWall(Vec2 p, double width, double tolerance, double step)
    {
        var wall = Walls
            .Where(w => w.Length >= width - BuildRules.MergeTolerance)
            .Select(w => (Wall: w, Distance: GeometryMath.DistanceToSegment(p, w.Start, w.End)))
            .Where(x => x.Distance <= tolerance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Wall)
            .FirstOrDefault();
        if (wall is null) return null;

        var along = (p - wall.Start).Dot(wall.End - wall.Start) / wall.Length;
        var start = Math.Round((along - width / 2) / step, MidpointRounding.AwayFromZero) * step;
        start = Math.Clamp(start, 0, Math.Max(0, wall.Length - width));
        return new WallAnchor(wall.Id, start + width / 2);
    }

    /// <summary>
    /// Nudges a piece so its edges sit flush against nearby wall faces. It snaps to at most two walls that aren't
    /// parallel to each other, so pieces fit into corners. Only walls the piece has an edge parallel to count,
    /// and a face within <paramref name="tolerance"/> of the piece's edge. The piece stays on the side of the wall
    /// its centre is on.
    /// </summary>
    public Vec2 SnapPieceToWalls(Vec2 center, Vec2 size, double rotation, double tolerance)
    {
        var u = new Vec2(1, 0).Rotate(rotation);
        var v = new Vec2(0, 1).Rotate(rotation);
        Vec2? firstNormal = null;

        for (var pass = 0; pass < 2; pass++)
        {
            (Vec2 Shift, Vec2 Normal, double Distance)? best = null;
            foreach (var wall in Walls.Where(w => w.Length > 0))
            {
                var direction = (wall.End - wall.Start) / wall.Length;
                var normal = new Vec2(-direction.Y, direction.X);
                if (firstNormal is { } n1 && Math.Abs(normal.Dot(n1)) > 0.99) continue;

                // The piece needs an edge parallel to the wall for "flush" to mean anything.
                var edgeParallel = Math.Abs(u.Dot(normal)) < 1e-3 || Math.Abs(v.Dot(normal)) < 1e-3;
                if (!edgeParallel) continue;

                var halfAcross = Math.Abs(size.X / 2 * u.Dot(normal)) + Math.Abs(size.Y / 2 * v.Dot(normal));
                var halfAlong = Math.Abs(size.X / 2 * u.Dot(direction)) + Math.Abs(size.Y / 2 * v.Dot(direction));

                // Only walls the piece actually lies alongside.
                var along = (center - wall.Start).Dot(direction);
                if (along <= -halfAlong || along >= wall.Length + halfAlong) continue;

                var across = (center - wall.Start).Dot(normal);
                var target = Math.Sign(across == 0 ? 1 : across) * (wall.Thickness / 2 + halfAcross);
                var distance = Math.Abs(target - across);
                if (distance > tolerance || distance >= (best?.Distance ?? double.MaxValue)) continue;

                best = (normal * (target - across), normal, distance);
            }

            if (best is not { } snap) break;
            center += snap.Shift;
            firstNormal = snap.Normal;
        }
        return center;
    }
}
