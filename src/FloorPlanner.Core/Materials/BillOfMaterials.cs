namespace FloorPlanner.Core.Materials;

/// <summary>One kind of piece in the bill: how many, and what one costs (null when the catalog has no recipe).</summary>
public sealed record BomLine(string Name, int Count, IReadOnlyDictionary<string, int>? UnitCost)
{
    public IEnumerable<KeyValuePair<string, int>> TotalCost =>
        UnitCost?.Select(c => KeyValuePair.Create(c.Key, c.Value * Count)) ?? [];
}

public sealed record BomSection(string Title, IReadOnlyList<BomLine> Lines);

public sealed record BillOfMaterials(IReadOnlyList<BomSection> Sections, double FloorAreaSquareMetres)
{
    /// <summary>Every resource needed, largest amount first.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> Totals =>
        Sections.SelectMany(s => s.Lines).SelectMany(l => l.TotalCost)
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => KeyValuePair.Create(g.Key, g.Sum(c => c.Value)))
            .OrderByDescending(c => c.Value).ThenBy(c => c.Key)
            .ToList();

    /// <summary>Lines whose cost isn't in the catalog, so the totals are too low by their amount.</summary>
    public IEnumerable<BomLine> UnknownCosts => Sections.SelectMany(s => s.Lines).Where(l => l.UnitCost is null);
}
