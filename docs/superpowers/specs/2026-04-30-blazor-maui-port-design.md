# Fantasy Maps → .NET 9 MAUI Blazor Port: Design Spec

**Date:** 2026-04-30  
**Author:** D'Vinci Fradique Braga  
**Status:** Approved for implementation

---

## Context

The existing project is a single-file interactive tutorial (`index.html` + `map.js` + `names.js`, ~1,450 lines total) for procedural fantasy map generation based on Martin O'Leary's mewo2/terrain algorithm. It uses D3.js v4 for Voronoi geometry, SVG rendering, and utility math. The goal is to port it faithfully to a .NET 9 MAUI Hybrid Blazor application targeting Windows, with no custom JavaScript — pure C# for all logic and rendering.

All 8 tutorial sections, their interactive controls, and the final SVG export must be reproduced with equivalent or better performance.

---

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Hosting model | MAUI Hybrid (Blazor WebView) | Native Windows performance; no WASM overhead |
| App structure | Preserve 8-section tutorial | Faithful 1:1 port |
| SVG rendering | MarkupString (SVG as string) | Bypasses Blazor diffing; handles 16K triangles efficiently |
| Voronoi library | Triangle.NET | Lightweight, battle-tested, API matches d3.voronoi() data model |
| Target platforms | Windows | Simplest build; user's primary platform |
| JavaScript | Zero custom JS | All logic in C#; SVG export via CommunityToolkit.Maui native file dialog |

---

## Project Structure

```
FantasyMaps/
  FantasyMaps.Core/                  # Pure C# class library (no MAUI dependency)
    Mesh/
      VoronoiMesh.cs                 # Mesh struct: vxs[], adj[], tris[], edges[]
      MeshBuilder.cs                 # Triangle.NET wrapper → generateGoodMesh(n)
    Terrain/
      HeightField.cs                 # float[] subclass + operators: slope, cone, add, normalize, relax, etc.
      Erosion.cs                     # fillSinks, getFlux, getSlope, erode, doErosion, cleanCoast
      CityPlacer.cs                  # cityScore, placeCity, placeCities
      Rivers.cs                      # contour, getRivers, mergeSegments, relaxPath
      Territories.cs                 # getTerritories (PQ flood fill), getBorders
    Language/
      LanguageModel.cs               # Phoneme sets, syllable structures, orthography
      LanguageFactory.cs             # makeBasicLanguage(), makeRandomLanguage()
      NameGenerator.cs               # makeName(), getMorpheme(), makeWord(), getWord()
    Rendering/
      SvgBuilder.cs                  # Coordinate scaling (×1000), path string generation
      TerrainRenderer.cs             # visualizeVoronoi, drawPaths, visualizeSlopes, visualizeCities
      MapRenderer.cs                 # drawMap() master render function
      LabelPlacer.cs                 # Penalty-based label placement (city + region labels)
      ViridisColor.cs                # d3.interpolateViridis(t) equivalent in C#
      ColorPalette.cs                # d3.schemeCategory10 (10 hex colors)
    RenderState.cs                   # render = { h, cities, params, rivers, coasts, borders, terr }
    MapParams.cs                     # { npts, ncities, nterrs, fontsizes, extent }

  FantasyMaps.Core.Tests/            # xUnit test project
    PriorityQueueTests.cs
    HeightFieldTests.cs
    ErosionTests.cs
    CityPlacerTests.cs
    MeshBuilderTests.cs
    NameGeneratorTests.cs
    ViridisColorTests.cs

  FantasyMaps.App/                   # .NET 9 MAUI Hybrid Blazor app
    wwwroot/
      css/app.css                    # Tutorial styles (ported from index.html <style>)
    Pages/
      Tutorial.razor                 # Main scroll page containing all 8 section components
    Components/
      Section1RandomPoints.razor     # Points demo + "Generate random points" button
      Section2VoronoiMesh.razor      # Voronoi mesh visualization
      Section3Heightmap.razor        # Height map sculpting (9 buttons)
      Section4Erosion.razor          # Erosion simulation (3 buttons)
      Section5Features.razor         # Feature toggles (coast/rivers/slopes)
      Section6Cities.razor           # City placement (2 buttons)
      Section7Territories.razor      # Territories & borders
      Section8FullMap.razor          # Full map generation + SVG export
      SvgViewer.razor                # Wrapper: renders @((MarkupString)Content)
      DemoBox.razor                  # Shared tutorial section container (title, text, SVG, buttons)
    Services/
      MapStateService.cs             # Scoped service: shared mesh cache, ensureMesh()
      SvgExportService.cs            # CommunityToolkit.Maui FileSaver wrapper
    MauiProgram.cs                   # App startup, service registration
```

---

## Library Dependencies

| NuGet Package | Version | Purpose |
|---|---|---|
| `Microsoft.Maui.Controls` | .NET 9 | MAUI app framework |
| `Microsoft.AspNetCore.Components.WebView.Maui` | .NET 9 | Blazor Hybrid WebView |
| `Triangle.NET` | latest stable | Voronoi/Delaunay — replaces d3.voronoi() |
| `CommunityToolkit.Maui` | 9.x | FileSaver for native SVG export dialog |

Zero JavaScript libraries. 4 packages total.

---

## D3.js → C# Mapping

| D3 / JavaScript | C# equivalent |
|---|---|
| `d3.voronoi().extent()(pts)` | `Triangle.NET BoundedVoronoi` |
| `vor.edges` (Voronoi edge array) | Voronoi edge collection from Triangle.NET result |
| `d3.path()` | `System.Text.StringBuilder` + path formatting methods in `SvgBuilder` |
| `d3.interpolateViridis(t)` | `ViridisColor.Interpolate(t)` — 256-entry RGB lookup table interpolation |
| `d3.schemeCategory10` | `ColorPalette.Category10` — `static readonly string[]` of 10 hex colors |
| `d3.quantile(arr, q)` | Sorted array `.ElementAt((int)(q * n))` |
| `d3.min/max/mean` | LINQ `.Min()` / `.Max()` / `.Average()` |
| `d3.scan(arr, cmp)` | `.Select((v,i)=>(v,i)).MaxBy(x => x.v)` |
| Custom JS `PriorityQueue` | .NET 6+ built-in `PriorityQueue<TElement, TPriority>` |
| `Math.random()` | `Random.Shared.NextDouble()` |
| `rnorm()` (Box-Muller) | Direct C# port of Box-Muller transform |
| `Array.map(h, f)` | LINQ `.Select()` or `Array.ConvertAll()` |
| `isedge(mesh, i)` | `mesh.IsEdge(i)` — adjacency degree < 3 |
| `isnearedge(mesh, i)` | `mesh.IsNearEdge(i)` — coordinate within 5% of extent boundary |

---

## Triangle.NET Integration

**What Triangle.NET provides:**
- Delaunay triangulation of input point sets
- Dual Voronoi diagram (bounded) with circumcenter vertices and left/right polygon associations
- Each Voronoi edge: two vertex coordinates + the two input points on each side

**How `MeshBuilder.cs` wraps it:**
```csharp
// Mirrors the original makeMesh(pts, extent):
// 1. Feed pts to Triangle.NET Delaunay triangulator
// 2. Compute bounded Voronoi dual
// 3. Iterate Voronoi edges → collect unique circumcenter vertices → build adj[] + tris[]
// 4. Return VoronoiMesh { Vxs, Adj, Tris, Edges, Pts }
```

> ⚠️ Implementer note: Triangle.NET's Voronoi API class names should be verified against the installed package version — the exact public API has evolved across releases. The NuGet package is `Triangle.NET` by Christian Woltering.

The rest of Core never imports Triangle.NET — only `MeshBuilder.cs` does. This isolates the dependency to one file.

---

## Rendering Pipeline

**Per-button-click flow:**
```
User click → @onclick Blazor handler (C#)
  → Terrain/Language logic in Core (synchronous, or Task.Run for slow ops)
  → Rendering layer builds SVG string via StringBuilder
  → _svgContent = svgString; StateHasChanged()
  → SvgViewer.razor renders @((MarkupString)Content)
  → One DOM update (browser replaces SVG element innerHTML)
```

**Section 8 async loading pattern:**
```csharp
_isGenerating = true;
StateHasChanged();                              // show "Generating map..." overlay
await Task.Run(() => RunFullMapGeneration());  // Core algorithms on thread pool
_isGenerating = false;
StateHasChanged();                              // show completed map
```

**SVG coordinate system:** Terrain coordinates are in [-0.5, 0.5]. `SvgBuilder` multiplies all coordinates by 1000 for the SVG output. ViewBox is `-500 -500 1000 1000`, matching the original exactly.

**SVG export (no JavaScript):**
```csharp
// SvgExportService.cs — uses CommunityToolkit.Maui native file dialog
var bytes = Encoding.UTF8.GetBytes(svgContent);
await FileSaver.Default.SaveAsync("fantasy-map.svg", new MemoryStream(bytes), cancellationToken);
```

---

## Section → State Mapping

| Section | State variables | Buttons | Depends on |
|---|---|---|---|
| 1 — Random Points | `VoronoiMesh _mesh` | Generate random points | — |
| 2 — Voronoi Mesh | (shared mesh) | Show Voronoi mesh | `MapStateService.SharedMesh` |
| 3 — Heightmap | `float[] _hmHeight`, `bool _init` | 9 buttons (slope, cone, etc.) | SharedMesh |
| 4 — Erosion | `float[] _erHeight`, `bool _init` | Gen coast, Erode, Clean coast | Section 3 height |
| 5 — Features | `bool _showCoast`, `_showRivers`, `_showSlopes` | 3 toggles | Section 4 height |
| 6 — Cities | `RenderState _cityRender` | Add city, Reset | Section 4 height |
| 7 — Territories | `RenderState _terrRender` | Show territories | Section 6 state |
| 8 — Full Map | `RenderState _fullRender`, `bool _isGenerating` | Generate map, Download SVG | — (self-contained) |

`MapStateService` holds the shared Voronoi mesh and initializes it lazily on first use via `EnsureMesh()`, mirroring the original JS `ensureMesh()` pattern exactly.

---

## Algorithm Notes

### Viridis Color
The Viridis colormap is defined as a 256-entry RGB lookup table in the d3-scale-chromatic source (GitHub: `d3/d3-scale-chromatic`, `src/sequential-multi/viridis.js`). `ViridisColor.Interpolate(double t)` linearly interpolates that table — no external library needed.

### Label Placement
`LabelPlacer.cs` is the most complex rendering component. It ports `drawLabels()` exactly:
- **City labels:** 4 candidate positions per city (right, left, above, below). Penalty function scores each by: overlap with coast/river paths, overlap with other labels, distance out of bounds. Minimum-penalty position wins.
- **Region labels:** Exhaustive search across all mesh vertices for the best position near the territory centroid, avoiding label overlaps and path collisions.

### PriorityQueue (Territories)
.NET 6+ ships `PriorityQueue<TElement, TPriority>` (min-heap) which directly replaces the custom JS binary heap. No additional code needed.

### Random Seeds
The original app uses `Math.random()` (unseeded). The C# port uses `Random.Shared` (unseeded) to match this behavior. If reproducible maps are needed later, `Random` instances can be threaded through the generation functions as a parameter.

---

## CSS Porting

All styles from `index.html`'s `<style>` block port to `wwwroot/css/app.css`. SVG element styles (`.coast`, `.river`, `.border`, `.slope`, `.city`, `.region`) are applied as inline `style="..."` attributes in the SVG string output by `TerrainRenderer`, matching the original's approach and ensuring the exported SVG is self-contained with inline styles.

---

## Testing Plan

**Unit tests (`FantasyMaps.Core.Tests` — xUnit):**
- `PriorityQueueTests` — enqueue/dequeue ordering, tie-breaking (using .NET built-in)
- `HeightFieldTests` — `Slope()`, `Cone()`, `Normalize()`, `Add()` produce expected value ranges
- `ErosionTests` — `FillSinks()` eliminates interior minima; `GetFlux()` ≥ 0; `Erode()` reduces mean height
- `CityPlacerTests` — cities land on land, not near edge, spacing maintained
- `MeshBuilderTests` — vertex count reasonable for n; all vertices have ≥ 1 neighbor
- `NameGeneratorTests` — names within [5, 12] chars; no name is a substring of another
- `ViridisColorTests` — `t=0` → approx `#440154`; `t=1` → approx `#FDE725`

**Visual verification (manual, side-by-side with original `index.html`):**
- All 8 sections produce visually equivalent output
- Section 8 SVG export opens correctly in browser and SVG viewers

**Performance targets (Windows, .NET 9, Release build):**
- `GenerateGoodMesh(4096)` < 500ms
- `GenerateGoodMesh(16384)` < 3s
- SVG string generation for 16K triangles < 200ms
- Full Section 8 pipeline < 5s

---

## Build Order

1. Create solution + `FantasyMaps.Core` class library + `FantasyMaps.Core.Tests` xUnit project + `FantasyMaps.App` MAUI Hybrid Blazor project
2. Add NuGet packages to each project
3. Port Core algorithms bottom-up:
   - `Mesh/` (VoronoiMesh, MeshBuilder with Triangle.NET)
   - `Terrain/` (HeightField → Erosion → CityPlacer → Rivers → Territories)
   - `Language/` (LanguageModel → LanguageFactory → NameGenerator)
   - `Rendering/` (ViridisColor → ColorPalette → SvgBuilder → TerrainRenderer → LabelPlacer → MapRenderer)
   - Root types: `RenderState`, `MapParams`
4. Write unit tests as each module is ported
5. Create MAUI App, register `MapStateService` and `SvgExportService` in `MauiProgram.cs`
6. Implement Blazor components section by section (1 through 8)
7. Port CSS to `wwwroot/css/app.css`
8. Implement `SvgExportService` for Section 8 SVG download
9. Manual visual verification against original
10. Performance benchmark; optimize SVG string generation if needed
