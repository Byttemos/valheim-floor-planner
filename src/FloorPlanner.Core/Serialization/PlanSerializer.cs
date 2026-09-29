using System.Text.Json;
using FloorPlanner.Core.Model;

namespace FloorPlanner.Core.Serialization;

public static class PlanSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string Serialize(FloorPlan plan) => JsonSerializer.Serialize(plan, Options);

    public static FloorPlan Deserialize(string json) =>
        JsonSerializer.Deserialize<FloorPlan>(json, Options) ?? throw new JsonException("Plan file was empty.");
}
