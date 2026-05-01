# Fantasy Maps — MAUI Hybrid Blazor App Implementation Plan (Plan 2 of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Prerequisite:** Plan 1 (Core library) must be complete and all tests passing before starting this plan.

**Goal:** Build a .NET 9 MAUI Hybrid Blazor Windows app that hosts all 8 tutorial sections using `FantasyMaps.Core` for computation and SVG-as-MarkupString rendering in a Blazor WebView.

**Architecture:** `FantasyMaps.App` is a MAUI Hybrid project that references `FantasyMaps.Core`. Each tutorial section is a Blazor component that calls Core algorithms, builds an SVG string, and injects it as `@((MarkupString)_svg)`. Shared mesh state is held in a scoped `MapStateService`. SVG export uses `CommunityToolkit.Maui`'s `FileSaver`.

**Tech Stack:** .NET 9, MAUI, Blazor Hybrid (WebView), CommunityToolkit.Maui, FantasyMaps.Core

---

## Data Flow Reminder

```
User clicks button
  → Blazor @onclick handler (C#)
  → Calls Core algorithm (HeightPrimitives / Erosion / etc.)
  → Calls TerrainRenderer / MapRenderer → returns SVG string
  → _svg = svgString; StateHasChanged()
  → SvgViewer.razor renders @((MarkupString)_svg)
  → Browser updates one SVG element
```

For Section 8 (slow): use `await Task.Run(...)` to run generation off the UI thread, with a loading overlay.

---

## File Map

```
FantasyMapsDotNet/FantasyMaps.App/
  Platforms/Windows/                # MAUI Windows platform head (auto-generated)
  wwwroot/
    css/
      app.css                       # Tutorial styles (ported from index.html)
  Pages/
    Tutorial.razor                  # Main page: scroll page with all 8 section components
  Components/
    SvgViewer.razor                 # Injects MarkupString SVG into a div
    DemoBox.razor                   # Shared section container (title, content, SVG, buttons)
    Section1RandomPoints.razor      # Section 1: random points generation
    Section2VoronoiMesh.razor       # Section 2: Voronoi mesh visualization
    Section3Heightmap.razor         # Section 3: height map sculpting (9 buttons)
    Section4Erosion.razor           # Section 4: erosion simulation
    Section5Features.razor          # Section 5: feature toggle (coast/rivers/slopes)
    Section6Cities.razor            # Section 6: city placement
    Section7Territories.razor       # Section 7: territories and borders
    Section8FullMap.razor           # Section 8: full map + SVG export
  Services/
    MapStateService.cs              # Singleton: shared VoronoiMesh (ensureMesh)
    SvgExportService.cs             # CommunityToolkit.Maui FileSaver wrapper
  MauiProgram.cs                    # App startup, DI registration
  App.xaml / App.xaml.cs            # MAUI application shell
  MainPage.xaml / MainPage.xaml.cs  # MAUI page hosting the BlazorWebView
```

---

## Task 1: MAUI Hybrid Project Setup

**Files:**
- Create: `FantasyMaps.App/FantasyMaps.App.csproj`
- Modify: `FantasyMaps.sln`

- [ ] **Step 1.1: Create the MAUI Hybrid Blazor project**

```bash
cd "d:/Projetos/Fantasy Maps/FantasyMapsDotNet"
dotnet new maui-blazor -n FantasyMaps.App -f net9.0 -o FantasyMaps.App
dotnet sln add FantasyMaps.App/FantasyMaps.App.csproj
```

> If `maui-blazor` template is not found, install it: `dotnet workload install maui`

- [ ] **Step 1.2: Add project reference and packages**

```bash
cd FantasyMaps.App
dotnet add reference ../FantasyMaps.Core/FantasyMaps.Core.csproj
dotnet add package CommunityToolkit.Maui
```

- [ ] **Step 1.3: Register CommunityToolkit.Maui in MauiProgram.cs**

Open `FantasyMaps.App/MauiProgram.cs`. It should contain the MAUI builder setup. Add:

```csharp
using CommunityToolkit.Maui;
using FantasyMaps.App.Services;
using Microsoft.Extensions.Logging;

namespace FantasyMaps.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()  // ← add this
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<MapStateService>();
        builder.Services.AddSingleton<SvgExportService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

- [ ] **Step 1.4: Build to verify project compiles**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```
Expected: `Build succeeded. 0 Error(s)`

> If you get a Windows SDK error, ensure the MAUI Windows workload is installed: `dotnet workload install maui-windows`

- [ ] **Step 1.5: Commit**

```bash
cd "d:/Projetos/Fantasy Maps"
git add FantasyMapsDotNet/FantasyMaps.App/
git commit -m "feat: initialize FantasyMaps.App MAUI Hybrid Blazor project"
```

---

## Task 2: Services — MapStateService and SvgExportService

**Files:**
- Create: `FantasyMaps.App/Services/MapStateService.cs`
- Create: `FantasyMaps.App/Services/SvgExportService.cs`

- [ ] **Step 2.1: Create the Services directory**

```bash
mkdir FantasyMapsDotNet/FantasyMaps.App/Services
```

- [ ] **Step 2.2: Write `MapStateService.cs`**

```csharp
using FantasyMaps.Core.Mesh;

namespace FantasyMaps.App.Services;

// Singleton service holding shared Voronoi mesh (lazy-initialized on first use).
// Mirrors the original ensureMesh() pattern from map.js.
public class MapStateService
{
    private VoronoiMesh? _sharedMesh;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public VoronoiMesh SharedMesh => _sharedMesh
        ?? throw new InvalidOperationException("Call EnsureMesh() first.");

    // Initializes the shared mesh if not already done.
    // Safe to call multiple times — only generates once.
    public async Task<VoronoiMesh> EnsureMeshAsync(int n = 4096)
    {
        if (_sharedMesh != null) return _sharedMesh;
        await _lock.WaitAsync();
        try
        {
            if (_sharedMesh == null)
                _sharedMesh = await Task.Run(() => MeshBuilder.GenerateGoodMesh(n));
        }
        finally { _lock.Release(); }
        return _sharedMesh;
    }

    // Reset the mesh (e.g., if user wants a fresh mesh).
    public void Reset() => _sharedMesh = null;
}
```

- [ ] **Step 2.3: Write `SvgExportService.cs`**

```csharp
using CommunityToolkit.Maui.Storage;
using System.Text;

namespace FantasyMaps.App.Services;

public class SvgExportService
{
    // Triggers a native Save File dialog and writes the SVG string to disk.
    public async Task SaveSvgAsync(string svgContent, string defaultFileName = "fantasy-map.svg",
        CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(svgContent);
        using var stream = new MemoryStream(bytes);
        var result = await FileSaver.Default.SaveAsync(defaultFileName, stream, ct);
        if (!result.IsSuccessful)
            throw new IOException($"SVG save failed: {result.Exception?.Message}");
    }
}
```

- [ ] **Step 2.4: Build**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```
Expected: `0 Error(s)`

- [ ] **Step 2.5: Commit**

```bash
git add FantasyMapsDotNet/FantasyMaps.App/Services/
git commit -m "feat: add MapStateService (shared mesh) and SvgExportService (native file save)"
```

---

## Task 3: CSS — Port Tutorial Styles

**Files:**
- Modify: `FantasyMaps.App/wwwroot/css/app.css`

- [ ] **Step 3.1: Replace `app.css` with ported tutorial styles**

Open `FantasyMaps.App/wwwroot/css/app.css` and replace its contents with:

```css
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: Georgia, serif; line-height: 1.7; color: #333; background: #fafafa; }
a { color: #2266aa; }

.container { max-width: 800px; margin: 0 auto; padding: 20px; }
h1 { font-size: 2em; margin-bottom: 0.5em; }
h2 { font-size: 1.4em; margin: 1.5em 0 0.5em; }
p { margin-bottom: 1em; }

.demo {
    background: #fff;
    border: 1px solid #ddd;
    border-radius: 4px;
    padding: 15px;
    margin: 1.5em 0;
}
.demo svg { display: block; max-width: 100%; }

button {
    background: #f5f5f5;
    border: 1px solid #ccc;
    border-radius: 3px;
    cursor: pointer;
    font-size: 0.9em;
    padding: 6px 14px;
    margin: 4px 2px;
}
button:hover { background: #e0e0e0; }
button:active { background: #ccc; }
button.primary { background: #2266aa; color: #fff; border-color: #1a4f88; }
button.primary:hover { background: #1a4f88; }

.loading-overlay {
    position: fixed; inset: 0;
    background: rgba(255,255,255,0.85);
    display: flex; align-items: center; justify-content: center;
    font-size: 1.4em; color: #555;
    z-index: 100;
}

/* SVG element styles — applied inline in SVG output */
path.coast { stroke: #000; stroke-width: 3; stroke-linecap: round; stroke-linejoin: round; fill: none; }
path.river { stroke: #36a; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; fill: none; }
path.border { stroke: #a33; stroke-width: 2.5; stroke-dasharray: 6,6; stroke-linecap: round; fill: none; }
line.slope { stroke: #797; stroke-width: 1; stroke-linecap: round; }
text.city { font-family: 'Palatino Linotype', Palatino, Georgia, serif; font-size: 15px; fill: #000;
    stroke: white; stroke-width: 3; paint-order: stroke; }
text.region { font-family: 'Palatino Linotype', Palatino, Georgia, serif; font-size: 13px;
    fill: #8a4; font-style: italic; stroke: white; stroke-width: 2; paint-order: stroke;
    text-anchor: middle; }
circle.city { fill: white; stroke: black; stroke-width: 5; stroke-linecap: round; }
```

- [ ] **Step 3.2: Verify the CSS file was saved**

```bash
cat "FantasyMapsDotNet/FantasyMaps.App/wwwroot/css/app.css" | head -5
```
Expected: Shows the first few CSS lines.

- [ ] **Step 3.3: Commit**

```bash
git add FantasyMapsDotNet/FantasyMaps.App/wwwroot/
git commit -m "feat: port tutorial CSS styles to app.css"
```

---

## Task 4: Shared Components — SvgViewer and DemoBox

**Files:**
- Create: `FantasyMaps.App/Components/SvgViewer.razor`
- Create: `FantasyMaps.App/Components/DemoBox.razor`

- [ ] **Step 4.1: Write `SvgViewer.razor`**

This component receives an SVG string and injects it as raw HTML markup. This bypasses Blazor's component diffing for the SVG content — critical for 16K-element SVGs.

```razor
@* SvgViewer.razor *@
<div class="svg-container">
    @((MarkupString)Content)
</div>

@code {
    [Parameter, EditorRequired]
    public string Content { get; set; } = "<svg viewBox=\"-500 -500 1000 1000\" style=\"width:800px;height:800px\"></svg>";
}
```

- [ ] **Step 4.2: Write `DemoBox.razor`**

```razor
@* DemoBox.razor — shared container for each tutorial section *@
<div class="demo">
    @ChildContent
</div>

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 4.3: Build**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```
Expected: `0 Error(s)`

- [ ] **Step 4.4: Commit**

```bash
git add FantasyMapsDotNet/FantasyMaps.App/Components/
git commit -m "feat: add SvgViewer and DemoBox shared Blazor components"
```

---

## Task 5: Section 1 — Random Points

**Files:**
- Create: `FantasyMaps.App/Components/Section1RandomPoints.razor`

- [ ] **Step 5.1: Write `Section1RandomPoints.razor`**

```razor
@using FantasyMaps.Core.Mesh
@using FantasyMaps.Core.Rendering
@inject Services.MapStateService State

<DemoBox>
    <h2>1. Placing Random Points</h2>
    <p>
        We start by placing random points across the map area, then apply
        Lloyd relaxation to distribute them more evenly.
    </p>
    <SvgViewer Content="@_svg" />
    <button @onclick="Generate">Generate random points</button>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");

    private async Task Generate()
    {
        var mesh = await State.EnsureMeshAsync(4096);
        var sb = new System.Text.StringBuilder();
        double r = Math.Sqrt(1.0 / mesh.Pts.Length) * 300;
        foreach (var pt in mesh.Pts)
            sb.AppendLine(SvgBuilder.Circle(pt[0], pt[1], r, "pt",
                "fill:#333;opacity:0.7"));
        _svg = SvgBuilder.WrapSvg(sb.ToString());
    }
}
```

- [ ] **Step 5.2: Build**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```

- [ ] **Step 5.3: Commit**

```bash
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section1RandomPoints.razor
git commit -m "feat: add Section 1 (random points)"
```

---

## Task 6: Section 2 — Voronoi Mesh

**Files:**
- Create: `FantasyMaps.App/Components/Section2VoronoiMesh.razor`

- [ ] **Step 6.1: Write `Section2VoronoiMesh.razor`**

```razor
@using FantasyMaps.Core.Mesh
@using FantasyMaps.Core.Rendering
@inject Services.MapStateService State

<DemoBox>
    <h2>2. Building the Mesh</h2>
    <p>
        Each point becomes the center of a Voronoi polygon. We also compute the
        dual Delaunay triangulation, which gives us the mesh we'll use for everything else.
    </p>
    <SvgViewer Content="@_svg" />
    <button @onclick="ShowMesh">Show Voronoi mesh</button>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");

    private async Task ShowMesh()
    {
        var mesh = await State.EnsureMeshAsync(4096);
        var sb = new System.Text.StringBuilder();

        // Draw Voronoi edges
        foreach (var (v0, v1, _, _) in mesh.Edges)
        {
            var p0 = mesh.Vxs[v0]; var p1 = mesh.Vxs[v1];
            sb.AppendLine(SvgBuilder.Line(p0[0], p0[1], p1[0], p1[1], "edge",
                "stroke:#aaa;stroke-width:0.5;opacity:0.7"));
        }

        // Draw input points in red
        double r = Math.Sqrt(1.0 / mesh.Pts.Length) * 200;
        foreach (var pt in mesh.Pts)
            sb.AppendLine(SvgBuilder.Circle(pt[0], pt[1], r, "pt",
                "fill:#c33;opacity:0.8"));

        _svg = SvgBuilder.WrapSvg(sb.ToString());
    }
}
```

- [ ] **Step 6.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section2VoronoiMesh.razor
git commit -m "feat: add Section 2 (Voronoi mesh visualization)"
```

---

## Task 7: Section 3 — Height Map Sculpting

**Files:**
- Create: `FantasyMaps.App/Components/Section3Heightmap.razor`

- [ ] **Step 7.1: Write `Section3Heightmap.razor`**

This section has 9 buttons: slope, cone, inverted cone, blobs, normalize, round, relax, set sea level, reset.

```razor
@using FantasyMaps.Core.Mesh
@using FantasyMaps.Core.Rendering
@using FantasyMaps.Core.Terrain
@inject Services.MapStateService State

<DemoBox>
    <h2>3. Sculpting the Height Map</h2>
    <p>
        Height maps are arrays of values, one per mesh vertex. We build terrain
        by combining primitive operations.
    </p>
    <SvgViewer Content="@_svg" />
    <div>
        <button @onclick="AddSlope">Add random slope</button>
        <button @onclick="AddCone">Add cone</button>
        <button @onclick="AddInvCone">Add inverted cone</button>
        <button @onclick="AddBlobs">Add five blobs</button>
        <button @onclick="Normalize">Normalize</button>
        <button @onclick="Round">Round</button>
        <button @onclick="Relax">Relax</button>
        <button @onclick="SetSea">Set sea level</button>
        <button @onclick="Reset">Reset</button>
    </div>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");
    private HeightField? _h;

    private async Task EnsureH()
    {
        if (_h != null) return;
        var mesh = await State.EnsureMeshAsync(4096);
        _h = HeightPrimitives.Zero(mesh);
    }

    private void Redraw()
    {
        if (_h == null) return;
        _svg = SvgBuilder.WrapSvg(TerrainRenderer.VisualizeVoronoi(_h)
            + TerrainRenderer.DrawPaths(Rivers.Contour(_h, 0f), "coast",
                "stroke:#000;stroke-width:2;fill:none"));
    }

    private async Task AddSlope() { await EnsureH();
        _h = HeightPrimitives.Add(_h!, HeightPrimitives.Slope(_h!.Mesh,
            [Rand.Uniform(-1, 1) * 4, Rand.Uniform(-1, 1) * 4])); Redraw(); }

    private async Task AddCone() { await EnsureH();
        _h = HeightPrimitives.Add(_h!, HeightPrimitives.Cone(_h!.Mesh, -1)); Redraw(); }

    private async Task AddInvCone() { await EnsureH();
        _h = HeightPrimitives.Add(_h!, HeightPrimitives.Cone(_h!.Mesh, 1)); Redraw(); }

    private async Task AddBlobs() { await EnsureH();
        _h = HeightPrimitives.Add(_h!, HeightPrimitives.Mountains(_h!.Mesh, 5)); Redraw(); }

    private async Task Normalize() { await EnsureH(); _h = HeightPrimitives.Normalize(_h!); Redraw(); }
    private async Task Round() { await EnsureH(); _h = HeightPrimitives.Peaky(_h!); Redraw(); }
    private async Task Relax() { await EnsureH(); _h = HeightPrimitives.Relax(_h!); Redraw(); }

    private async Task SetSea() { await EnsureH();
        _h = Erosion.SetSeaLevel(_h!, 0.5); Redraw(); }

    private async Task Reset() { await EnsureH();
        _h = HeightPrimitives.Zero(_h!.Mesh); Redraw(); }
}
```

- [ ] **Step 7.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section3Heightmap.razor
git commit -m "feat: add Section 3 (height map sculpting with 9 buttons)"
```

---

## Task 8: Section 4 — Erosion

**Files:**
- Create: `FantasyMaps.App/Components/Section4Erosion.razor`

- [ ] **Step 8.1: Write `Section4Erosion.razor`**

```razor
@using FantasyMaps.Core.Rendering
@using FantasyMaps.Core.Terrain
@inject Services.MapStateService State

<DemoBox>
    <h2>4. Erosion</h2>
    <p>
        Water simulation: flux accumulates downhill, eroding the terrain.
        Coastline cleaning removes isolated land and water patches.
    </p>
    <SvgViewer Content="@_svg" />
    <div>
        <button @onclick="GenCoast">Generate coastline</button>
        <button @onclick="Erode">Erode</button>
        <button @onclick="CleanCoast">Clean coastline</button>
    </div>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");
    private HeightField? _h;

    private async Task EnsureH()
    {
        if (_h != null) return;
        var mesh = await State.EnsureMeshAsync(4096);
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Add(h, HeightPrimitives.Slope(mesh,
            [Rand.Uniform(-1,1)*4, Rand.Uniform(-1,1)*4]));
        h = HeightPrimitives.Peaky(h);
        h = Erosion.SetSeaLevel(h, 0.5);
        _h = h;
    }

    private void Redraw()
    {
        if (_h == null) return;
        _svg = SvgBuilder.WrapSvg(
            TerrainRenderer.VisualizeVoronoi(_h)
            + TerrainRenderer.DrawPaths(Rivers.Contour(_h, 0f), "coast",
                "stroke:#000;stroke-width:2;fill:none"));
    }

    private async Task GenCoast() { await EnsureH(); Redraw(); }

    private async Task Erode() {
        await EnsureH();
        _h = await Task.Run(() => Erosion.DoErosion(_h!, 0.05f, 5));
        _h = Erosion.SetSeaLevel(_h, 0.5);
        Redraw();
    }

    private async Task CleanCoast() {
        await EnsureH();
        _h = Erosion.CleanCoast(_h!, 3);
        Redraw();
    }
}
```

- [ ] **Step 8.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section4Erosion.razor
git commit -m "feat: add Section 4 (erosion simulation)"
```

---

## Task 9: Section 5 — Feature Toggles

**Files:**
- Create: `FantasyMaps.App/Components/Section5Features.razor`

- [ ] **Step 9.1: Write `Section5Features.razor`**

```razor
@using FantasyMaps.Core.Rendering
@using FantasyMaps.Core.Terrain
@inject Services.MapStateService State

<DemoBox>
    <h2>5. Rendering Features</h2>
    <p>Toggle individual map layers to see how each feature is built.</p>
    <SvgViewer Content="@_svg" />
    <div>
        <button @onclick="ToggleCoast">@(_showCoast ? "Hide" : "Show") coastline</button>
        <button @onclick="ToggleRivers">@(_showRivers ? "Hide" : "Show") rivers</button>
        <button @onclick="ToggleSlopes">@(_showSlopes ? "Hide" : "Show") slopes</button>
    </div>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");
    private HeightField? _h;
    private bool _showCoast = true, _showRivers = true, _showSlopes = true;
    private List<double[][]> _coasts = [], _rivers = [];

    private async Task EnsureH()
    {
        if (_h != null) return;
        var mesh = await State.EnsureMeshAsync(4096);
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Add(h, HeightPrimitives.Slope(mesh, [Rand.Uniform(-1,1)*4, Rand.Uniform(-1,1)*4]));
        h = HeightPrimitives.Peaky(h);
        h = await Task.Run(() => Erosion.DoErosion(h, 0.05f, 5));
        h = Erosion.SetSeaLevel(h, 0.5);
        h = Erosion.CleanCoast(h, 3);
        _h = h;
        _coasts = Rivers.Contour(h, 0f);
        _rivers = Rivers.GetRivers(h, 0.01f);
        Redraw();
    }

    private void Redraw()
    {
        if (_h == null) return;
        var sb = new System.Text.StringBuilder();
        sb.Append(TerrainRenderer.VisualizeVoronoi(_h));
        if (_showCoast) sb.Append(TerrainRenderer.DrawPaths(_coasts, "coast",
            "stroke:#000;stroke-width:3;fill:none"));
        if (_showRivers) sb.Append(TerrainRenderer.DrawPaths(_rivers, "river",
            "stroke:#36a;stroke-width:2;fill:none"));
        if (_showSlopes) sb.Append(TerrainRenderer.VisualizeSlopes(_h));
        _svg = SvgBuilder.WrapSvg(sb.ToString());
    }

    private async Task ToggleCoast() { await EnsureH(); _showCoast = !_showCoast; Redraw(); }
    private async Task ToggleRivers() { await EnsureH(); _showRivers = !_showRivers; Redraw(); }
    private async Task ToggleSlopes() { await EnsureH(); _showSlopes = !_showSlopes; Redraw(); }
}
```

- [ ] **Step 9.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section5Features.razor
git commit -m "feat: add Section 5 (feature toggle: coast/rivers/slopes)"
```

---

## Task 10: Section 6 — City Placement

**Files:**
- Create: `FantasyMaps.App/Components/Section6Cities.razor`

- [ ] **Step 10.1: Write `Section6Cities.razor`**

```razor
@using FantasyMaps.Core
@using FantasyMaps.Core.Rendering
@using FantasyMaps.Core.Terrain
@inject Services.MapStateService State

<DemoBox>
    <h2>6. Placing Cities</h2>
    <p>
        Cities are scored by water flux (trade potential), distance from the map
        edge, and distance from existing cities. Each click places the highest-scoring city.
    </p>
    <SvgViewer Content="@_svg" />
    <div>
        <button @onclick="AddCity">Add new city</button>
        <button @onclick="ResetCities">Reset cities</button>
    </div>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");
    private RenderState? _render;

    private async Task EnsureRender()
    {
        if (_render != null) return;
        var mesh = await State.EnsureMeshAsync(4096);
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Add(h, HeightPrimitives.Slope(mesh, [Rand.Uniform(-1,1)*4, Rand.Uniform(-1,1)*4]));
        h = HeightPrimitives.Peaky(h);
        h = await Task.Run(() => Erosion.DoErosion(h, 0.05f, 5));
        h = Erosion.SetSeaLevel(h, 0.5);
        h = Erosion.CleanCoast(h, 3);
        _render = new RenderState { H = h, Params = new MapParams { Nterrs = 5 } };
    }

    private void Redraw()
    {
        if (_render == null) return;
        var h = _render.H;
        var sb = new System.Text.StringBuilder();
        sb.Append(TerrainRenderer.VisualizeVoronoi(h));
        sb.Append(TerrainRenderer.DrawPaths(Rivers.Contour(h, 0f), "coast",
            "stroke:#000;stroke-width:3;fill:none"));
        sb.Append(TerrainRenderer.DrawPaths(Rivers.GetRivers(h, 0.01f), "river",
            "stroke:#36a;stroke-width:2;fill:none"));
        sb.Append(TerrainRenderer.VisualizeCities(_render));
        _svg = SvgBuilder.WrapSvg(sb.ToString());
    }

    private async Task AddCity() { await EnsureRender(); CityPlacer.PlaceCity(_render!); Redraw(); }

    private async Task ResetCities()
    {
        await EnsureRender();
        _render!.Cities.Clear();
        Redraw();
    }
}
```

- [ ] **Step 10.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section6Cities.razor
git commit -m "feat: add Section 6 (city placement)"
```

---

## Task 11: Section 7 — Territories and Borders

**Files:**
- Create: `FantasyMaps.App/Components/Section7Territories.razor`

- [ ] **Step 11.1: Write `Section7Territories.razor`**

```razor
@using FantasyMaps.Core
@using FantasyMaps.Core.Rendering
@using FantasyMaps.Core.Terrain
@inject Services.MapStateService State

<DemoBox>
    <h2>7. Territories and Borders</h2>
    <p>
        Territory expansion uses a priority queue (Dijkstra-style flood fill) from
        major cities, weighted by terrain difficulty and water proximity.
    </p>
    <SvgViewer Content="@_svg" />
    <button @onclick="ShowTerritories">Show territories &amp; borders</button>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");
    private RenderState? _render;

    private async Task ShowTerritories()
    {
        if (_render == null)
        {
            var mesh = await State.EnsureMeshAsync(4096);
            var h = HeightPrimitives.Mountains(mesh, 5);
            h = HeightPrimitives.Add(h, HeightPrimitives.Slope(mesh, [Rand.Uniform(-1,1)*4, Rand.Uniform(-1,1)*4]));
            h = HeightPrimitives.Peaky(h);
            h = await Task.Run(() => Erosion.DoErosion(h, 0.05f, 5));
            h = Erosion.SetSeaLevel(h, 0.5);
            h = Erosion.CleanCoast(h, 3);
            _render = new RenderState { H = h, Params = new MapParams { Ncities = 15, Nterrs = 5 } };
            CityPlacer.PlaceCities(_render);
        }

        _render.Terr = await Task.Run(() => Territories.GetTerritories(_render));
        _render.Borders = Territories.GetBorders(_render);

        var sb = new System.Text.StringBuilder();
        // Territory colored fills
        var h2 = _render.H;
        for (int i = 0; i < h2.Mesh.Vxs.Length; i++)
        {
            var pts = h2.Mesh.Tris[i];
            if (pts == null || pts.Length < 3 || h2[i] <= 0f) continue;
            int owner = _render.Terr.Length > i ? _render.Terr[i] : -1;
            if (owner < 0) continue;
            int ci = _render.Cities.IndexOf(owner);
            string color = ColorPalette.Category10[ci % ColorPalette.Category10.Length];
            sb.AppendLine($"<path d=\"{SvgBuilder.MakePath(pts)}\" fill=\"{color}\" fill-opacity=\"0.5\" />");
        }
        sb.Append(TerrainRenderer.DrawPaths(Rivers.Contour(h2, 0f), "coast", "stroke:#000;stroke-width:3;fill:none"));
        sb.Append(TerrainRenderer.DrawPaths(_render.Borders, "border",
            "stroke:#a33;stroke-width:2.5;stroke-dasharray:6,6;fill:none"));
        sb.Append(TerrainRenderer.VisualizeCities(_render));
        _svg = SvgBuilder.WrapSvg(sb.ToString());
    }
}
```

- [ ] **Step 11.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section7Territories.razor
git commit -m "feat: add Section 7 (territories and borders)"
```

---

## Task 12: Section 8 — Full Map + SVG Export

**Files:**
- Create: `FantasyMaps.App/Components/Section8FullMap.razor`

- [ ] **Step 12.1: Write `Section8FullMap.razor`**

This section generates a high-resolution map (16K points) with labels and exports to SVG. Uses async generation with a loading overlay.

```razor
@using FantasyMaps.Core
@using FantasyMaps.Core.Language
@using FantasyMaps.Core.Mesh
@using FantasyMaps.Core.Rendering
@using FantasyMaps.Core.Terrain
@inject Services.SvgExportService ExportService

<DemoBox>
    <h2>8. The Complete Map</h2>
    <p>
        Full pipeline: 16,384 points, 15 cities, 5 territories, procedural language
        for city and region names, penalty-based label placement.
    </p>
    @if (_isGenerating)
    {
        <div class="loading-overlay">Generating map…</div>
    }
    <SvgViewer Content="@_svg" />
    <div>
        <button class="primary" @onclick="GenerateMap" disabled="@_isGenerating">Generate map</button>
        <button @onclick="DownloadSvg" disabled="@(_mapSvgContent == null || _isGenerating)">Download SVG</button>
    </div>
</DemoBox>

@code {
    private string _svg = SvgBuilder.WrapSvg("", style: "width:800px;height:800px");
    private string? _mapSvgContent;
    private bool _isGenerating;

    private async Task GenerateMap()
    {
        _isGenerating = true;
        StateHasChanged();

        try
        {
            var (@params, svgContent) = await Task.Run(() =>
            {
                var p = new MapParams { Npts = 16384, Ncities = 15, Nterrs = 5,
                    Fontsizes = [25, 18, 15] };
                var mesh = MeshBuilder.GenerateGoodMesh(p.Npts);
                var render = MapRenderer.GenerateFullMap(p, mesh);
                var lang = LanguageFactory.MakeRandomLanguage();
                string svg = MapRenderer.DrawMap(render, lang);
                return (p, svg);
            });

            _mapSvgContent = svgContent;
            _svg = svgContent;
        }
        finally
        {
            _isGenerating = false;
        }
    }

    private async Task DownloadSvg()
    {
        if (_mapSvgContent == null) return;
        try { await ExportService.SaveSvgAsync(_mapSvgContent); }
        catch (Exception ex) { Console.WriteLine($"Export failed: {ex.Message}"); }
    }
}
```

- [ ] **Step 12.2: Build and commit**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
git add FantasyMapsDotNet/FantasyMaps.App/Components/Section8FullMap.razor
git commit -m "feat: add Section 8 (full map generation + SVG export)"
```

---

## Task 13: Tutorial.razor — Assemble All Sections

**Files:**
- Modify: `FantasyMaps.App/Pages/Tutorial.razor` (or create if not auto-generated)

- [ ] **Step 13.1: Write `Tutorial.razor`**

```razor
@page "/"

<div class="container">
    <h1>Generating Fantasy Maps</h1>
    <p>
        A step-by-step tutorial on procedural fantasy map generation,
        based on Martin O'Leary's <em>mewo2/terrain</em> algorithm.
        Ported to C# / .NET 9 MAUI Blazor.
    </p>

    <Section1RandomPoints />
    <Section2VoronoiMesh />
    <Section3Heightmap />
    <Section4Erosion />
    <Section5Features />
    <Section6Cities />
    <Section7Territories />
    <Section8FullMap />
</div>
```

- [ ] **Step 13.2: Ensure components are registered**

Open `FantasyMaps.App/_Imports.razor` (auto-generated). Add the component namespace imports:

```razor
@using FantasyMaps.App.Components
@using FantasyMaps.App.Services
```

- [ ] **Step 13.3: Verify MainPage.xaml hosts the BlazorWebView**

Open `FantasyMaps.App/MainPage.xaml`. It should contain a `BlazorWebView` pointing at `Tutorial.razor`:

```xml
<BlazorWebView HostPage="wwwroot/index.html">
    <BlazorWebView.RootComponents>
        <RootComponent Selector="#app" ComponentType="{x:Type local:Pages.Tutorial}" />
    </BlazorWebView.RootComponents>
</BlazorWebView>
```

If this differs, adjust to match your template's generated structure — the key is that `Tutorial.razor` is the root component.

- [ ] **Step 13.4: Full build**

```bash
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 13.5: Commit**

```bash
git add FantasyMapsDotNet/FantasyMaps.App/
git commit -m "feat: assemble Tutorial.razor with all 8 sections"
```

---

## Task 14: Visual Verification

Run the app and verify all 8 sections visually against the original `index.html`.

- [ ] **Step 14.1: Launch the app**

```bash
dotnet run --project FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```

- [ ] **Step 14.2: Side-by-side comparison checklist**

Open `d:/Projetos/Fantasy Maps/index.html` in a browser alongside the MAUI app. Verify each section:

| Section | Check |
|---|---|
| 1 — Random Points | Clicking "Generate" shows ~4096 small dots distributed evenly |
| 2 — Voronoi Mesh | Clicking "Show Voronoi mesh" shows edge lines + red dots |
| 3 — Height Map | Each button (slope, cone, etc.) updates the Viridis-colored visualization |
| 4 — Erosion | "Generate coastline" shows terrain; "Erode" smooths it; "Clean coast" removes isolated pixels |
| 5 — Features | Toggle buttons independently hide/show coast, rivers, slope strokes |
| 6 — Cities | Each "Add new city" click places a white circle on land near rivers |
| 7 — Territories | "Show territories" colors regions by city with red dashed borders |
| 8 — Full Map | "Generate map" produces a complete map with labels; "Download SVG" opens a save dialog |

- [ ] **Step 14.3: Fix any visual discrepancies**

Common issues and fixes:
- **SVG coordinates wrong**: Check `SvgBuilder.Scale` (should be 1000) and ensure all output paths use `MakePath` rather than raw coordinate strings.
- **No coast/rivers**: Verify `Erosion.SetSeaLevel` is called (some vertices should be `<= 0`).
- **Blank SVG**: Check `SvgViewer.razor` is rendering `@((MarkupString)Content)` not escaped content.
- **Triangle.NET API mismatch**: Check `MeshBuilder.cs` — the `edge.N1`/`edge.N2` site index properties may have different names in your installed version.
- **Labels missing**: Ensure `NameGenerator.MakeName` returns non-empty strings and that `LabelPlacer.DrawLabels` is called with a valid `LanguageModel`.

- [ ] **Step 14.4: Final commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: MAUI Blazor app complete — all 8 tutorial sections verified"
```

---

## Performance Verification

After visual verification, benchmark Section 8 generation:

- [ ] **Add a timing log to Section 8**

In `Section8FullMap.razor`, temporarily wrap the `Task.Run` body:

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... generation code ...
sw.Stop();
Console.WriteLine($"Full map generation: {sw.ElapsedMilliseconds}ms");
```

- [ ] **Run in Release configuration**

```bash
dotnet run --project FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0 -c Release
```

Expected: Full map generation (16K points) < 5s on a modern Windows machine.

If generation exceeds targets:
- `GenerateGoodMesh` slow → profile Triangle.NET call; reduce Lloyd relaxation iterations
- SVG string building slow → profile `TerrainRenderer.VisualizeVoronoi`; avoid per-element `AppendLine` overhead with larger buffer sizes
- Label placement slow → `LabelPlacer` exhaustive search; add early exit when penalty is very low

---

## Plan Complete

Both plans are now implemented:
- **Plan 1** (Core): All algorithms ported to C#, tested with xUnit
- **Plan 2** (App): All 8 tutorial sections as Blazor components, visual parity with original

The application is ready for use. To extend further:
- Add seeded random (`Random(seed)`) for reproducible maps
- Add sliders for terrain parameters (number of mountains, erosion amount)
- Port to additional MAUI targets (macOS, Android) by adding platform targets to the `.csproj`
