using System.Text.Json;
using FloorPlanner.Core.Model;
using FloorPlanner.Core.Serialization;
using Microsoft.JSInterop;

namespace FloorPlanner.Web.Editor;

/// <summary>
/// Keeps a copy of the current plan in the browser's localStorage, so a reload picks up where you left off.
/// It calls the browser's own localStorage functions directly, so no JavaScript file is needed.
/// </summary>
public sealed class AutoSave(EditorState state, IJSRuntime js)
{
    private const string Key = "floorplanner.autosave";

    // WebAssembly can call into the browser synchronously, which keeps saving out of the async render flow.
    private readonly IJSInProcessRuntime _js = (IJSInProcessRuntime)js;
    private FloorPlan? _saved;

    /// <summary>Loads the autosaved plan, if any, then saves every change from now on.</summary>
    public void Start()
    {
        try
        {
            if (_js.Invoke<string?>("localStorage.getItem", Key) is { } json)
                state.Load(PlanSerializer.Deserialize(json));
        }
        catch (JsonException)
        {
            // A save from an incompatible version: start fresh rather than fail to start.
        }
        catch (JSException)
        {
            // Storage unavailable (e.g. blocked by privacy settings): the app still works, just without autosave.
            return;
        }

        _saved = state.Plan;
        state.Changed += SaveIfChanged;
    }

    // Changed fires for every cursor move, but the committed plan only changes on edits, undo/redo and loads.
    private void SaveIfChanged()
    {
        if (ReferenceEquals(_saved, state.Plan)) return;
        _saved = state.Plan;
        try
        {
            _js.InvokeVoid("localStorage.setItem", Key, PlanSerializer.Serialize(state.Plan));
        }
        catch (JSException)
        {
            // Quota exceeded or storage blocked: keep editing; Save to file still works.
        }
    }
}
