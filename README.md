# Liquid

`Liquid` is a game project about physically credible water motion inside transparent 3D containers.

The current target is a first playable interaction prototype:

- one transparent square glass container
- several water drops inside the container
- trackball-style rotation control
- drops that slide, collide with glass, merge into larger drops, and settle with visible inertia

## Current Status

The repository is in pre-engine bootstrap stage. Product direction, prototype scope, and current design conclusions are already captured in the memory system.

## Repository Layout

- [MEMORY.md](/D:/myWorkSpace/appProjects/liquid/MEMORY.md): entry point to the layered memory system
- [memory/](/D:/myWorkSpace/appProjects/liquid/memory): long-lived project memory, research, design, and active context
- [scripts/](/D:/myWorkSpace/appProjects/liquid/scripts): helper scripts, including task-based memory lookup
- [prototype/](/D:/myWorkSpace/appProjects/liquid/prototype): neutral workspace for future prototype implementation

## Memory Workflow

Before starting a task:

1. Read [memory/00-system/task-load-map.md](/D:/myWorkSpace/appProjects/liquid/memory/00-system/task-load-map.md).
2. Or run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\get-memory.ps1 -Task "fluid physics trackball prototype"
```

3. Update the lowest sensible memory layer when a new conclusion is reached.

## Next Milestone

Choose the first engine and simulation stack, then build a convincing square-glass-container prototype.
