using System.Text.Json;
using FloorPlanner.Core.Catalog;
using FloorPlanner.Core.Geometry;

namespace FloorPlanner.Core.Tests;

public class CatalogTests
{
    [Fact]
    public void Parse_converts_metres_and_accepts_comments_and_trailing_commas()
    {
        var catalog = PieceCatalog.Parse("""
            // hand-written
            [ { "id": "bed", "name": "Bed", "category": "Living", "width": 1, "depth": 2, "image": "Bed.webp" }, ]
            """);

        var bed = Assert.Single(catalog.Items);
        Assert.Equal(new Vec2(1000, 2000), bed.Size);
        Assert.Equal(PlacementKind.Free, bed.Placement);
        Assert.Same(bed, catalog.Find("BED"));
    }

    [Fact]
    public void Parse_rejects_unknown_placement() =>
        Assert.Throws<JsonException>(() => PieceCatalog.Parse("""[ { "id": "x", "width": 1, "depth": 1, "placement": "ceiling" } ]"""));

    [Fact]
    public void Shipped_catalog_is_valid_and_its_images_exist()
    {
        var assets = FindAssetsDirectory();

        var catalog = PieceCatalog.Parse(File.ReadAllText(Path.Combine(assets, "catalog.json")));

        Assert.NotEmpty(catalog.Items);
        Assert.All(catalog.Items.Where(i => i.Image is not null && !i.Image.Contains("://")),
            item => Assert.True(File.Exists(Path.Combine(assets, item.Image!)), $"Missing image {item.Image} for {item.Id}"));
    }

    private static string FindAssetsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FloorPlanner.slnx")))
                return Path.Combine(dir.FullName, "Assets");
        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
