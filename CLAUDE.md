# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A single-file interactive tutorial for procedural fantasy map generation, based on Martin O'Leary's [mewo2/terrain](https://github.com/mewo2/terrain) algorithm. The entire application lives in `index.html` with no build step.

## Running the Project

Open `index.html` directly in a browser — no server required. The only external dependency is D3.js v4, loaded from CDN.

## Architecture

All code is in `index.html`, organized into four logical sections:

**1. PriorityQueue** — Custom min-heap used for territory expansion (Dijkstra-style flood fill from city nodes).

**2. Language generation** (`makeRandomLanguage`, `makeName`) — Procedural name generator. Creates phoneme sets, syllable structures, and orthography rules per-map. `makeName(lang, key)` generates city/region names; `key` biases word morphemes toward a semantic role.

**3. Terrain engine** — Core algorithm pipeline:
- `generateGoodMesh(n)` → Voronoi mesh with Lloyd-relaxed points
- Height fields are plain arrays with a `.mesh` property attached (`h.mesh`)
- Primitives: `slope`, `cone`, `mountains` — combined with `add()`
- `fillSinks` → `erode` → `fillSinks` cycle simulates hydraulic erosion
- `getFlux(h)` computes water flow (used for rivers and city scoring)
- `getTerritories(render)` expands from city nodes using a weighted priority queue

**4. Visualization** — D3 v4 SVG rendering:
- Internal coordinates are normalized `[-0.5, 0.5]`; multiplied by 1000 for SVG (viewBox `-500 -500 1000 1000`)
- `visualizeVoronoi` fills mesh triangles using `d3.interpolateViridis`
- `drawMap(svg, render)` is the top-level render call for the complete map
- `drawLabels` scores candidate label positions using a penalty function to avoid overlap with paths and other labels

**Render state object** passed through visualization functions:
```js
render = {
  h,        // height field
  cities,   // array of vertex indices
  params,   // { npts, ncities, nterrs, fontsizes, extent }
  rivers,   // merged path segments
  coasts,   // contour at h=0
  borders,  // territory boundary paths
  terr      // vertex→city mapping
}
```

**Interactive demo state** — Each tutorial section has its own state variable (`sharedMesh`, `hmHeight`, `erHeight`, `renderH`, `cityRender`). Sections lazily initialize from earlier stages via `ensure*()` helpers.

## Key Conventions

- Height field arrays carry a `.mesh` reference — always preserve this when creating derived arrays (use `zero(mesh)` as a base, or the `map(h, f)` helper which propagates `.mesh`).
- SVG coordinate scale: multiply all terrain coordinates by 1000 when setting `cx`/`cy`/`x`/`y` attributes.
- Sea level is 0: `h[i] > 0` is land, `h[i] <= 0` is ocean.
- `isedge` / `isnearedge` guard against boundary artifacts — always check these before operating on vertices near the map edge.
