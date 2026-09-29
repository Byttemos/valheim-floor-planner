using System.Globalization;
using System.Text.Json;
using FloorPlanner.Core.Catalog;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FloorPlanner.Web;
using FloorPlanner.Web.Editor;

// SVG attributes need '.' as the decimal separator, whatever the browser's locale is.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In WebAssembly there is one user per browser tab, so a singleton is fine.
builder.Services.AddSingleton<EditorState>();
builder.Services.AddSingleton<AutoSave>();

var host = builder.Build();
await LoadCatalogAsync(host.Services.GetRequiredService<EditorState>(), builder.HostEnvironment.BaseAddress);
host.Services.GetRequiredService<AutoSave>().Start();
await host.RunAsync();

static async Task LoadCatalogAsync(EditorState state, string baseAddress)
{
    try
    {
        using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
        state.Catalog = PieceCatalog.Parse(await http.GetStringAsync("assets/catalog.json"));
    }
    catch (Exception ex) when (ex is HttpRequestException or JsonException)
    {
        state.CatalogError = $"Could not load catalog.json: {ex.Message}";
    }
}
