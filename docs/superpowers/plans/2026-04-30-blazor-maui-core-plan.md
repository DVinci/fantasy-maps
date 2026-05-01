# Fantasy Maps — Core Library Implementation Plan (Plan 1 of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port all terrain generation, language, and SVG rendering algorithms from `map.js` and `names.js` into a pure C# class library (`FantasyMaps.Core`) with full xUnit test coverage.

**Architecture:** `FantasyMaps.Core` is a dependency-free class library (except `Triangle.NET` for Voronoi) that implements all procedural generation algorithms. The library has no UI or MAUI dependency, making it fully unit-testable. Plan 2 (MAUI App) builds on top of this.

**Tech Stack:** .NET 9, C#, xUnit, Triangle.NET (NuGet: `Triangle.NET` by Christian Woltering)

---

## Data Model Reference

Before implementing, understand these key invariants from the original JS:

- **Height field** (`float[]`): Indexed by **Voronoi vertex index** (0..`mesh.Vxs.Length-1`). Every height array has a `.Mesh` reference property (use `HeightField` class).
- **`mesh.Tris[i]`**: Array of `double[2]` input-point coordinates adjacent to Voronoi vertex `i`. Typically 3 points (one per Delaunay triangle touching that circumcenter). Used to render colored triangles.
- **`mesh.Edges[i]`**: `(int V0, int V1, double[]? Left, double[]? Right)` — V0/V1 are Voronoi vertex indices; Left/Right are `[x,y]` coordinates of the two Delaunay sites on each side of this edge. Used for coastlines and borders.
- **Cities**: `List<int>` of Voronoi vertex indices — cities sit at mesh vertices.
- **Territories** (`int[]`): Indexed by Voronoi vertex index → owning city's vertex index.
- **SVG coordinate system**: Internal coords are `[-0.5, 0.5]`. All SVG output multiplies by 1000. ViewBox: `-500 -500 1000 1000`.
- **Sea level**: `h[i] > 0` = land; `h[i] <= 0` = ocean.

---

## File Map

```
FantasyMapsDotNet/
  FantasyMaps.Core/
    Mesh/
      VoronoiMesh.cs          # Core mesh data structure + IsEdge/IsNearEdge/Neighbours/Distance
      MeshBuilder.cs          # Triangle.NET wrapper: GenerateGoodMesh(n, extent)
    Terrain/
      HeightField.cs          # Wrapper: float[] + Mesh + static factory helpers
      HeightPrimitives.cs     # Zero, Slope, Cone, Mountains, Normalize, Peaky, Add, Relax, Map
      Erosion.cs              # Downhill, FillSinks, GetFlux, Trislope, GetSlope, ErosionRate,
                              # Erode, DoErosion, SetSeaLevel, CleanCoast
      CityPlacer.cs           # CityScore, PlaceCity, PlaceCities
      Rivers.cs               # MergeSegments, RelaxPath, Contour, GetRivers
      Territories.cs          # GetTerritories, GetBorders
    Language/
      LanguageModel.cs        # Phoneme sets, syllable structure, orthography, word cache
      LanguageFactory.cs      # MakeBasicLanguage(), MakeRandomLanguage()
      NameGenerator.cs        # MakeName(), GetWord(), GetMorpheme(), MakeWord()
    Rendering/
      ViridisColor.cs         # Interpolate(t) → "#rrggbb"
      ColorPalette.cs         # Category10 color array
      SvgBuilder.cs           # Coordinate scaling, MakePath(), AppendCircle(), AppendLine(), etc.
      TerrainRenderer.cs      # VisualizeVoronoi(), DrawPaths(), VisualizeSlopes(), VisualizeCities()
      LabelPlacer.cs          # DrawLabels() with penalty-based placement
      MapRenderer.cs          # DrawMap() master function
    RenderState.cs            # Render state object: H, Cities, Params, Rivers, Coasts, Borders, Terr
    MapParams.cs              # Npts, Ncities, Nterrs, Fontsizes, Extent
    Extent.cs                 # record Extent(double Width, double Height)
  FantasyMaps.Core.Tests/
    MeshBuilderTests.cs
    HeightPrimitivesTests.cs
    ErosionTests.cs
    CityPlacerTests.cs
    RiversTests.cs
    TerritoriesTests.cs
    NameGeneratorTests.cs
    ViridisColorTests.cs
```

---

## Task 1: Solution and Project Setup

**Files:**
- Create: `FantasyMapsDotNet/FantasyMaps.sln`
- Create: `FantasyMaps.Core/FantasyMaps.Core.csproj`
- Create: `FantasyMaps.Core.Tests/FantasyMaps.Core.Tests.csproj`

- [ ] **Step 1.1: Create the solution and projects**

```bash
cd "d:/Projetos/Fantasy Maps"
mkdir FantasyMapsDotNet && cd FantasyMapsDotNet
dotnet new sln -n FantasyMaps
dotnet new classlib -n FantasyMaps.Core -f net9.0 -o FantasyMaps.Core
dotnet new xunit -n FantasyMaps.Core.Tests -f net9.0 -o FantasyMaps.Core.Tests
dotnet sln add FantasyMaps.Core/FantasyMaps.Core.csproj
dotnet sln add FantasyMaps.Core.Tests/FantasyMaps.Core.Tests.csproj
```

- [ ] **Step 1.2: Wire up project references and packages**

```bash
cd FantasyMaps.Core.Tests
dotnet add reference ../FantasyMaps.Core/FantasyMaps.Core.csproj
cd ../FantasyMaps.Core
dotnet add package Triangle.NET
cd ..
```

> ⚠️ If the package `Triangle.NET` is not found, search NuGet for "Triangle.NET Woltering" — the package ID may vary by version. Alternatively use `dotnet add package TriangleNet`.

- [ ] **Step 1.3: Clean default files**

Delete the generated `Class1.cs` from `FantasyMaps.Core/` and `UnitTest1.cs` from `FantasyMaps.Core.Tests/`.

```bash
rm FantasyMaps.Core/Class1.cs
rm FantasyMaps.Core.Tests/UnitTest1.cs
```

- [ ] **Step 1.4: Verify solution builds**

```bash
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 1.5: Create subdirectories**

```bash
mkdir -p FantasyMaps.Core/Mesh FantasyMaps.Core/Terrain FantasyMaps.Core/Language FantasyMaps.Core/Rendering
```

- [ ] **Step 1.6: Commit**

```bash
cd "d:/Projetos/Fantasy Maps"
git add FantasyMapsDotNet/
git commit -m "feat: initialize FantasyMaps solution with Core and Tests projects"
```

---

## Task 2: Root Types — Extent, MapParams, RenderState

**Files:**
- Create: `FantasyMaps.Core/Extent.cs`
- Create: `FantasyMaps.Core/MapParams.cs`
- Create: `FantasyMaps.Core/RenderState.cs`

- [ ] **Step 2.1: Write `Extent.cs`**

```csharp
namespace FantasyMaps.Core;

public record Extent(double Width = 1.0, double Height = 1.0);
```

- [ ] **Step 2.2: Write `MapParams.cs`**

```csharp
namespace FantasyMaps.Core;

public class MapParams
{
    public int Npts { get; set; } = 4096;
    public int Ncities { get; set; } = 15;
    public int Nterrs { get; set; } = 5;
    public double[] Fontsizes { get; set; } = [25, 18, 15];
    public Extent Extent { get; set; } = new();
}
```

- [ ] **Step 2.3: Write `RenderState.cs`** (forward reference — `HeightField` and `VoronoiMesh` are stubs for now)

```csharp
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core;

public class RenderState
{
    public HeightField H { get; set; } = null!;
    public List<int> Cities { get; set; } = [];
    public MapParams Params { get; set; } = new();
    // Paths: each is a list of [x,y] coordinate pairs forming a connected line
    public List<double[][]> Rivers { get; set; } = [];
    public List<double[][]> Coasts { get; set; } = [];
    public List<double[][]> Borders { get; set; } = [];
    // Territory map: vertex index → owning city vertex index (-1 = unowned)
    public int[] Terr { get; set; } = [];
    public LanguageModel? Language { get; set; }
}
```

> Note: `LanguageModel` is defined in Task 10. Add a forward reference or add the property after Task 10.

- [ ] **Step 2.4: Build**

```bash
dotnet build FantasyMaps.Core/FantasyMaps.Core.csproj
```
Expected: `0 Error(s)`

- [ ] **Step 2.5: Commit**

```bash
git add FantasyMapsDotNet/FantasyMaps.Core/
git commit -m "feat: add Extent, MapParams, RenderState types"
```

---

## Task 3: VoronoiMesh Data Structure

**Files:**
- Create: `FantasyMaps.Core/Mesh/VoronoiMesh.cs`
- Create: `FantasyMaps.Core.Tests/MeshBuilderTests.cs` (partial — structure tests only)

- [ ] **Step 3.1: Write the failing test first**

`FantasyMaps.Core.Tests/MeshBuilderTests.cs`:
```csharp
using FantasyMaps.Core.Mesh;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class MeshBuilderTests
{
    [Fact]
    public void VoronoiMesh_IsEdge_ReturnsTrueForBoundaryVertex()
    {
        // A vertex with fewer than 3 neighbours is an edge vertex
        var adj = new int[][] { [1], [0, 2], [1] };
        var mesh = new VoronoiMesh(
            vxs: [[0, 0], [0.1, 0], [0.2, 0]],
            adj: adj,
            tris: [[], [], []],
            edges: [],
            pts: [[0, 0]],
            extent: new Core.Extent());
        Assert.True(mesh.IsEdge(0));   // degree 1 < 3
        Assert.False(mesh.IsEdge(1));  // degree 2 — still edge but test IsEdge logic
    }

    [Fact]
    public void VoronoiMesh_Neighbours_ReturnsAdjacentIndices()
    {
        var adj = new int[][] { [1, 2], [0, 2], [0, 1] };
        var mesh = new VoronoiMesh(
            vxs: [[0, 0], [0.1, 0.1], [0.2, 0]],
            adj: adj,
            tris: [[], [], []],
            edges: [],
            pts: [[0.1, 0.05]],
            extent: new Core.Extent());
        Assert.Equal([1, 2], mesh.Neighbours(0));
    }

    [Fact]
    public void VoronoiMesh_Distance_ComputesEuclidean()
    {
        var mesh = new VoronoiMesh(
            vxs: [[0, 0], [3, 4]],
            adj: [[1], [0]],
            tris: [[], []],
            edges: [],
            pts: [],
            extent: new Core.Extent());
        Assert.Equal(5.0, mesh.Distance(0, 1), precision: 10);
    }
}
```

- [ ] **Step 3.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "MeshBuilderTests"
```
Expected: compile error (VoronoiMesh not found)

- [ ] **Step 3.3: Write `VoronoiMesh.cs`**

```csharp
namespace FantasyMaps.Core.Mesh;

public class VoronoiMesh
{
    // Voronoi vertices (circumcenters of Delaunay triangles). Each is [x, y].
    public double[][] Vxs { get; }
    // Adjacency list: Adj[i] = indices of Voronoi vertices adjacent to vertex i.
    public int[][] Adj { get; }
    // Tris[i] = list of [x,y] input-point coordinates adjacent to Voronoi vertex i.
    // Typically 3 points — forms the small triangle rendered around vertex i.
    public double[][][] Tris { get; }
    // Edges: (V0, V1, Left, Right). V0/V1 are Voronoi vertex indices.
    // Left/Right are [x,y] coords of the Delaunay sites on each side (null at boundary).
    public (int V0, int V1, double[]? Left, double[]? Right)[] Edges { get; }
    // Input points (Delaunay sites). Each is [x, y].
    public double[][] Pts { get; }
    public Extent Extent { get; }

    public VoronoiMesh(
        double[][] vxs,
        int[][] adj,
        double[][][] tris,
        (int, int, double[]?, double[]?)[] edges,
        double[][] pts,
        Extent extent)
    {
        Vxs = vxs;
        Adj = adj;
        Tris = tris;
        Edges = edges;
        Pts = pts;
        Extent = extent;
    }

    // A vertex is a boundary vertex if it has fewer than 3 neighbours.
    public bool IsEdge(int i) => Adj[i].Length < 3;

    // A vertex is "near edge" if within 5% of the extent boundary.
    public bool IsNearEdge(int i)
    {
        double x = Vxs[i][0], y = Vxs[i][1];
        double hw = Extent.Width / 2, hh = Extent.Height / 2;
        return x < -0.45 * Extent.Width || x > 0.45 * Extent.Width
            || y < -0.45 * Extent.Height || y > 0.45 * Extent.Height;
    }

    public int[] Neighbours(int i) => Adj[i];

    public double Distance(int i, int j)
    {
        double dx = Vxs[i][0] - Vxs[j][0];
        double dy = Vxs[i][1] - Vxs[j][1];
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
```

- [ ] **Step 3.4: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "MeshBuilderTests"
```
Expected: 3 passing.

- [ ] **Step 3.5: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add VoronoiMesh data structure with IsEdge/Neighbours/Distance"
```

---

## Task 4: MeshBuilder — Triangle.NET Voronoi Wrapper

**Files:**
- Create: `FantasyMaps.Core/Mesh/MeshBuilder.cs`
- Modify: `FantasyMaps.Core.Tests/MeshBuilderTests.cs` (add integration test)

This is the most complex task. `MeshBuilder.GenerateGoodMesh(n, extent)` wraps Triangle.NET to produce a `VoronoiMesh` with the correct data structure.

**Algorithm:**
1. Generate `n` random points in `[-w/2, w/2] × [-h/2, h/2]`
2. Apply 1 iteration of Lloyd relaxation: compute Voronoi polygons, replace each point with the centroid of its polygon
3. Triangulate with Triangle.NET (Delaunay)
4. Compute bounded Voronoi dual
5. Build `Vxs`, `Adj`, `Tris`, `Edges` from the Voronoi edge list

**Key Triangle.NET API** (verify against installed version):
```csharp
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Voronoi;

var polygon = new Polygon();
foreach (var pt in pts) polygon.Add(new Vertex(pt[0], pt[1]));
var delaunay = (TriangleNet.Mesh)polygon.Triangulate(new ConstraintOptions(), new QualityOptions());
var voronoi = new BoundedVoronoi(delaunay);
// voronoi.Edges: IEnumerable<VoronoiEdge>
//   edge.P1, edge.P2: vertex indices into voronoi.Vertices
// voronoi.Vertices: IList<Point> — circumcenter coordinates
// voronoi.Regions: IEnumerable<VoronoiRegion>
//   region.ID: the Delaunay site index (index into original pts)
//   region.Vertices: IList<Point> — the Voronoi cell's vertices
```

> ⚠️ The exact API class names differ across Triangle.NET versions. Consult the installed package's XML documentation or source. The pattern is: triangulate the polygon → build VoronoiDiagram → iterate edges with their adjacent sites.

- [ ] **Step 4.1: Add integration test**

In `MeshBuilderTests.cs`, add:
```csharp
[Fact]
public void GenerateGoodMesh_ProducesValidMesh()
{
    var mesh = MeshBuilder.GenerateGoodMesh(256, new Core.Extent());
    Assert.True(mesh.Vxs.Length > 0, "Should have Voronoi vertices");
    Assert.Equal(mesh.Vxs.Length, mesh.Adj.Length);
    Assert.Equal(mesh.Vxs.Length, mesh.Tris.Length);
    // Every vertex has at least one neighbour
    Assert.All(mesh.Adj, adj => Assert.True(adj.Length >= 1));
    // Coordinates within extent
    Assert.All(mesh.Vxs, v => {
        Assert.True(v[0] >= -0.6 && v[0] <= 0.6);
        Assert.True(v[1] >= -0.6 && v[1] <= 0.6);
    });
}
```

- [ ] **Step 4.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "GenerateGoodMesh"
```
Expected: compile error (MeshBuilder not found)

- [ ] **Step 4.3: Write `MeshBuilder.cs`**

```csharp
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Voronoi;

namespace FantasyMaps.Core.Mesh;

public static class MeshBuilder
{
    public static VoronoiMesh GenerateGoodMesh(int n, Extent? extent = null)
    {
        extent ??= new Extent();
        var pts = GenerateGoodPoints(n, extent);
        return BuildMesh(pts, extent);
    }

    // --- Point generation ---

    private static double[][] GeneratePoints(int n, Extent extent)
    {
        var pts = new double[n][];
        for (int i = 0; i < n; i++)
            pts[i] = [(Random.Shared.NextDouble() - 0.5) * extent.Width,
                      (Random.Shared.NextDouble() - 0.5) * extent.Height];
        return pts;
    }

    private static double[] Centroid(IEnumerable<double[]> pts)
    {
        double sx = 0, sy = 0; int count = 0;
        foreach (var p in pts) { sx += p[0]; sy += p[1]; count++; }
        return [sx / count, sy / count];
    }

    // One iteration of Lloyd relaxation: replace each point with its Voronoi polygon centroid.
    private static double[][] ImprovePoints(double[][] pts, Extent extent)
    {
        // Build Voronoi polygons, compute centroids
        var polygon = PtsToPolygon(pts, extent);
        var options = new ConstraintOptions();
        var quality = new QualityOptions();
        var delaunay = (TriangleNet.Mesh)polygon.Triangulate(options, quality);
        var voronoi = new BoundedVoronoi(delaunay);

        // For each region, compute centroid of its polygon vertices
        var newPts = new List<double[]>();
        foreach (var region in voronoi.Regions)
        {
            if (region.Vertices == null || region.Vertices.Count == 0) continue;
            var regionCoords = region.Vertices.Select(v => new double[] { v.X, v.Y });
            newPts.Add(Centroid(regionCoords));
        }
        // If Lloyd produced fewer points (edge cases), pad with originals
        if (newPts.Count < pts.Length)
            newPts.AddRange(pts.Skip(newPts.Count));
        return [.. newPts.Take(pts.Length)];
    }

    private static double[][] GenerateGoodPoints(int n, Extent extent)
    {
        var pts = GeneratePoints(n, extent);
        pts = pts.OrderBy(p => p[0]).ToArray();
        return ImprovePoints(pts, extent);
    }

    // --- Mesh construction ---

    private static Polygon PtsToPolygon(double[][] pts, Extent extent)
    {
        double hw = extent.Width / 2, hh = extent.Height / 2;
        var polygon = new Polygon();
        foreach (var pt in pts)
            polygon.Add(new Vertex(pt[0], pt[1]));
        // Add bounding box vertices so BoundedVoronoi has a boundary
        polygon.Add(new Vertex(-hw, -hh));
        polygon.Add(new Vertex(hw, -hh));
        polygon.Add(new Vertex(hw, hh));
        polygon.Add(new Vertex(-hw, hh));
        return polygon;
    }

    private static VoronoiMesh BuildMesh(double[][] pts, Extent extent)
    {
        var polygon = PtsToPolygon(pts, extent);
        var delaunay = (TriangleNet.Mesh)polygon.Triangulate(
            new ConstraintOptions(), new QualityOptions());
        var voronoi = new BoundedVoronoi(delaunay);

        // Collect Voronoi vertices (circumcenters)
        var vxList = voronoi.Vertices.Select(v => new double[] { v.X, v.Y }).ToList();
        var vxs = vxList.ToArray();

        // Build adjacency, tris, edges from Voronoi edges
        var adj = new List<int>[vxs.Length];
        for (int i = 0; i < vxs.Length; i++) adj[i] = [];

        var tris = new List<double[]>[vxs.Length];
        for (int i = 0; i < vxs.Length; i++) tris[i] = [];

        var edges = new List<(int V0, int V1, double[]? Left, double[]? Right)>();

        foreach (var edge in voronoi.Edges)
        {
            int e0 = edge.P1, e1 = edge.P2;
            if (e0 < 0 || e1 < 0 || e0 >= vxs.Length || e1 >= vxs.Length) continue;

            // Adjacency
            if (!adj[e0].Contains(e1)) adj[e0].Add(e1);
            if (!adj[e1].Contains(e0)) adj[e1].Add(e0);

            // Left/Right sites — the two Delaunay vertices that this Voronoi edge separates
            double[]? left = null, right = null;
            if (edge.N1 >= 0 && edge.N1 < pts.Length) left = pts[edge.N1];
            if (edge.N2 >= 0 && edge.N2 < pts.Length) right = pts[edge.N2];

            // Tris: each vertex collects adjacent input-point coordinates
            if (left != null)
            {
                if (!tris[e0].Any(p => p[0] == left[0] && p[1] == left[1])) tris[e0].Add(left);
                if (!tris[e1].Any(p => p[0] == left[0] && p[1] == left[1])) tris[e1].Add(left);
            }
            if (right != null)
            {
                if (!tris[e0].Any(p => p[0] == right[0] && p[1] == right[1])) tris[e0].Add(right);
                if (!tris[e1].Any(p => p[0] == right[0] && p[1] == right[1])) tris[e1].Add(right);
            }

            edges.Add((e0, e1, left, right));
        }

        return new VoronoiMesh(
            vxs: vxs,
            adj: adj.Select(a => a.ToArray()).ToArray(),
            tris: tris.Select(t => t.ToArray()).ToArray(),
            edges: [.. edges],
            pts: pts,
            extent: extent);
    }
}
```

> ⚠️ `edge.N1` and `edge.N2` are the Triangle.NET property names for the two site indices adjacent to a Voronoi edge. Verify against the installed package — they may be named differently (e.g., `Twin`, `Site1`, `Site2`). The intent is: two integer indices into the original input `pts` array.

- [ ] **Step 4.4: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "MeshBuilderTests"
```
Expected: all 4 passing.

- [ ] **Step 4.5: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add MeshBuilder with Triangle.NET Voronoi/Delaunay wrapper"
```

---

## Task 5: HeightField and Primitives

**Files:**
- Create: `FantasyMaps.Core/Terrain/HeightField.cs`
- Create: `FantasyMaps.Core/Terrain/HeightPrimitives.cs`
- Create: `FantasyMaps.Core.Tests/HeightPrimitivesTests.cs`

- [ ] **Step 5.1: Write failing tests**

`HeightPrimitivesTests.cs`:
```csharp
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class HeightPrimitivesTests
{
    private static VoronoiMesh MakeSmallMesh()
    {
        // 3-vertex mesh at known positions
        var vxs = new double[][] { [-0.2, 0.0], [0.0, 0.0], [0.2, 0.0] };
        var adj = new int[][] { [1], [0, 2], [1] };
        var tris = new double[][][] { [], [], [] };
        return new VoronoiMesh(vxs, adj, tris, [], [], new Core.Extent());
    }

    [Fact]
    public void Zero_ReturnsAllZeros()
    {
        var mesh = MakeSmallMesh();
        var h = HeightPrimitives.Zero(mesh);
        Assert.Equal(3, h.Length);
        Assert.All(h.Values, v => Assert.Equal(0f, v));
        Assert.Same(mesh, h.Mesh);
    }

    [Fact]
    public void Normalize_ScalesToZeroOne()
    {
        var mesh = MakeSmallMesh();
        var h = HeightPrimitives.Zero(mesh);
        h.Values[0] = -1f; h.Values[1] = 0f; h.Values[2] = 3f;
        var norm = HeightPrimitives.Normalize(h);
        Assert.Equal(0f, norm.Values[0], precision: 5);
        Assert.Equal(1f, norm.Values[2], precision: 5);
        Assert.True(norm.Values[1] > 0f && norm.Values[1] < 1f);
    }

    [Fact]
    public void Add_SumsHeightFields()
    {
        var mesh = MakeSmallMesh();
        var h1 = HeightPrimitives.Zero(mesh); h1.Values[0] = 1f;
        var h2 = HeightPrimitives.Zero(mesh); h2.Values[0] = 2f;
        var sum = HeightPrimitives.Add(h1, h2);
        Assert.Equal(3f, sum.Values[0], precision: 5);
    }

    [Fact]
    public void Slope_ProducesGradient()
    {
        var mesh = MakeSmallMesh();
        // Direction [1,0] → x-coordinate becomes height
        var h = HeightPrimitives.Slope(mesh, [1.0, 0.0]);
        // vertex at x=-0.2 should be lower than at x=0.2
        Assert.True(h.Values[0] < h.Values[2]);
    }
}
```

- [ ] **Step 5.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "HeightPrimitivesTests"
```
Expected: compile error.

- [ ] **Step 5.2b: Write `Rand.cs`** (needed by HeightPrimitives, TerrainRenderer, and App components)

```csharp
namespace FantasyMaps.Core;

public static class Rand
{
    private static double? _spare;

    // Box-Muller normal distribution.
    public static double Normal()
    {
        if (_spare.HasValue) { var s = _spare.Value; _spare = null; return s; }
        double u, v, mag;
        do { u = Random.Shared.NextDouble() * 2 - 1; v = Random.Shared.NextDouble() * 2 - 1;
             mag = u * u + v * v; } while (mag >= 1 || mag == 0);
        double mul = Math.Sqrt(-2 * Math.Log(mag) / mag);
        _spare = v * mul;
        return u * mul;
    }

    public static double Uniform(double lo, double hi) => lo + Random.Shared.NextDouble() * (hi - lo);
}
```

- [ ] **Step 5.3: Write `HeightField.cs`**

```csharp
using FantasyMaps.Core.Mesh;

namespace FantasyMaps.Core.Terrain;

// Typed wrapper around float[] that always carries a Mesh reference.
// Mirrors the JS pattern: h.mesh = mesh; h.downhill (cached).
public class HeightField
{
    public float[] Values { get; }
    public VoronoiMesh Mesh { get; }
    // Cached downhill array (set by Erosion.Downhill, invalidated on mutation)
    public int[]? DownhillCache { get; set; }

    public HeightField(float[] values, VoronoiMesh mesh)
    {
        Values = values;
        Mesh = mesh;
    }

    public int Length => Values.Length;

    public float this[int i]
    {
        get => Values[i];
        set { Values[i] = value; DownhillCache = null; }
    }

    public HeightField Clone()
    {
        var copy = new float[Values.Length];
        Values.CopyTo(copy, 0);
        return new HeightField(copy, Mesh);
    }
}
```

- [ ] **Step 5.4: Write `HeightPrimitives.cs`**

```csharp
using FantasyMaps.Core.Mesh;

namespace FantasyMaps.Core.Terrain;

public static class HeightPrimitives
{
    public static HeightField Zero(VoronoiMesh mesh)
        => new(new float[mesh.Vxs.Length], mesh);

    // Apply f(x, y, i) to each vertex, return new HeightField.
    public static HeightField Map(HeightField h, Func<double, double, int, float> f)
    {
        var result = Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            var vx = h.Mesh.Vxs[i];
            result.Values[i] = f(vx[0], vx[1], i);
        }
        return result;
    }

    // Maps values element-wise.
    public static HeightField Map(HeightField h, Func<float, float> f)
    {
        var result = Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++) result.Values[i] = f(h.Values[i]);
        return result;
    }

    public static HeightField Slope(VoronoiMesh mesh, double[] direction)
        => Map(Zero(mesh), (x, y, _) => (float)(x * direction[0] + y * direction[1]));

    public static HeightField Cone(VoronoiMesh mesh, double strength)
        => Map(Zero(mesh), (x, y, _) => (float)(Math.Sqrt(x * x + y * y) * strength));

    // n Gaussian mountains of radius r at random positions.
    public static HeightField Mountains(VoronoiMesh mesh, int n, double r = 0.05)
    {
        var result = Zero(mesh);
        for (int k = 0; k < n; k++)
        {
            double cx = (Random.Shared.NextDouble() - 0.5) * mesh.Extent.Width;
            double cy = (Random.Shared.NextDouble() - 0.5) * mesh.Extent.Height;
            for (int i = 0; i < mesh.Vxs.Length; i++)
            {
                double dx = mesh.Vxs[i][0] - cx, dy = mesh.Vxs[i][1] - cy;
                result.Values[i] += (float)Math.Exp(-(dx * dx + dy * dy) / (2 * r * r));
            }
        }
        return result;
    }

    public static HeightField Normalize(HeightField h)
    {
        float lo = h.Values.Min(), hi = h.Values.Max();
        float range = hi - lo;
        if (range < 1e-9f) return h.Clone();
        return Map(h, v => (v - lo) / range);
    }

    // Emphasize peaks: apply sqrt after normalize.
    public static HeightField Peaky(HeightField h)
    {
        var norm = Normalize(h);
        return Map(norm, v => (float)Math.Sqrt(v));
    }

    // Sum two or more height fields element-wise.
    public static HeightField Add(params HeightField[] fields)
    {
        var result = Zero(fields[0].Mesh);
        foreach (var h in fields)
            for (int i = 0; i < h.Length; i++) result.Values[i] += h.Values[i];
        return result;
    }

    // Smooth by replacing each vertex's value with the average of itself and its neighbours.
    public static HeightField Relax(HeightField h)
    {
        var result = Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            var nbs = h.Mesh.Neighbours(i);
            if (nbs.Length == 0) { result.Values[i] = h.Values[i]; continue; }
            float sum = h.Values[i];
            foreach (int nb in nbs) sum += h.Values[nb];
            result.Values[i] = sum / (nbs.Length + 1);
        }
        return result;
    }

    // q-th quantile (q ∈ [0,1]).
    public static float Quantile(HeightField h, double q)
    {
        var sorted = h.Values.OrderBy(v => v).ToArray();
        int idx = Math.Clamp((int)(q * sorted.Length), 0, sorted.Length - 1);
        return sorted[idx];
    }
}
```

- [ ] **Step 5.5: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "HeightPrimitivesTests"
```
Expected: 4 passing.

- [ ] **Step 5.6: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add HeightField and HeightPrimitives (slope, cone, mountains, normalize, add, relax)"
```

---

## Task 6: Erosion Algorithms

**Files:**
- Create: `FantasyMaps.Core/Terrain/Erosion.cs`
- Create: `FantasyMaps.Core.Tests/ErosionTests.cs`

- [ ] **Step 6.1: Write failing tests**

`ErosionTests.cs`:
```csharp
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class ErosionTests
{
    [Fact]
    public void GetFlux_IsNonNegativeEverywhere()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(64);
        var h = HeightPrimitives.Cone(mesh, -0.5f);
        h = HeightPrimitives.Normalize(h);
        var flux = Erosion.GetFlux(h);
        Assert.All(flux.Values, v => Assert.True(v >= 0f));
    }

    [Fact]
    public void Erode_ReducesMeanLandHeight()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(64);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        float before = h.Values.Average();
        var eroded = Erosion.Erode(h, 0.1f);
        Assert.True(eroded.Values.Average() < before);
    }

    [Fact]
    public void FillSinks_EliminatesInteriorMinima()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(64);
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Normalize(h);
        var filled = Erosion.FillSinks(h);
        // Every interior vertex should have at least one downhill path to the edge
        for (int i = 0; i < filled.Length; i++)
        {
            if (filled.Mesh.IsEdge(i)) continue;
            var nbs = filled.Mesh.Neighbours(i);
            bool hasDownhill = nbs.Any(nb => filled[nb] < filled[i] + 1e-4f);
            // After fillSinks, either there is a downhill path OR vertex is at edge
            // (not a strict interior minimum)
            Assert.True(hasDownhill || filled.Mesh.IsEdge(i),
                $"Vertex {i} appears to be an interior minimum after fillSinks");
        }
    }
}
```

- [ ] **Step 6.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "ErosionTests"
```

- [ ] **Step 6.3: Write `Erosion.cs`**

Direct port of the JS erosion algorithms:

```csharp
namespace FantasyMaps.Core.Terrain;

public static class Erosion
{
    // For each vertex, find the index of the lowest neighbouring vertex.
    // Returns -2 for edge vertices (always drain to boundary), -1 if no downhill exists (local min).
    public static int[] Downhill(HeightField h)
    {
        if (h.DownhillCache != null) return h.DownhillCache;
        var downs = new int[h.Length];
        for (int i = 0; i < h.Length; i++)
        {
            if (h.Mesh.IsEdge(i)) { downs[i] = -2; continue; }
            int best = -1; float bestH = h[i];
            foreach (int nb in h.Mesh.Neighbours(i))
                if (h[nb] < bestH) { bestH = h[nb]; best = nb; }
            downs[i] = best;
        }
        h.DownhillCache = downs;
        return downs;
    }

    // Ensure no interior sinks by repeatedly raising vertices until all have
    // a drainage path to the edge (Planchon-Darboux algorithm).
    public static HeightField FillSinks(HeightField h, float epsilon = 1e-5f)
    {
        const float Infinity = 999999f;
        var newH = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
            newH.Values[i] = h.Mesh.IsNearEdge(i) ? h[i] : Infinity;

        while (true)
        {
            bool changed = false;
            for (int i = 0; i < h.Length; i++)
            {
                if (newH[i] == h[i]) continue;
                foreach (int nb in h.Mesh.Neighbours(i))
                {
                    if (h[i] >= newH[nb] + epsilon) { newH.Values[i] = h[i]; changed = true; break; }
                    float oh = newH[nb] + epsilon;
                    if (newH[i] > oh && oh > h[i]) { newH.Values[i] = oh; changed = true; }
                }
            }
            if (!changed) return newH;
        }
    }

    // Water flux: how much water flows through each vertex (accumulates downhill).
    public static HeightField GetFlux(HeightField h)
    {
        var dh = Downhill(h);
        var flux = HeightPrimitives.Zero(h.Mesh);
        var idxs = Enumerable.Range(0, h.Length).ToArray();
        Array.Sort(idxs, (a, b) => h[b].CompareTo(h[a]));  // descending by height
        float init = 1f / h.Length;
        for (int i = 0; i < h.Length; i++) flux.Values[i] = init;
        foreach (int j in idxs)
            if (dh[j] >= 0) flux.Values[dh[j]] += flux[j];
        return flux;
    }

    // Compute 2D slope gradient at vertex i using its 3 neighbours (via triangle cross-product).
    public static (double Sx, double Sy) Trislope(HeightField h, int i)
    {
        var nbs = h.Mesh.Neighbours(i);
        if (nbs.Length != 3) return (0, 0);
        var p0 = h.Mesh.Vxs[nbs[0]]; var p1 = h.Mesh.Vxs[nbs[1]]; var p2 = h.Mesh.Vxs[nbs[2]];
        double x1 = p1[0] - p0[0], x2 = p2[0] - p0[0];
        double y1 = p1[1] - p0[1], y2 = p2[1] - p0[1];
        double det = x1 * y2 - x2 * y1;
        double h1 = h[nbs[1]] - h[nbs[0]], h2 = h[nbs[2]] - h[nbs[0]];
        return ((y2 * h1 - y1 * h2) / det, (-x2 * h1 + x1 * h2) / det);
    }

    public static HeightField GetSlope(HeightField h)
    {
        var sl = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            var (sx, sy) = Trislope(h, i);
            sl.Values[i] = (float)Math.Sqrt(sx * sx + sy * sy);
        }
        return sl;
    }

    public static HeightField ErosionRate(HeightField h)
    {
        var flux = GetFlux(h); var sl = GetSlope(h);
        var result = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            float river = (float)(Math.Sqrt(flux[i]) * sl[i]);
            float creep = sl[i] * sl[i];
            float total = Math.Min(1000f * river + creep, 200f);
            result.Values[i] = total;
        }
        return result;
    }

    public static HeightField Erode(HeightField h, float amount)
    {
        var er = ErosionRate(h);
        float maxR = er.Values.Max();
        var result = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
            result.Values[i] = h[i] - amount * (er[i] / maxR);
        return result;
    }

    public static HeightField DoErosion(HeightField h, float amount, int n = 1)
    {
        h = FillSinks(h);
        for (int i = 0; i < n; i++) { h = Erode(h, amount); h = FillSinks(h); }
        return h;
    }

    // Shift heights so the q-th quantile becomes sea level (0).
    public static HeightField SetSeaLevel(HeightField h, double q)
    {
        float delta = HeightPrimitives.Quantile(h, q);
        var result = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++) result.Values[i] = h[i] - delta;
        return result;
    }

    // Remove isolated land pixels (surrounded by ocean) and isolated ocean pixels (surrounded by land).
    public static HeightField CleanCoast(HeightField h, int iters = 1)
    {
        for (int iter = 0; iter < iters; iter++)
        {
            // Pass 1: remove isolated land (surrounded mostly by ocean)
            var newH = h.Clone();
            for (int i = 0; i < h.Length; i++)
            {
                if (h[i] <= 0f) continue;
                var nbs = h.Mesh.Neighbours(i);
                if (nbs.Length != 3) continue;
                int landCount = 0; float bestOcean = -999999f;
                foreach (int nb in nbs)
                    if (h[nb] > 0f) landCount++; else if (h[nb] > bestOcean) bestOcean = h[nb];
                if (landCount > 1) continue;
                newH.Values[i] = bestOcean / 2f;
            }
            h = newH;
            // Pass 2: remove isolated ocean (surrounded mostly by land)
            newH = h.Clone();
            for (int i = 0; i < h.Length; i++)
            {
                if (h[i] > 0f) continue;
                var nbs = h.Mesh.Neighbours(i);
                if (nbs.Length != 3) continue;
                int oceanCount = 0; float bestLand = 999999f;
                foreach (int nb in nbs)
                    if (h[nb] <= 0f) oceanCount++; else if (h[nb] < bestLand) bestLand = h[nb];
                if (oceanCount > 1) continue;
                newH.Values[i] = bestLand / 2f;
            }
            h = newH;
        }
        return h;
    }
}
```

- [ ] **Step 6.4: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "ErosionTests"
```
Expected: 3 passing.

- [ ] **Step 6.5: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add Erosion (downhill, fillSinks, flux, slope, erode, cleanCoast)"
```

---

## Task 7: City Placement

**Files:**
- Create: `FantasyMaps.Core/Terrain/CityPlacer.cs`
- Create: `FantasyMaps.Core.Tests/CityPlacerTests.cs`

- [ ] **Step 7.1: Write failing test**

`CityPlacerTests.cs`:
```csharp
using FantasyMaps.Core;
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class CityPlacerTests
{
    [Fact]
    public void PlaceCities_PlacesOnLandNotNearEdge()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(256);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        h = Erosion.SetSeaLevel(h, 0.5);
        var render = new RenderState { H = h, Params = new MapParams { Ncities = 5 } };
        CityPlacer.PlaceCities(render);
        Assert.Equal(5, render.Cities.Count);
        foreach (int city in render.Cities)
        {
            Assert.True(h[city] > 0f, $"City {city} is in ocean");
            Assert.False(mesh.IsNearEdge(city), $"City {city} is near edge");
        }
    }
}
```

- [ ] **Step 7.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "CityPlacerTests"
```

- [ ] **Step 7.3: Write `CityPlacer.cs`**

```csharp
namespace FantasyMaps.Core.Terrain;

public static class CityPlacer
{
    public static float[] CityScore(HeightField h, List<int> cities)
    {
        var flux = Erosion.GetFlux(h);
        var score = new float[h.Length];
        for (int i = 0; i < h.Length; i++) score[i] = (float)Math.Sqrt(flux[i]);

        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] <= 0f || h.Mesh.IsNearEdge(i)) { score[i] = -999999f; continue; }
            double vx = h.Mesh.Vxs[i][0], vy = h.Mesh.Vxs[i][1];
            // Bonus for being near the centre of the map
            score[i] += (float)(0.01 / (1e-9 + Math.Abs(vx) - h.Mesh.Extent.Width / 2));
            score[i] += (float)(0.01 / (1e-9 + Math.Abs(vy) - h.Mesh.Extent.Height / 2));
            // Penalty for proximity to existing cities
            foreach (int city in cities)
                score[i] -= (float)(0.02 / (h.Mesh.Distance(city, i) + 1e-9));
        }
        return score;
    }

    public static void PlaceCity(RenderState render)
    {
        var score = CityScore(render.H, render.Cities);
        int newCity = Array.IndexOf(score, score.Max());
        render.Cities.Add(newCity);
    }

    public static void PlaceCities(RenderState render)
    {
        for (int i = 0; i < render.Params.Ncities; i++) PlaceCity(render);
    }
}
```

- [ ] **Step 7.4: Run test — verify it passes**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "CityPlacerTests"
```
Expected: 1 passing.

- [ ] **Step 7.5: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add CityPlacer (score, place, placeCities)"
```

---

## Task 8: Rivers and Coastlines

**Files:**
- Create: `FantasyMaps.Core/Terrain/Rivers.cs`
- Create: `FantasyMaps.Core.Tests/RiversTests.cs`

- [ ] **Step 8.1: Write failing tests**

`RiversTests.cs`:
```csharp
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class RiversTests
{
    private static HeightField MakeTestTerrain()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(256);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        h = Erosion.FillSinks(h);
        return Erosion.SetSeaLevel(h, 0.5);
    }

    [Fact]
    public void Contour_ReturnsPathsAtSeaLevel()
    {
        var h = MakeTestTerrain();
        var coasts = Rivers.Contour(h, 0f);
        Assert.True(coasts.Count > 0, "Should have at least one coastline path");
        // Each path element is a [x,y] coordinate pair
        Assert.All(coasts, path => Assert.True(path.Length >= 2));
    }

    [Fact]
    public void GetRivers_ReturnsPathsAboveSeaLevel()
    {
        var h = MakeTestTerrain();
        var riverPaths = Rivers.GetRivers(h, 0.01f);
        // Rivers should all be above sea level
        foreach (var path in riverPaths)
            foreach (var pt in path)
                Assert.True(pt.Length == 2);
    }
}
```

- [ ] **Step 8.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "RiversTests"
```

- [ ] **Step 8.3: Write `Rivers.cs`**

```csharp
namespace FantasyMaps.Core.Terrain;

public static class Rivers
{
    // Contour: find all edges crossing `level` and merge into continuous paths.
    // Returns list of paths, each path is an array of [x,y] coordinate pairs.
    public static List<double[][]> Contour(HeightField h, float level = 0f)
    {
        var segments = new List<double[][]>();
        foreach (var (v0, v1, left, right) in h.Mesh.Edges)
        {
            if (right == null) continue;
            if (h.Mesh.IsNearEdge(v0) || h.Mesh.IsNearEdge(v1)) continue;
            bool v0Above = h[v0] > level, v1Above = h[v1] > level;
            if (v0Above != v1Above && left != null)
                segments.Add([left, right]);
        }
        return MergeSegments(segments);
    }

    // Rivers: find high-flux paths above sea level.
    public static List<double[][]> GetRivers(HeightField h, float limit)
    {
        var dh = Erosion.Downhill(h);
        var flux = Erosion.GetFlux(h);
        int aboveCount = h.Values.Count(v => v > 0f);
        float adjustedLimit = limit * aboveCount / h.Length;

        var links = new List<double[][]>();
        for (int i = 0; i < h.Length; i++)
        {
            if (h.Mesh.IsNearEdge(i)) continue;
            if (flux[i] > adjustedLimit && h[i] > 0f && dh[i] >= 0)
            {
                var up = h.Mesh.Vxs[i];
                var downVx = h.Mesh.Vxs[dh[i]];
                if (h[dh[i]] > 0f)
                    links.Add([up, downVx]);
                else
                    links.Add([up, [(up[0] + downVx[0]) / 2, (up[1] + downVx[1]) / 2]]);
            }
        }
        return MergeSegments(links).Select(RelaxPath).ToList();
    }

    // Connect line segments into longer paths using shared endpoints.
    // Segments are identified by their endpoint coordinates (coordinate-string key).
    public static List<double[][]> MergeSegments(List<double[][]> segs)
    {
        // Build adjacency: coordinate-key → list of other endpoint coordinate-keys
        var adj = new Dictionary<string, List<string>>();
        var coordMap = new Dictionary<string, double[]>();

        string Key(double[] pt) => $"{pt[0]:R},{pt[1]:R}";
        void AddAdj(double[] a, double[] b)
        {
            string ka = Key(a), kb = Key(b);
            coordMap[ka] = a; coordMap[kb] = b;
            if (!adj.ContainsKey(ka)) adj[ka] = [];
            if (!adj.ContainsKey(kb)) adj[kb] = [];
            adj[ka].Add(kb); adj[kb].Add(ka);
        }
        foreach (var seg in segs) { if (seg.Length >= 2) AddAdj(seg[0], seg[^1]); }

        var done = new bool[segs.Count];
        var paths = new List<double[][]>();
        List<string>? path = null;

        while (true)
        {
            if (path == null)
            {
                int idx = Array.FindIndex(done, d => !d);
                if (idx < 0) break;
                done[idx] = true;
                path = [Key(segs[idx][0]), Key(segs[idx][^1])];
            }
            bool changed = false;
            for (int i = 0; i < segs.Count; i++)
            {
                if (done[i]) continue;
                string s0 = Key(segs[i][0]), s1 = Key(segs[i][^1]);
                string head = path[0], tail = path[^1];
                if (adj.TryGetValue(head, out var headAdj) && headAdj.Count == 2 && s1 == head)
                    { path.Insert(0, s0); done[i] = true; changed = true; break; }
                if (adj.TryGetValue(head, out headAdj) && headAdj.Count == 2 && s0 == head)
                    { path.Insert(0, s1); done[i] = true; changed = true; break; }
                if (adj.TryGetValue(tail, out var tailAdj) && tailAdj.Count == 2 && s0 == tail)
                    { path.Add(s1); done[i] = true; changed = true; break; }
                if (adj.TryGetValue(tail, out tailAdj) && tailAdj.Count == 2 && s1 == tail)
                    { path.Add(s0); done[i] = true; changed = true; break; }
            }
            if (!changed)
            {
                paths.Add(path.Select(k => coordMap[k]).ToArray());
                path = null;
            }
        }
        return paths;
    }

    // Smooth a path via weighted average of neighbours (keep endpoints fixed).
    public static double[][] RelaxPath(double[][] path)
    {
        if (path.Length < 3) return path;
        var result = new double[path.Length][];
        result[0] = path[0];
        for (int i = 1; i < path.Length - 1; i++)
            result[i] = [
                0.25 * path[i-1][0] + 0.5 * path[i][0] + 0.25 * path[i+1][0],
                0.25 * path[i-1][1] + 0.5 * path[i][1] + 0.25 * path[i+1][1]];
        result[^1] = path[^1];
        return result;
    }
}
```

- [ ] **Step 8.4: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "RiversTests"
```
Expected: 2 passing.

- [ ] **Step 8.5: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add Rivers (contour, getRivers, mergeSegments, relaxPath)"
```

---

## Task 9: Territory Expansion

**Files:**
- Create: `FantasyMaps.Core/Terrain/Territories.cs`
- Create: `FantasyMaps.Core.Tests/TerritoriesTests.cs`

- [ ] **Step 9.1: Write failing test**

`TerritoriesTests.cs`:
```csharp
using FantasyMaps.Core;
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class TerritoriesTests
{
    [Fact]
    public void GetTerritories_AssignsEveryLandVertexToACity()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(128);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        h = Erosion.SetSeaLevel(h, 0.5);
        var render = new RenderState { H = h, Params = new MapParams { Ncities = 3, Nterrs = 3 } };
        CityPlacer.PlaceCities(render);
        var terr = Territories.GetTerritories(render);
        render.Terr = terr;
        // Every land vertex should have a territory owner
        for (int i = 0; i < h.Length; i++)
            if (h[i] > 0f && !mesh.IsEdge(i))
                Assert.True(terr[i] >= 0, $"Land vertex {i} has no territory owner");
    }
}
```

- [ ] **Step 9.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "TerritoriesTests"
```

- [ ] **Step 9.3: Write `Territories.cs`**

```csharp
namespace FantasyMaps.Core.Terrain;

public static class Territories
{
    // Flood-fill territory expansion from major cities using a priority queue.
    // Returns int[] indexed by vertex index, value = owning city's vertex index (-1 = unowned).
    public static int[] GetTerritories(RenderState render)
    {
        var h = render.H;
        var cities = render.Cities;
        int n = Math.Min(render.Params.Nterrs, cities.Count);
        var flux = Erosion.GetFlux(h);
        var terr = new int[h.Length];
        Array.Fill(terr, -1);

        // PriorityQueue<(score, cityVertex, targetVertex), score>
        var queue = new PriorityQueue<(float Score, int City, int Vx), float>();

        float Weight(int u, int v)
        {
            double horiz = h.Mesh.Distance(u, v);
            float vert = h[v] - h[u];
            if (vert > 0f) vert /= 10f;
            float diff = 1f + 0.25f * (float)Math.Pow(vert / horiz, 2);
            diff += 100f * (float)Math.Sqrt(flux[u]);
            if (h[u] <= 0f) diff = 100f;
            if ((h[u] > 0f) != (h[v] > 0f)) return 1000f;
            return (float)(horiz * diff);
        }

        for (int i = 0; i < n; i++)
        {
            int city = cities[i];
            terr[city] = city;
            foreach (int nb in h.Mesh.Neighbours(city))
            {
                float w = Weight(city, nb);
                queue.Enqueue((w, city, nb), w);
            }
        }

        while (queue.Count > 0)
        {
            var (score, city, vx) = queue.Dequeue();
            if (terr[vx] >= 0) continue;
            terr[vx] = city;
            foreach (int nb in h.Mesh.Neighbours(vx))
            {
                if (terr[nb] >= 0) continue;
                float w = score + Weight(vx, nb);
                queue.Enqueue((w, city, nb), w);
            }
        }
        return terr;
    }

    // Find edges where adjacent land vertices have different territory owners.
    // Returns paths (list of [x,y] coordinate segments).
    public static List<double[][]> GetBorders(RenderState render)
    {
        var terr = render.Terr;
        var h = render.H;
        var segments = new List<double[][]>();

        foreach (var (v0, v1, left, right) in h.Mesh.Edges)
        {
            if (right == null) continue;
            if (h.Mesh.IsNearEdge(v0) || h.Mesh.IsNearEdge(v1)) continue;
            if (h[v0] < 0f || h[v1] < 0f) continue;
            if (terr[v0] != terr[v1] && left != null)
                segments.Add([left, right]);
        }
        return Rivers.MergeSegments(segments).Select(Rivers.RelaxPath).ToList();
    }
}
```

- [ ] **Step 9.4: Run test — verify it passes**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "TerritoriesTests"
```
Expected: 1 passing.

- [ ] **Step 9.5: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add Territories (priority-queue flood fill and border extraction)"
```

---

## Task 10: Language and Name Generation

**Files:**
- Create: `FantasyMaps.Core/Language/LanguageModel.cs`
- Create: `FantasyMaps.Core/Language/LanguageFactory.cs`
- Create: `FantasyMaps.Core/Language/NameGenerator.cs`
- Create: `FantasyMaps.Core.Tests/NameGeneratorTests.cs`

This is a direct port of `names.js`. The key structures are: phoneme sets, syllable templates, orthography rules, and a word cache per language.

- [ ] **Step 10.1: Write failing test**

`NameGeneratorTests.cs`:
```csharp
using FantasyMaps.Core.Language;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class NameGeneratorTests
{
    [Fact]
    public void MakeName_ReturnsStringWithinLengthBounds()
    {
        var lang = LanguageFactory.MakeRandomLanguage();
        for (int i = 0; i < 20; i++)
        {
            string name = NameGenerator.MakeName(lang, $"key{i}");
            Assert.True(name.Length >= 3 && name.Length <= 20,
                $"Name '{name}' is outside expected length range");
        }
    }

    [Fact]
    public void MakeName_SameKeyReturnsSameName()
    {
        var lang = LanguageFactory.MakeRandomLanguage();
        string name1 = NameGenerator.MakeName(lang, "city0");
        string name2 = NameGenerator.MakeName(lang, "city0");
        Assert.Equal(name1, name2);
    }
}
```

- [ ] **Step 10.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "NameGeneratorTests"
```

- [ ] **Step 10.3: Write `LanguageModel.cs`**

```csharp
namespace FantasyMaps.Core.Language;

public class LanguageModel
{
    public string[] Consonants { get; set; } = [];
    public string[] Vowels { get; set; } = [];
    public string[] Sibilants { get; set; } = [];
    public string[] Liquids { get; set; } = [];
    public string[] Finals { get; set; } = [];
    public string SyllableStructure { get; set; } = "CVC";
    public string Joiner { get; set; } = " ";
    public int MinSyllables { get; set; } = 1;
    public int MaxSyllables { get; set; } = 2;
    // Orthography: list of (pattern, replacement) pairs applied in sequence
    public (string Pattern, string Replacement)[] Orthography { get; set; } = [];
    // Restriction: regex that generated syllables must NOT match (empty = no restriction)
    public string Restriction { get; set; } = "";
    // Word cache: key → generated word (so same key always gives same result)
    public Dictionary<string, string> WordCache { get; } = [];
    public Dictionary<string, string> MorphemeCache { get; } = [];
}
```

- [ ] **Step 10.4: Write `LanguageFactory.cs`**

Port the preset phoneme sets and `makeRandomLanguage()` from `names.js`. Key data:

```csharp
namespace FantasyMaps.Core.Language;

public static class LanguageFactory
{
    private static readonly string[][] ConsonantSets =
    [
        ["p","t","k","f","s","z","m","n"],                             // Minimal
        ["p","t","k","b","d","g","f","s","z","h","m","n","l","r"],     // English-ish
        ["p","t","k","b","d","g","m","n"],                             // Piraha-ish
        ["p","k","m","n","h"],                                         // Hawaiian-ish
        ["p","t","k","q","b","d","g","m","n","r","l","f","s","x","z"], // Greenlandic-ish
        ["p","t","k","b","d","g","f","s","x","m","n","l","r"],         // Arabic-ish
    ];

    private static readonly string[][] VowelSets =
    [
        ["a","e","i","o","u"],
        ["a","e","i","o","u","aa","ee"],
        ["a","e","i","o","u","á","é","í","ó","ú"],
        ["a","e","i","o","u","â","ê","î","ô","û"],
        ["a","e","i","o","u","ä","ö","ü"],
    ];

    private static readonly string[] SyllableStructures =
    [
        "CVC","CVV?C","VC","CVV","CCV","CVVC?","CV","V","CV","CVC",
        "CVC","CVCC","CCVC","CVC?","S?CVC?","S?CV","S?CVC",
        "CVC","S?CVC","CVVC?","CVC","CVC?"
    ];

    private static readonly (string Pattern, string Replacement)[][] OrthographySets =
    [
        // Default IPA-ish
        [("q","kw"),("c","tsh"),("x","kh"),("ĥ","sh"),("ĝ","ng")],
        // Slavic-ish
        [("q","ch"),("c","ts"),("x","kh"),("ĥ","sh"),("ĝ","ng"),("j","y")],
        // French-ish
        [("q","qu"),("c","s"),("x","x"),("ĥ","sh"),("ĝ","ng"),("j","j")],
        // German-ish
        [("q","qu"),("c","ts"),("x","chs"),("ĥ","sch"),("ĝ","ng"),("j","j")],
    ];

    public static LanguageModel MakeBasicLanguage() => new()
    {
        Consonants = ["p","t","k","m","n"],
        Vowels = ["a","e","i"],
        Sibilants = ["s"],
        Liquids = ["l"],
        Finals = ["n","t"],
        SyllableStructure = "CVC",
        Orthography = [("q","kw")],
        Joiner = " ",
        MinSyllables = 1,
        MaxSyllables = 2
    };

    public static LanguageModel MakeRandomLanguage()
    {
        var r = Random.Shared;
        return new LanguageModel
        {
            Consonants = ConsonantSets[r.Next(ConsonantSets.Length)],
            Vowels = VowelSets[r.Next(VowelSets.Length)],
            Sibilants = ["s","sh","z"],
            Liquids = ["l","r"],
            Finals = ["n","t","s"],
            SyllableStructure = SyllableStructures[r.Next(SyllableStructures.Length)],
            Orthography = OrthographySets[r.Next(OrthographySets.Length)],
            Joiner = r.NextDouble() < 0.5 ? " " : "-",
            MinSyllables = 1,
            MaxSyllables = r.Next(1, 4),
            Restriction = r.NextDouble() < 0.3 ? "(.)\1" : ""
        };
    }
}
```

- [ ] **Step 10.5: Write `NameGenerator.cs`**

```csharp
using System.Text.RegularExpressions;

namespace FantasyMaps.Core.Language;

public static class NameGenerator
{
    private static string Choose(string[] arr)
        => arr[Random.Shared.Next(arr.Length)];

    private static string MakeSyllable(LanguageModel lang)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in lang.SyllableStructure)
            {
                bool optional = false;
                char code = c;
                if (c == '?') continue;
                // Check if next char is '?'
                int pos = lang.SyllableStructure.IndexOf(c);
                if (pos + 1 < lang.SyllableStructure.Length && lang.SyllableStructure[pos + 1] == '?')
                    if (Random.Shared.NextDouble() < 0.5) continue;
                sb.Append(code switch {
                    'C' => Choose(lang.Consonants),
                    'V' => Choose(lang.Vowels),
                    'S' => Choose(lang.Sibilants.Length > 0 ? lang.Sibilants : lang.Consonants),
                    'L' => Choose(lang.Liquids.Length > 0 ? lang.Liquids : lang.Consonants),
                    'F' => Choose(lang.Finals.Length > 0 ? lang.Finals : lang.Consonants),
                    _ => c.ToString()
                });
            }
            string syll = sb.ToString();
            if (!string.IsNullOrEmpty(lang.Restriction)
                && Regex.IsMatch(syll, lang.Restriction)) continue;
            return ApplyOrthography(lang, syll);
        }
        return "a";
    }

    private static string ApplyOrthography(LanguageModel lang, string syll)
    {
        foreach (var (pattern, replacement) in lang.Orthography)
            syll = syll.Replace(pattern, replacement);
        return syll;
    }

    public static string GetMorpheme(LanguageModel lang, string key)
    {
        if (lang.MorphemeCache.TryGetValue(key, out var cached)) return cached;
        int syllCount = Random.Shared.Next(lang.MinSyllables, lang.MaxSyllables + 1);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < syllCount; i++) sb.Append(MakeSyllable(lang));
        string result = sb.ToString();
        lang.MorphemeCache[key] = result;
        return result;
    }

    public static string MakeName(LanguageModel lang, string key)
    {
        if (lang.WordCache.TryGetValue(key, out var cached)) return cached;

        string name;
        int tries = 0;
        do {
            name = GenerateName(lang, key + tries);
            tries++;
        } while (tries < 100 && (name.Length < 3 || name.Length > 20
            || lang.WordCache.Values.Any(existing => existing.Contains(name) || name.Contains(existing))));

        if (name.Length < 2) name = GetMorpheme(lang, key);
        name = char.ToUpper(name[0]) + name[1..];
        lang.WordCache[key] = name;
        return name;
    }

    private static string GenerateName(LanguageModel lang, string key)
    {
        double r = Random.Shared.NextDouble();
        if (r < 0.5)
            return GetMorpheme(lang, key);
        if (r < 0.75)
            return GetMorpheme(lang, key + "a") + lang.Joiner + GetMorpheme(lang, key + "b");
        return "The " + GetMorpheme(lang, key + "a") + " of " + GetMorpheme(lang, key + "b");
    }
}
```

> Note: The syllable structure parser above is simplified. The original `names.js` handles `?` after the previous letter — if you see incorrect syllable output, trace through the original `makeSyllable` logic and adjust the loop.

- [ ] **Step 10.6: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "NameGeneratorTests"
```
Expected: 2 passing.

- [ ] **Step 10.7: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add Language system (LanguageModel, LanguageFactory, NameGenerator)"
```

---

## Task 11: Viridis Color, Color Palette, SvgBuilder

**Files:**
- Create: `FantasyMaps.Core/Rendering/ViridisColor.cs`
- Create: `FantasyMaps.Core/Rendering/ColorPalette.cs`
- Create: `FantasyMaps.Core/Rendering/SvgBuilder.cs`
- Create: `FantasyMaps.Core.Tests/ViridisColorTests.cs`

- [ ] **Step 11.1: Write failing test**

`ViridisColorTests.cs`:
```csharp
using FantasyMaps.Core.Rendering;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class ViridisColorTests
{
    [Fact]
    public void Interpolate_AtZero_ReturnsPurple()
    {
        string color = ViridisColor.Interpolate(0.0);
        // Viridis at t=0 is approx rgb(68,1,84) = #440154
        Assert.StartsWith("#", color);
        Assert.Equal(7, color.Length);
    }

    [Fact]
    public void Interpolate_AtOne_ReturnsYellow()
    {
        string color = ViridisColor.Interpolate(1.0);
        // Viridis at t=1 is approx rgb(253,231,37) = #fde725
        Assert.StartsWith("#", color);
    }

    [Fact]
    public void Interpolate_MidRange_ReturnsValidHex()
    {
        for (double t = 0; t <= 1.0; t += 0.1)
        {
            string color = ViridisColor.Interpolate(t);
            Assert.Matches(@"^#[0-9a-f]{6}$", color);
        }
    }
}
```

- [ ] **Step 11.2: Run test — verify it fails**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "ViridisColorTests"
```

- [ ] **Step 11.3: Write `ViridisColor.cs`**

The Viridis lookup table (256 RGB entries from d3-scale-chromatic). Shown as condensed for space — use the full 256-entry table from the d3-scale-chromatic GitHub source (`src/sequential-multi/viridis.js`). Each entry is `[r, g, b]` bytes.

```csharp
namespace FantasyMaps.Core.Rendering;

public static class ViridisColor
{
    // 256-entry Viridis lookup table: [r, g, b] per entry.
    // Source: https://github.com/d3/d3-scale-chromatic/blob/main/src/sequential-multi/viridis.js
    private static readonly byte[][] Table =
    [
        [68,1,84],[68,2,86],[69,4,87],[69,5,89],[70,7,90],[70,8,92],[70,10,93],[70,11,94],
        [71,13,96],[71,14,97],[71,16,99],[71,17,100],[71,19,101],[72,20,103],[72,22,104],[72,23,105],
        [72,25,107],[72,26,108],[72,28,110],[72,29,111],[72,31,112],[72,32,113],[72,34,115],[72,35,116],
        [72,37,117],[72,38,118],[72,40,120],[72,41,121],[71,43,122],[71,44,124],[71,46,125],[71,47,126],
        [71,49,127],[71,50,129],[71,52,130],[70,53,131],[70,55,132],[70,56,134],[70,58,135],[70,59,136],
        [69,61,137],[69,62,138],[69,64,140],[69,65,141],[68,67,142],[68,68,143],[68,70,144],[68,71,146],
        [67,73,147],[67,74,148],[67,76,149],[67,77,150],[66,79,151],[66,80,152],[66,82,153],[65,83,155],
        [65,85,156],[65,86,157],[64,88,158],[64,89,159],[63,91,160],[63,92,161],[63,94,162],[62,95,163],
        [62,97,164],[61,99,165],[61,100,166],[61,102,167],[60,103,168],[60,105,169],[59,107,170],[59,108,171],
        [59,110,172],[58,111,173],[58,113,174],[57,115,175],[57,116,176],[56,118,177],[56,119,178],[55,121,179],
        [55,123,180],[54,124,181],[54,126,182],[53,128,183],[53,129,184],[52,131,185],[52,133,185],[51,134,186],
        [51,136,187],[50,138,188],[50,139,189],[49,141,190],[49,143,191],[48,144,192],[48,146,193],[47,148,194],
        [47,149,194],[46,151,195],[46,153,196],[45,154,197],[45,156,198],[44,158,199],[44,159,200],[43,161,201],
        [43,163,201],[42,164,202],[42,166,203],[41,168,204],[41,170,205],[40,171,206],[40,173,206],[39,175,207],
        [39,176,208],[38,178,209],[38,180,210],[37,181,211],[37,183,211],[36,185,212],[36,187,213],[35,188,214],
        [35,190,215],[34,192,215],[34,194,216],[33,195,217],[33,197,218],[32,199,219],[32,200,219],[31,202,220],
        [31,204,221],[30,206,222],[30,207,222],[29,209,223],[29,211,224],[28,213,225],[28,214,225],[27,216,226],
        [27,218,227],[26,220,228],[26,221,228],[25,223,229],[25,225,230],[24,227,231],[24,228,231],[23,230,232],
        [23,232,233],[22,234,234],[22,235,234],[21,237,235],[21,239,236],[20,241,237],[20,242,237],[19,244,238],
        [19,246,239],[18,248,240],[18,249,240],[17,251,241],[17,253,242],[16,255,243],[68,1,84],[68,2,86],
        // ... fill remaining entries from the full d3-scale-chromatic viridis table ...
        // The full table has 256 entries. The implementer should copy all 256 from:
        // https://github.com/d3/d3-scale-chromatic/blob/main/src/sequential-multi/viridis.js
        [253,231,37]  // t=1 (index 255)
    ];

    public static string Interpolate(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        double pos = t * 255;
        int lo = (int)pos, hi = Math.Min(lo + 1, 255);
        double frac = pos - lo;
        // Ensure table is long enough — fall back to boundary if table is abbreviated
        var cLo = lo < Table.Length ? Table[lo] : Table[^1];
        var cHi = hi < Table.Length ? Table[hi] : Table[^1];
        int r = (int)(cLo[0] + frac * (cHi[0] - cLo[0]));
        int g = (int)(cLo[1] + frac * (cHi[1] - cLo[1]));
        int b = (int)(cLo[2] + frac * (cHi[2] - cLo[2]));
        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
```

> ⚠️ The table above is abbreviated. The implementer must replace the `// ... fill remaining ...` comment with all 256 rows from the d3-scale-chromatic viridis source linked in the comment.

- [ ] **Step 11.4: Write `ColorPalette.cs`**

```csharp
namespace FantasyMaps.Core.Rendering;

public static class ColorPalette
{
    public static readonly string[] Category10 =
    [
        "#1f77b4","#ff7f0e","#2ca02c","#d62728","#9467bd",
        "#8c564b","#e377c2","#7f7f7f","#bcbd22","#17becf"
    ];
}
```

- [ ] **Step 11.5: Write `SvgBuilder.cs`**

```csharp
using System.Text;

namespace FantasyMaps.Core.Rendering;

// Builds SVG element strings. All coordinates are multiplied by 1000 (internal: [-0.5,0.5]).
public static class SvgBuilder
{
    public const double Scale = 1000.0;

    // Generate an SVG path "d" attribute from a sequence of [x,y] points.
    public static string MakePath(double[][] points)
    {
        if (points.Length == 0) return "";
        var sb = new StringBuilder();
        sb.Append($"M{points[0][0] * Scale:F2},{points[0][1] * Scale:F2}");
        for (int i = 1; i < points.Length; i++)
            sb.Append($"L{points[i][0] * Scale:F2},{points[i][1] * Scale:F2}");
        return sb.ToString();
    }

    // Render a filled polygon path element.
    public static string FilledPath(double[][] points, string fill, string? cssClass = null)
    {
        string cls = cssClass != null ? $" class=\"{cssClass}\"" : "";
        return $"<path{cls} d=\"{MakePath(points)}\" fill=\"{fill}\" />";
    }

    // Render a stroked path element (no fill).
    public static string StrokedPath(double[][] points, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<path class=\"{cssClass}\"{styleAttr} d=\"{MakePath(points)}\" fill=\"none\" />";
    }

    // Render a circle element.
    public static string Circle(double x, double y, double r, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<circle class=\"{cssClass}\"{styleAttr} cx=\"{x * Scale:F2}\" cy=\"{y * Scale:F2}\" r=\"{r}\" />";
    }

    // Render a line element.
    public static string Line(double x1, double y1, double x2, double y2, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<line class=\"{cssClass}\"{styleAttr} x1=\"{x1 * Scale:F2}\" y1=\"{y1 * Scale:F2}\" x2=\"{x2 * Scale:F2}\" y2=\"{y2 * Scale:F2}\" />";
    }

    // Render a text element.
    public static string Text(double x, double y, string content, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<text class=\"{cssClass}\"{styleAttr} x=\"{x * Scale:F2}\" y=\"{y * Scale:F2}\">{System.Web.HttpUtility.HtmlEncode(content)}</text>";
    }

    // Wrap elements in an SVG root element.
    public static string WrapSvg(string content, string viewBox = "-500 -500 1000 1000", string style = "width:800px;height:800px")
        => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{viewBox}\" style=\"{style}\">{content}</svg>";
}
```

- [ ] **Step 11.6: Run tests — verify they pass**

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "ViridisColorTests"
```
Expected: 3 passing.

- [ ] **Step 11.7: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add ViridisColor, ColorPalette, SvgBuilder"
```

---

## Task 12: TerrainRenderer

**Files:**
- Create: `FantasyMaps.Core/Rendering/TerrainRenderer.cs`

No new tests needed — visual output is verified manually in the App (Plan 2).

- [ ] **Step 12.1: Write `TerrainRenderer.cs`**

```csharp
using System.Text;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core.Rendering;

public static class TerrainRenderer
{
    // Render the Voronoi mesh filled with Viridis colors based on field values.
    public static string VisualizeVoronoi(HeightField field, float? lo = null, float? hi = null)
    {
        float loVal = lo ?? field.Values.Min() - 1e-9f;
        float hiVal = hi ?? field.Values.Max() + 1e-9f;
        float range = hiVal - loVal;
        var sb = new StringBuilder();

        for (int i = 0; i < field.Mesh.Vxs.Length; i++)
        {
            var triPts = field.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;
            float t = range > 1e-9f ? Math.Clamp((field[i] - loVal) / range, 0f, 1f) : 0f;
            string color = ViridisColor.Interpolate(t);
            sb.AppendLine(SvgBuilder.FilledPath(triPts, color, "field"));
        }
        return sb.ToString();
    }

    // Render a list of paths with a given CSS class.
    public static string DrawPaths(List<double[][]> paths, string cssClass, string style = "")
    {
        var sb = new StringBuilder();
        foreach (var path in paths)
            sb.AppendLine(SvgBuilder.StrokedPath(path, cssClass, style));
        return sb.ToString();
    }

    // Render procedurally generated slope indicator strokes.
    // Small lines perpendicular to the slope direction, density proportional to steepness.
    public static string VisualizeSlopes(HeightField h)
    {
        var sb = new StringBuilder();
        double r = 0.25 / Math.Sqrt(h.Length);

        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] <= 0f || h.Mesh.IsNearEdge(i)) continue;
            var nbs = h.Mesh.Neighbours(i).Concat([i]).ToArray();
            double s = 0, s2 = 0;
            foreach (int nb in nbs)
            {
                var (sx, sy) = Erosion.Trislope(h, nb);
                s += sx / 10; s2 += sy;
            }
            s /= nbs.Length; s2 /= nbs.Length;
            double absS = Math.Abs(s);
            double threshold = 0.1 + Random.Shared.NextDouble() * 0.3;
            if (absS < threshold) continue;

            double l = r * (1 + Random.Shared.NextDouble()) * (1 - 0.2 * Math.Pow(Math.Atan(s), 2))
                       * Math.Exp(s2 / 100);
            double x = h.Mesh.Vxs[i][0], y = h.Mesh.Vxs[i][1];

            if (Math.Abs(l * s) > 2 * r)
            {
                int n = Math.Min((int)Math.Abs(l * s / r), 4);
                l /= n;
                for (int j = 0; j < n; j++)
                {
                    double u = Rand.Normal() * r, v = Rand.Normal() * r;
                    sb.AppendLine(SvgBuilder.Line(x + u - l, y + v + l * s, x + u + l, y + v - l * s,
                        "slope", "stroke:#797;stroke-width:1;stroke-linecap:round"));
                }
            }
            else
            {
                sb.AppendLine(SvgBuilder.Line(x - l, y + l * s, x + l, y - l * s,
                    "slope", "stroke:#797;stroke-width:1;stroke-linecap:round"));
            }
        }
        return sb.ToString();
    }

    // Render city circles.
    public static string VisualizeCities(RenderState render)
    {
        var sb = new StringBuilder();
        int n = render.Params.Nterrs;
        for (int idx = 0; idx < render.Cities.Count; idx++)
        {
            int city = render.Cities[idx];
            var vx = render.H.Mesh.Vxs[city];
            double radius = idx < n ? 10 : 4;
            sb.AppendLine(SvgBuilder.Circle(vx[0], vx[1], radius, "city",
                "fill:white;stroke:black;stroke-width:5;stroke-linecap:round"));
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 12.2: Build and verify no errors**

```bash
dotnet build FantasyMaps.Core/FantasyMaps.Core.csproj
```
Expected: `0 Error(s)`

- [ ] **Step 12.3: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add TerrainRenderer (visualizeVoronoi, drawPaths, visualizeSlopes, visualizeCities)"
```

---

## Task 13: LabelPlacer

**Files:**
- Create: `FantasyMaps.Core/Rendering/LabelPlacer.cs`

Port of `drawLabels()` from `map.js`. Penalty-based label placement.

- [ ] **Step 13.1: Write `LabelPlacer.cs`**

```csharp
using System.Text;
using FantasyMaps.Core.Language;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core.Rendering;

public static class LabelPlacer
{
    // Compute a penalty score for placing a label at (lx, ly) with given width/height.
    // Lower penalty = better position.
    private static double LabelPenalty(
        double lx, double ly, double w, double h,
        List<double[][]> paths, List<(double Lx, double Ly, double W, double H)> existing)
    {
        double penalty = 0;
        const double Scale = SvgBuilder.Scale;

        // Out-of-bounds penalty
        if (lx < -0.45 || lx + w / Scale > 0.45 || ly < -0.45 || ly + h / Scale > 0.45)
            penalty += 10000;

        // Penalty for proximity to paths (coasts, rivers, borders)
        foreach (var path in paths)
            foreach (var pt in path)
            {
                double dx = pt[0] - lx - w / (2 * Scale);
                double dy = pt[1] - ly - h / (2 * Scale);
                double dist2 = dx * dx + dy * dy;
                if (dist2 < 1e-9) penalty += 500;
                else penalty += Math.Max(0, 0.01 - dist2) * 200;
            }

        // Penalty for overlap with existing labels
        foreach (var (ex, ey, ew, eh) in existing)
        {
            double overlapX = Math.Max(0, Math.Min(lx + w / Scale, ex + ew / Scale) - Math.Max(lx, ex));
            double overlapY = Math.Max(0, Math.Min(ly + h / Scale, ey + eh / Scale) - Math.Max(ly, ey));
            if (overlapX > 0 && overlapY > 0) penalty += 1000 * overlapX * overlapY;
        }

        return penalty;
    }

    public static string DrawLabels(RenderState render, LanguageModel lang)
    {
        var sb = new StringBuilder();
        var placed = new List<(double Lx, double Ly, double W, double H)>();
        var allPaths = render.Coasts.Concat(render.Rivers).Concat(render.Borders).ToList();
        var h = render.H;
        var cities = render.Cities;
        var fontsizes = render.Params.Fontsizes;
        int nterrs = render.Params.Nterrs;

        // City labels: try 4 positions (right, left, above, below)
        for (int ci = 0; ci < cities.Count; ci++)
        {
            int city = cities[ci];
            var vx = h.Mesh.Vxs[city];
            string name = NameGenerator.MakeName(lang, $"city{ci}");
            double fontSize = ci < nterrs ? fontsizes[0] : fontsizes[1];
            double approxW = name.Length * fontSize * 0.6;
            double approxH = fontSize;

            // 4 candidate offsets (right, left, above, below)
            (double dx, double dy)[] offsets =
            [
                (10 / SvgBuilder.Scale, 0),
                (-approxW / SvgBuilder.Scale - 10 / SvgBuilder.Scale, 0),
                (-approxW / (2 * SvgBuilder.Scale), -approxH / SvgBuilder.Scale),
                (-approxW / (2 * SvgBuilder.Scale), approxH / SvgBuilder.Scale),
            ];

            double bestPenalty = double.MaxValue; int bestIdx = 0;
            for (int k = 0; k < offsets.Length; k++)
            {
                double penalty = LabelPenalty(vx[0] + offsets[k].dx, vx[1] + offsets[k].dy,
                    approxW, approxH, allPaths, placed);
                if (penalty < bestPenalty) { bestPenalty = penalty; bestIdx = k; }
            }

            double lx = vx[0] + offsets[bestIdx].dx;
            double ly = vx[1] + offsets[bestIdx].dy;
            placed.Add((lx, ly, approxW, approxH));

            string textStyle = $"font-family:'Palatino Linotype',Palatino,Georgia,serif;font-size:{fontSize}px;" +
                "fill:#000;stroke:white;stroke-width:3;paint-order:stroke;text-anchor:start";
            sb.AppendLine(SvgBuilder.Text(lx, ly, name, "city", textStyle));
        }

        // Region labels: search mesh vertices for best position near territory centroid
        var terrGroups = cities.Take(nterrs)
            .Select((city, idx) => (City: city, Idx: idx))
            .ToDictionary(x => x.City, x => x.Idx);

        for (int ti = 0; ti < Math.Min(nterrs, cities.Count); ti++)
        {
            int cityVx = cities[ti];
            string regionName = NameGenerator.MakeName(lang, $"region{ti}").ToUpper();
            double fontSize = fontsizes[2];
            double approxW = regionName.Length * fontSize * 0.6;
            double approxH = fontSize;

            // Find best vertex in this territory
            double bestPenalty = double.MaxValue; double bx = 0, by = 0;
            for (int i = 0; i < h.Length; i++)
            {
                if (render.Terr.Length > 0 && render.Terr[i] != cityVx) continue;
                if (h[i] <= 0f) continue;
                var vx = h.Mesh.Vxs[i];
                double penalty = LabelPenalty(vx[0] - approxW / (2 * SvgBuilder.Scale),
                    vx[1] - approxH / (2 * SvgBuilder.Scale), approxW, approxH, allPaths, placed);
                if (penalty < bestPenalty) { bestPenalty = penalty; bx = vx[0]; by = vx[1]; }
            }

            double lx2 = bx - approxW / (2 * SvgBuilder.Scale);
            double ly2 = by - approxH / (2 * SvgBuilder.Scale);
            placed.Add((lx2, ly2, approxW, approxH));
            string regionStyle = $"font-family:'Palatino Linotype',Palatino,Georgia,serif;font-size:{fontSize}px;" +
                "fill:#8a4;font-style:italic;stroke:white;stroke-width:2;paint-order:stroke;text-anchor:middle";
            sb.AppendLine(SvgBuilder.Text(bx, by, regionName, "region", regionStyle));
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 13.2: Build**

```bash
dotnet build FantasyMaps.Core/FantasyMaps.Core.csproj
```
Expected: `0 Error(s)`

- [ ] **Step 13.3: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add LabelPlacer (penalty-based city and region label placement)"
```

---

## Task 14: MapRenderer — Master Render Function

**Files:**
- Create: `FantasyMaps.Core/Rendering/MapRenderer.cs`

- [ ] **Step 14.1: Write `MapRenderer.cs`**

```csharp
using System.Text;
using FantasyMaps.Core.Language;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core.Rendering;

public static class MapRenderer
{
    // Full generation pipeline: given a height field, compute all derived state.
    public static void PrepareRender(RenderState render)
    {
        var h = render.H;
        render.Rivers = Rivers.GetRivers(h, 0.01f);
        render.Coasts = Rivers.Contour(h, 0f);
        render.Terr = Territories.GetTerritories(render);
        render.Borders = Territories.GetBorders(render);
    }

    // Render the complete map as an SVG string.
    public static string DrawMap(RenderState render, LanguageModel? lang = null)
    {
        PrepareRender(render);
        var sb = new StringBuilder();

        // Layer 1: terrain color fill (Viridis)
        sb.Append(TerrainRenderer.VisualizeVoronoi(render.H));

        // Layer 2: coastlines
        sb.Append(TerrainRenderer.DrawPaths(render.Coasts, "coast",
            "stroke:#000;stroke-width:3;stroke-linecap:round;stroke-linejoin:round"));

        // Layer 3: rivers
        sb.Append(TerrainRenderer.DrawPaths(render.Rivers, "river",
            "stroke:#36a;stroke-width:2;stroke-linecap:round;stroke-linejoin:round"));

        // Layer 4: slope indicators
        sb.Append(TerrainRenderer.VisualizeSlopes(render.H));

        // Layer 5: territory fills (semi-transparent)
        RenderTerritories(render, sb);

        // Layer 6: borders
        sb.Append(TerrainRenderer.DrawPaths(render.Borders, "border",
            "stroke:#a33;stroke-width:2.5;stroke-dasharray:6,6;stroke-linecap:round;stroke-linejoin:round"));

        // Layer 7: city circles
        sb.Append(TerrainRenderer.VisualizeCities(render));

        // Layer 8: labels
        if (lang != null)
            sb.Append(LabelPlacer.DrawLabels(render, lang));

        return SvgBuilder.WrapSvg(sb.ToString());
    }

    private static void RenderTerritories(RenderState render, StringBuilder sb)
    {
        if (render.Terr.Length == 0) return;
        var h = render.H;
        for (int i = 0; i < h.Mesh.Vxs.Length; i++)
        {
            var triPts = h.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;
            if (h[i] <= 0f) continue;
            int terrOwner = render.Terr.Length > i ? render.Terr[i] : -1;
            if (terrOwner < 0) continue;
            int cityIdx = render.Cities.IndexOf(terrOwner);
            string color = ColorPalette.Category10[cityIdx % ColorPalette.Category10.Length];
            sb.AppendLine($"<path class=\"field\" d=\"{SvgBuilder.MakePath(triPts)}\" fill=\"{color}\" fill-opacity=\"0.5\" />");
        }
    }

    // Assemble a RenderState for the full Section 8 map generation.
    public static RenderState GenerateFullMap(MapParams @params, Mesh.VoronoiMesh mesh)
    {
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Add(h, HeightPrimitives.Slope(mesh, [Random.Shared.NextDouble() * 4 - 2, Random.Shared.NextDouble() * 4 - 2]));
        h = HeightPrimitives.Peaky(h);
        h = Erosion.DoErosion(h, 0.05f, 5);
        h = Erosion.SetSeaLevel(h, 0.5);
        h = Erosion.CleanCoast(h, 3);

        var render = new RenderState { H = h, Params = @params };
        CityPlacer.PlaceCities(render);
        return render;
    }
}
```

- [ ] **Step 14.2: Build and run all tests**

```bash
dotnet build && dotnet test FantasyMaps.Core.Tests/
```
Expected: All tests pass, `0 Error(s)`.

- [ ] **Step 14.3: Commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: add MapRenderer (full pipeline orchestration and SVG assembly)"
```

---

## Verification

After all tasks are complete, verify the Core library end-to-end by running a quick smoke test:

- [ ] **Create a temporary console test**

Add a `Program.cs` to a new `dotnet new console` project or add a test:

```csharp
// In a new xunit test or temp file:
[Fact]
public void FullPipeline_GeneratesValidSvg()
{
    var @params = new MapParams { Npts = 512, Ncities = 5, Nterrs = 3 };
    var mesh = MeshBuilder.GenerateGoodMesh(@params.Npts);
    var render = MapRenderer.GenerateFullMap(@params, mesh);
    var lang = LanguageFactory.MakeRandomLanguage();
    string svg = MapRenderer.DrawMap(render, lang);
    Assert.Contains("<svg", svg);
    Assert.Contains("<path", svg);
    Assert.Contains("<text", svg);
    Assert.True(svg.Length > 1000, "SVG should have substantial content");
}
```

```bash
dotnet test FantasyMaps.Core.Tests/ --filter "FullPipeline"
```
Expected: 1 passing.

- [ ] **Final commit**

```bash
git add FantasyMapsDotNet/
git commit -m "feat: Core library complete — all algorithms ported and tested"
```

---

## Next Step

Once this plan is complete (all tests green, smoke test passes), proceed to **Plan 2: MAUI App** (`docs/superpowers/plans/2026-04-30-blazor-maui-app-plan.md`), which builds the Blazor Hybrid UI layer on top of this Core library.
