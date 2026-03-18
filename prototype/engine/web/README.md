# Web Prototype

This is a dependency-free browser prototype for the current MVP.

## Run

Fastest path:

1. Open `index.html` in a browser.

Or serve it locally:

```powershell
cd D:\myWorkSpace\appProjects\liquid\prototype\engine\web
python -m http.server 8123
```

Then open `http://localhost:8123`.

## Controls

- Drag on the viewport to rotate the glass cube with a virtual trackball.
- `Reset Drops` restores the initial droplet layout.
- `Scatter` throws the droplets apart so you can test re-gathering.

## Current Scope

- Transparent square glass container
- Trackball-style container rotation
- Inner-wall droplet sliding
- Apparent gravity plus angular inertia
- Volume-conserving droplet merging
