namespace FloorPlanner.Web.Editor;

public static class AssetUrls
{
    /// <summary>Catalog images are file names in the repo's Assets folder (served at /assets) unless given as a URL.</summary>
    public static string? For(string? image) =>
        image switch
        {
            null or "" => null,
            _ when image.Contains("://") || image.StartsWith('/') => image,
            _ => $"assets/{image}",
        };
}
