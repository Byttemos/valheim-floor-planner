# Floor Planner

A browser-based planner for laying out a **Valheim** base before a gaming session. It is written in C# only, using
**Blazor WebAssembly**: the .NET runtime runs in the browser and draws the plan as SVG. No server is needed, and
there is no hand-written JavaScript.

## Run

```sh
dotnet watch --project src/FloorPlanner.Web   # hot reload, opens the browser
dotnet test                                   # Core unit tests
```

## Controls

| Action | Input |
| --- | --- |
| Wall tool / Select tool | `W` / `V` (or the toolbar) |
| Draw walls | click, click, click … right-click or `Esc` to finish |
| Use 1 m sections instead of 2 m | hold `Shift` while placing (walls and doors) |
| Place a piece | pick it in the side panel, then click; `Esc` or right-click to stop |
| Rotate a piece | scroll wheel while it follows the cursor; `R` for an already placed, selected piece (22.5° steps) |
| Place or drag a piece without snapping | hold `Ctrl` |
| Move / delete wall or piece | Select tool: drag / `Delete` |
| Zoom / pan | scroll wheel (except while placing a rotatable piece) / middle-drag |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` |
| Bill of materials | `B`, or the **Materials** tab on the right edge |
| Save / open | toolbar (JSON file). The plan is also autosaved in the browser |

---

## Design specification

The goal is a quick sketching tool that respects Valheim's building rules closely enough that a plan can be built
in-game as drawn. It is a planning aid, not a simulator: no physics, structural integrity or terrain.

Status: ✅ done · 🚧 next · ⏳ waiting on input

### 1. Units and grid ✅

- World units are millimetres internally; everything shown to the user is in metres.
- Background grid: a minor line every **1 m** and a major line every **2 m**, so one major square is one full wall section.
- Wall start points snap to the 1 m grid or to an existing wall end, whichever is closer.

### 2. Walls ✅

Valheim walls are made from fixed-size pieces, so wall lengths are quantised:

- **Section length:** walls snap to multiples of **2 m** (one full wall piece).
- **Half sections:** holding **Shift** switches to **1 m** increments, for the small 1 m pieces used in a pinch.
- **Minimum length:** one section (2 m, or 1 m with Shift).
- **Angles:** walls snap to **22.5° increments** (16 directions, as in Valheim's own build rotation), which also allows
  diagonals. The step is a single constant (`BuildRules.AngleStepDegrees`).
- **Closing a room:** if a wall's end lands on an existing wall end, allowing for rounding error, the two points are
  merged, so the loop is closed exactly.
- **Material count:** the status bar shows how many 2 m and 1 m pieces the plan uses in total, over all courses and
  leaving out the sections doors replace (the same count as the bill of materials). A 5 m wall counts as
  2 × 2 m + 1 × 1 m.

### 3. Rooms (flooring) ✅

- When walls form a **closed loop**, the enclosed shape is a *room*. Open wall chains get no floor.
- Rooms are filled brown (**`#826252`**, `--floor` in `app.css`) to indicate wooden flooring. The floor is drawn
  underneath walls and pieces.
- Rooms are **derived, not stored**. They are recalculated from the walls whenever the walls change, so they cannot
  get out of sync.
  - Detection finds the bounded faces of the planar graph formed by the walls (`Core/Rooms/RoomFinder.cs`).
  - A wall is split wherever another wall ends on it, so a dividing wall that meets the long walls mid-way gives
    two rooms.
  - Stub walls inside a room are ignored, and separate buildings are separate rooms.
- Each room shows its **floor area** (m²) at its centre, drawn above the pieces so furniture can't hide it.
- Possible extras: a 2 × 2 m floor tile count per room, and room names.

### 4. Piece catalog ✅ (sizes still placeholders ⏳)

- The pieces you can place are listed in **`Assets/catalog.json`**. Their icons live in the same **`Assets/`** folder.
  To add a piece, drop in its icon and add an entry. No code changes are needed.
- The folder is served to the app at `/assets/` (linked in `FloorPlanner.Web.csproj`, not copied).
- Entry format (sizes in metres; comments and trailing commas are allowed):

  ```json
  { "id": "workbench", "name": "Workbench", "category": "Crafting", "width": 2, "depth": 1, "placement": "free", "image": "Workbench.webp" }
  ```

  | Field | Meaning |
  | --- | --- |
  | `id` | Unique key, stored in saved plans. Don't rename it once used. |
  | `name` | Label shown in the planner. |
  | `category` | Group heading in the side panel. |
  | `width`, `depth` | In-game footprint in metres. For wall pieces, `width` runs along the wall. |
  | `placement` | `"free"` (anywhere on the floor) or `"wall"` (snaps into a wall, like doors). Defaults to `"free"`. |
  | `image` | Optional. A file name in `Assets/`, or a full URL. Pieces without one show a dashed box instead. |
  | `cost` | Optional. The build recipe, e.g. `{ "Wood": 10, "Stone": 4 }`. Used by the bill of materials. |

- `"placement": "structure"` entries aren't placeable and don't appear in the side panel. They give the name and cost
  of the pieces the bill of materials counts: `wood-wall`, `wood-wall-small`, `wood-floor`, `wood-floor-small` and
  `wood-roof`. The costs are from memory and need checking in-game ⏳.

- A unit test parses the shipped catalog and checks that every image file exists, so a typo fails `dotnet test`.
- Plans store only the piece's `id`. Correcting a size in the catalog therefore updates existing plans, and a piece
  missing from the catalog is shown as a red "?" box, so it can still be found and deleted.

### 5. Placing pieces ✅

- There is no top-down art, so a piece is drawn as a **dotted outline of its in-game footprint**, with its **icon in
  the middle** and its **name** underneath. The outline rotates with the piece; the icon and name stay upright.
- **Free pieces** snap to a 0.5 m grid and rotate in 22.5° steps: with the **scroll wheel** while the piece follows
  the cursor (scrolling zooms in every other situation), or with `R` once placed and selected.
- **Flush against walls:** walls are centred on the grid, so their faces sit half a wall thickness off it. After grid
  snapping, a piece whose edge is close to a wall face (12 px on screen, at most 0.4 m) is pushed flush against it.
  - This works on up to two non-parallel walls, so pieces fit neatly into corners.
  - It applies only to walls the piece has a parallel edge to (it works on diagonal walls when the piece is rotated to
    match), and the piece stays on the side of the wall it is on.
- **`Ctrl` turns snapping off** while placing or dragging. Free pieces go exactly where the cursor is, and doors slide
  freely along their wall instead of in section steps.
- **Wall pieces (doors)** can only be placed on a wall:
  - Near a wall, the ghost snaps onto it and lines up with it. Away from walls it turns red and won't place.
  - Along the wall, its edges snap to 2 m section boundaries, or 1 m with Shift, measured from the wall's start.
  - The wall is drawn with a gap where the door is, and the door's name sits beside the wall.
  - A door is stored as *wall id + offset along the wall*. Moving the wall carries the door along; deleting the wall
    deletes its doors. Dragging a door slides it along the wall, or onto another wall.
- Later: a door-swing arc to show which way it opens, and a warning when pieces overlap.

### 6. Bill of materials ✅

- A panel slides out from the right edge (the **Materials** tab, or `B`). It lists every resource the build needs,
  wood first in practice, then the pieces behind it.
- It updates live, including while dragging.
- **Walls** are counted exactly, per course:
  - **Wall height** sets how many 2 m courses the walls are (1–4).
  - A door replaces the bottom course where it sits, and the wall either side of it is counted on its own.
- **Floors** are estimated per room: as many 2 × 2 m tiles as the area allows, then 1 × 1 m tiles for the rest.
- **Roof** (optional, on by default): the roof isn't drawn, so it's approximated.
  - Valheim roof pieces cover 2 × 2 m of floor whatever their pitch, so the estimate is floor area ÷ 4 m², plus 20%
    for eaves, the ridge and cut pieces (`BomCalculator.RoofAllowance`).
- **Pieces** are counted by type, with their catalog recipe.
  - Pieces without a `cost` are listed as "cost unknown" and named in a warning, since the totals leave them out.
- Not included: beams, poles and gable ends.
- Wall height and the roof toggle are saved with the plan (`build` in the JSON) and are undoable.

### 7. Autosave ✅

- Every committed change (edit, undo/redo, New, Open, settings) is saved to the browser's `localStorage`, and
  restored on start-up, so a reload doesn't lose work.
- Undo history and the view are not kept across a reload.
- It is per browser and per machine, so use Save to share a plan or keep a copy.
- It uses the browser's own `localStorage` functions via Blazor's JS runtime (`Editor/AutoSave.cs`), so there is still
  no hand-written JavaScript.

### 8. Out of scope for now

- Multiple floors/levels, drawn roofs, stairs, terrain and height.
- Multiplayer or real-time shared editing. Plans are shared as JSON files through Save/Open.

### Open questions

- Real in-game sizes for all catalog pieces (the current ones are placeholders).
- Double door width: does it span exactly two 2 m sections?
- Should furniture be allowed to overlap walls or other furniture, or should overlaps be flagged?
- Real build costs, especially for the double door and the hearth (missing) and the 1 m wall piece.

---

## Code structure

```
src/FloorPlanner.Core/          Pure C#: no UI, fully unit-testable
  Geometry/                     Vec2, snapping, distance-to-segment …
  Model/                        FloorPlan (immutable document), Wall, Placement, BuildRules (Valheim constants)
  Rooms/                        RoomFinder: closed wall loops -> rooms with area
  Catalog/                      CatalogItem, PieceCatalog (parses Assets/catalog.json)
  Materials/                    BomCalculator: plan + rooms + catalog -> bill of materials
  Editing/                      History<T> (undo/redo), Viewport (screen <-> world)
  Serialization/                JSON save/load
src/FloorPlanner.Web/           Blazor WebAssembly UI
  Editor/EditorState.cs         The app's single source of truth; components re-render on Changed
  Editor/AutoSave.cs            Mirrors the plan to localStorage and restores it on start-up
  Editor/Tools/                 One class per editing mode (ITool): WallTool, PlaceTool, SelectTool
  Components/                   PlanCanvas (SVG + input), WallShape, PlacementShape, RoomShape, CatalogPanel, BomPanel, Toolbar, StatusBar
Assets/                         catalog.json + piece icons, served at /assets
tests/FloorPlanner.Core.Tests/  xUnit
```

### Design decisions

- **The document is immutable.** Every edit returns a new `FloorPlan`, so undo/redo is just a stack of snapshots.
- **Hit-testing happens in C#, not the DOM.** The SVG has `pointer-events: none` and all input goes to one `<div>`.
  That keeps coordinates predictable and puts the logic in testable Core code.
- **Tools are strategies.** Each mode (wall, place piece, select) is an `ITool`.
- **Game rules live in one place** (`BuildRules`), so they are easy to change once in-game measurements are in.
- **Invariant culture** is forced in `Program.cs`. Otherwise a Danish browser locale would write `0,1` into SVG attributes.
