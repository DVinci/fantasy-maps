# Fantasy Maps

An interactive tutorial for procedural fantasy map generation. Each section lets you explore a different stage of the algorithm — from raw Voronoi mesh construction through erosion simulation, city placement, and territory expansion — ending with a fully labelled fantasy map you can export as SVG.

Based on Martin O'Leary's [mewo2/terrain](https://github.com/mewo2/terrain) algorithm and [mewo2/naming-language](https://github.com/mewo2/naming-language) for procedural place names.

Two implementations are included: the original browser-based JavaScript version, and a full port to .NET 9 as a MAUI Hybrid Blazor desktop application.

---

## JavaScript version

No build step. Open `index.html` directly in a browser, or serve it over HTTP:

```bash
npm run serve        # http://localhost:5500
```

### VS Code

Press **F5** and select a launch configuration:

| Configuration | Opens in |
|---|---|
| Fantasy Maps (VS Code Browser) | VS Code Simple Browser panel |
| Fantasy Maps (External Browser) | System default browser |

> Requires Node.js.

### Testing (JavaScript)

```bash
npm install
npx playwright install chromium

npm test               # headless
npm run test:headed    # watch the browser
npm run test:ui        # Playwright interactive UI
```

---

## .NET 9 port (`FantasyMapsDotNet/`)

A faithful port to C# with zero JavaScript. All terrain logic, SVG rendering, and label placement run in pure .NET. The UI is a MAUI Hybrid Blazor app targeting Windows.

### Projects

| Project | Description |
|---|---|
| `FantasyMaps.Core` | Pure C# class library — mesh, terrain, erosion, language, rendering |
| `FantasyMaps.App` | .NET 9 MAUI Hybrid Blazor desktop app (Windows) |
| `FantasyMaps.Web` | ASP.NET Core Blazor Server harness (used for Playwright testing) |
| `FantasyMaps.Core.Tests` | xUnit unit tests for the Core library |
| `FantasyMaps.UITests` | Playwright NUnit end-to-end tests against the Web harness |

### Running the desktop app

```bash
cd FantasyMapsDotNet
dotnet build FantasyMaps.App/FantasyMaps.App.csproj -f net9.0-windows10.0.19041.0
```

Or press **F5** in VS Code with the **Fantasy Maps .NET (MAUI App)** launch configuration (requires the C# Dev Kit extension). The launch config automatically stops any running instance and rebuilds before launching.

### Running the web test harness

```bash
cd FantasyMapsDotNet
dotnet run --project FantasyMaps.Web --urls http://localhost:5100
```

Or use the **Fantasy Maps .NET (Web Harness)** VS Code launch configuration.

### Testing (.NET)

```bash
cd FantasyMapsDotNet

# Core unit tests (21 tests)
dotnet test FantasyMaps.Core.Tests

# Playwright UI tests (starts the web harness automatically)
dotnet test FantasyMaps.UITests
```

### Section 8 — Full map controls

The full map generator exposes two user controls:

- **Quality** — mesh resolution: Low (512), Medium (2048), High (4096, default), Ultra (16384 — matches JS original)
- **Terrain** slider (Rugged → Smooth) — number of height-field relaxation passes (0–15, default 5); higher values produce smoother coastlines and more continental landmasses

---

## How it works

The map is generated through a pipeline of stages, each interactive:

1. **Random points** — uniform random points improved with Lloyd relaxation into a well-spaced grid
2. **Voronoi mesh** — Delaunay triangulation and dual Voronoi diagram; Voronoi vertices become the computational graph
3. **Height map** — additive primitives (slope, cone, Gaussian blobs) build up a scalar height field
4. **Erosion** — fill sinks → compute water flux → erode proportional to flux × slope, repeated
5. **Rendering** — coastline contours at h=0, rivers where flux exceeds a threshold, slope hatch marks
6. **Cities** — scored by flux (trade), distance from other cities, and map centrality
7. **Territories** — Dijkstra-style flood fill from city nodes, weighted by terrain cost
8. **Labels** — penalty-function layout avoids overlap with paths, cities, and other labels

The coordinate system is normalised to `[-0.5, 0.5]` internally and scaled by `1000` for SVG output (viewBox `-500 -500 1000 1000`).

---

## Credits

- Terrain algorithm: [Martin O'Leary](https://github.com/mewo2) — [mewo2/terrain](https://github.com/mewo2/terrain), MIT License
- Language generation: [mewo2/naming-language](https://github.com/mewo2/naming-language)
- Voronoi / D3: [d3/d3](https://github.com/d3/d3) v4, ISC License
- .NET Voronoi: [Triangle.NET](https://github.com/wo80/Triangle.NET) by Christian Woltering
