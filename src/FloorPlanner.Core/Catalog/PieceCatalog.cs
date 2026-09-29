using System.Text.Json;
using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Catalog;

/// <summary>The set of pieces that can be placed, loaded from a hand-edited catalog.json.</summary>
public sealed class PieceCatalog
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // The file is edited by hand, so be forgiving.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Dictionary<string, CatalogItem> _byId;

    public PieceCatalog(IEnumerable<CatalogItem> items)
    {
        Items = items.ToList();
        _byId = Items.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static PieceCatalog Empty { get; } = new([]);

    public IReadOnlyList<CatalogItem> Items { get; }

    /// <summary>The pieces shown in the side panel (everything except structure pieces).</summary>
    public IEnumerable<CatalogItem> Placeable => Items.Where(i => i.Placement != PlacementKind.Structure);

    public CatalogItem? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Parses catalog.json. Sizes in the file are in metres.</summary>
    public static PieceCatalog Parse(string json)
    {
        var entries = JsonSerializer.Deserialize<List<Entry>>(json, Options) ?? [];
        return new(entries.Select(ToItem));
    }

    private static CatalogItem ToItem(Entry e)
    {
        if (string.IsNullOrWhiteSpace(e.Id)) throw new JsonException($"Catalog entry '{e.Name}' has no id.");
        if (e.Width <= 0 || e.Depth <= 0) throw new JsonException($"Catalog entry '{e.Id}' needs a positive width and depth.");

        var placement = e.Placement?.ToLowerInvariant() switch
        {
            null or "free" => PlacementKind.Free,
            "wall" => PlacementKind.Wall,
            "structure" => PlacementKind.Structure,
            var other => throw new JsonException(
                $"Catalog entry '{e.Id}' has unknown placement '{other}' (use \"free\", \"wall\" or \"structure\")."),
        };
        if (e.Cost?.FirstOrDefault(c => c.Value <= 0) is { Key: not null } bad)
            throw new JsonException($"Catalog entry '{e.Id}' has a non-positive cost for '{bad.Key}'.");

        return new(e.Id, e.Name ?? e.Id, e.Category ?? "Other", new Vec2(e.Width * 1000, e.Depth * 1000), placement, e.Image, e.Cost);
    }

    private sealed record Entry(
        string Id, string? Name, string? Category, double Width, double Depth, string? Placement, string? Image,
        Dictionary<string, int>? Cost);
}
