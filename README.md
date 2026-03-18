# Liquid

`Liquid` is a game project about physically credible water motion inside transparent 3D containers.

The current playable target is a browser-based interaction prototype with:

- one transparent square glass container
- several water drops inside the container
- trackball-style rotation control
- droplets that slide on the inner glass, react to angular motion, merge into larger drops, and settle with visible inertia

## Current Status

The repository now includes a dependency-free web prototype for the square-glass-container MVP, alongside the layered memory system that tracks product and simulation decisions.

## Repository Layout

- [MEMORY.md](/D:/myWorkSpace/appProjects/liquid/MEMORY.md): entry point to the layered memory system
- [memory/](/D:/myWorkSpace/appProjects/liquid/memory): long-lived project memory, research, design, and active context
- [scripts/](/D:/myWorkSpace/appProjects/liquid/scripts): helper scripts for memory lookup and local prototype serving
- [prototype/engine/web](/D:/myWorkSpace/appProjects/liquid/prototype/engine/web): current minimal testable prototype

## Run The Prototype

Open [index.html](/D:/myWorkSpace/appProjects/liquid/prototype/engine/web/index.html) directly in a browser, or run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-web-prototype.ps1
```

Then open `http://localhost:8123`.

## Memory Workflow

Before starting a task:

1. Read [memory/00-system/task-load-map.md](/D:/myWorkSpace/appProjects/liquid/memory/00-system/task-load-map.md).
2. Or run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\get-memory.ps1 -Task "fluid physics trackball prototype"
```

3. Update the lowest sensible memory layer when a new conclusion is reached.

## Next Milestone

Push the prototype closer to convincing liquid behavior by refining face transitions, wall adhesion, and visual deformation, or move to a heavier simulation stack when the browser model stops being informative.
