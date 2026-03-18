# Core Gameplay Loop

## Current Loop

1. The player rotates a transparent container.
2. Liquid drops slide, cling, collide, and merge under gravity and inertia.
3. The player uses vessel motion to gather or position the liquid.
4. The level goal is satisfied by moving enough liquid through a path or onto a target region.

## Current Level Unit

One container equals one level.

This keeps the play space legible and lets each vessel shape define a distinct puzzle.

## Early Goal Direction

The first prototype only needs believable liquid motion and merging.

Future goals can include:

- Gather enough liquid into one body.
- Move liquid through a narrow route.
- Wet specific target locations.
- Leave precise amounts in different chambers.

## Design Implication

The vessel itself is level design. Internal geometry, wall properties, and route shape should do more work than external UI systems.
