using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Rooms;

namespace FloorPlanner.Core.Materials;

/// <summary>
/// Counts the pieces a plan needs. Walls and placed pieces are exact; floors and the roof are estimated from the
/// room areas. Names and costs come from the catalog's structure entries (the ids below).
/// </summary>
public static class BomCalculator
{
    public const string WallId = "wood-wall";
    public const string SmallWallId = "wood-wall-small";
    public const string FloorId = "wood-floor";
    public const string SmallFloorId = "wood-floor-small";
    public const string RoofId = "wood-roof";

    /// <summary>A roof piece covers 2 × 2 m of floor, whatever its pitch.</summary>
    public const double RoofPieceArea = 4;

    /// <summary>Extra roof pieces for eaves, the ridge and cut pieces at hips and valleys.</summary>
    public const double RoofAllowance = 0.2;

    public static BillOfMaterials Calculate(FloorPlan plan, IReadOnlyList<Room> rooms, PieceCatalog catalog)
    {
        var (fullWalls, halfWalls) = WallPieces(plan, catalog);
        var floorArea = rooms.Sum(r => r.AreaSquareMetres);
        var floorTiles = rooms.Select(r => FloorTiles(r.AreaSquareMetres))
            .Aggregate((Full: 0, Small: 0), (sum, t) => (sum.Full + t.Full, sum.Small + t.Small));

        var structure = new List<BomLine>
        {
            Line(catalog, WallId, "Wood wall 2 m", fullWalls),
            Line(catalog, SmallWallId, "Wood wall 1 m", halfWalls),
            Line(catalog, FloorId, "Wood floor 2×2", floorTiles.Full),
            Line(catalog, SmallFloorId, "Wood floor 1×1", floorTiles.Small),
        };
        if (plan.Build.Roof)
            structure.Add(Line(catalog, RoofId, "Wood roof", RoofPieces(floorArea)));

        var pieces = plan.Placements
            .GroupBy(p => p.ItemId, StringComparer.OrdinalIgnoreCase)
            .Select(g => Line(catalog, g.Key, g.Key, g.Count()))
            .OrderBy(l => l.Name);

        return new(
            [
                new("Structure", structure.Where(l => l.Count > 0).ToList()),
                new("Pieces", pieces.ToList()),
            ],
            floorArea);
    }

    /// <summary>
    /// Wall pieces over all courses. Doors replace the bottom course where they sit, so the wall either side of a door
    /// is counted on its own.
    /// </summary>
    public static (int Full, int Half) WallPieces(FloorPlan plan, PieceCatalog catalog)
    {
        var courses = Math.Clamp(plan.Build.WallCourses, 1, BuildSettings.MaxWallCourses);
        int full = 0, half = 0;
        foreach (var wall in plan.Walls)
        {
            var whole = BuildRules.SectionsFor(wall.Length);
            full += whole.Full * (courses - 1);
            half += whole.Half * (courses - 1);

            var pos = 0.0;
            foreach (var (from, to) in plan.OpeningsIn(wall, catalog).Append((wall.Length, wall.Length)))
            {
                var gap = BuildRules.SectionsFor(Math.Max(0, from - pos));
                full += gap.Full;
                half += gap.Half;
                pos = Math.Max(pos, to);
            }
        }
        return (full, half);
    }

    /// <summary>2 × 2 m tiles for as much of the area as possible, 1 × 1 m tiles for the rest.</summary>
    public static (int Full, int Small) FloorTiles(double squareMetres)
    {
        var area = Math.Round(squareMetres, 2);
        var full = (int)(area / 4);
        return (full, (int)Math.Ceiling(area - full * 4));
    }

    public static int RoofPieces(double floorSquareMetres) =>
        floorSquareMetres <= 0 ? 0 : (int)Math.Ceiling(Math.Round(floorSquareMetres * (1 + RoofAllowance), 2) / RoofPieceArea);

    private static BomLine Line(PieceCatalog catalog, string id, string fallbackName, int count) =>
        catalog.Find(id) is { } item ? new(item.Name, count, item.Cost) : new(fallbackName, count, null);
}
