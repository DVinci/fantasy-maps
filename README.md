# Fantasy Maps

An interactive, browser-based tutorial for procedural fantasy map generation. Each section of the page lets you explore a different stage of the algorithm — from raw Voronoi mesh construction through erosion simulation, city placement, and territory expansion — ending with a fully labelled fantasy map you can export as SVG.

Based on Martin O'Leary's [mewo2/terrain](https://github.com/mewo2/terrain) algorithm and [mewo2/naming-language](https://github.com/mewo2/naming-language) for procedural place names.

## Running locally

No build step. Open `index.html` directly in a browser, or serve it over HTTP (required if your browser blocks local file access to the D3 CDN):

```bash
npm run serve        # http://localhost:5500
```

### VS Code

Press **F5** and select a launch configuration:

| Configuration | Opens in |
|---|---|
| Fantasy Maps (VS Code Browser) | VS Code Simple Browser panel |
| Fantasy Maps (External Browser) | System default browser |

> Requires Node.js. The server starts automatically on port 5500.

## How it works

The map is generated through a pipeline of stages, each interactive:

1. **Random points** — uniform random points, improved with one round of Lloyd relaxation into a well-spaced grid
2. **Voronoi mesh** — Delaunay triangulation and Voronoi diagram; Voronoi vertices become the computational graph
3. **Height map** — additive primitives (slope, cone, Gaussian blobs) build up a scalar height field
4. **Erosion** — fill sinks → compute water flux → erode proportional to flux × slope, repeated
5. **Rendering** — coastline contours at h=0, rivers where flux exceeds a threshold, slope hatch marks
6. **Cities** — scored by flux (trade), distance from other cities, and map centrality
7. **Territories** — Dijkstra-style flood fill from city nodes, weighted by terrain cost
8. **Labels** — penalty-function layout avoids overlap with paths, cities, and other labels

The coordinate system is normalised to `[-0.5, 0.5]` internally and scaled by `1000` for SVG output (viewBox `-500 -500 1000 1000`).

## Testing

Requires Node.js.

```bash
npm install
npx playwright install chromium

npm test               # headless
npm run test:headed    # watch the browser
npm run test:ui        # Playwright interactive UI
```

The test suite covers all eight interactive sections: page load, Voronoi mesh, height map editing, coastline/erosion, city placement, and full map generation with labelled SVG output. The dev server starts automatically before each test run.

## Credits

- Terrain algorithm: [Martin O'Leary](https://github.com/mewo2) — [mewo2/terrain](https://github.com/mewo2/terrain), MIT License
- Language generation: [mewo2/naming-language](https://github.com/mewo2/naming-language)
- Voronoi / D3: [d3/d3](https://github.com/d3/d3) v4, ISC License
