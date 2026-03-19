# Liquid

`Liquid` is a game project about physically credible water motion inside transparent 3D containers.

The first completed playable target is a browser-based interaction prototype with:

- one transparent square glass container
- several water drops inside the container
- trackball-style rotation control
- droplets that slide on the inner glass, react to angular motion, merge into larger drops, and settle with visible inertia

## Current Status

The repository now includes:

- a completed dependency-free web prototype that validated the square-glass-container hand-feel
- a layered memory system that tracks product and simulation decisions
- a live Unity prototype workspace for the next realism step: `Unity + C# + URP + Zibra Liquid`
- a working local Unity scene with Zibra liquid, trackball container control, reset flow, and a readable square-glass-container presentation

The public repository does not include the commercial `Zibra Liquid` plugin files. Import that plugin locally after cloning if you want the Unity prototype to compile and run.

## Repository Layout

- [MEMORY.md](/D:/myWorkSpace/appProjects/liquid/MEMORY.md): entry point to the layered memory system
- [memory/](/D:/myWorkSpace/appProjects/liquid/memory): long-lived project memory, research, design, and active context
- [scripts/](/D:/myWorkSpace/appProjects/liquid/scripts): helper scripts for memory lookup and local prototype serving
- [prototype/engine/web](/D:/myWorkSpace/appProjects/liquid/prototype/engine/web): completed browser MVP used to validate hand-feel quickly
- [prototype/engine/unity](/D:/myWorkSpace/appProjects/liquid/prototype/engine/unity): next implementation target for GPU-driven liquid simulation

## Run The Web Prototype

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

Validate the current Unity liquid scene across slow, medium, and aggressive motion, document the acceptable motion envelope, and decide whether the extreme high-speed sealed-container requirement is still inside the current `Unity + Zibra` path. Preserve the browser prototype as a historical baseline rather than extending it further.
