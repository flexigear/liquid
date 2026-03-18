# Web Prototype Implementation

## Purpose

Record what the current dependency-free browser prototype actually simulates, so later iterations can keep or replace its assumptions deliberately.

## Current Stack

- Plain HTML, CSS, and JavaScript
- Canvas 2D rendering
- No external runtime dependencies

## Current Approximation

- The glass container is rendered as a rotating transparent cube.
- Trackball-style pointer input rotates the container through a target rotation plus smoothing.
- Water is approximated as droplets attached to the currently supporting inner face.
- Each droplet moves in face coordinates under projected gravity, angular acceleration, centrifugal terms, and Coriolis-like drag terms.
- Droplets attract each other on the same face and merge by conserved volume.

## Why This Exists

This model is not the final realism target. It exists to validate hand-feel quickly without committing to a full fluid solver.

## Known Gaps

- Droplets do not yet form a continuous free-surface body.
- Face-to-face transfer is an approximation, not a full edge-flow simulation.
- The prototype does not model films, wet trails, or true detachment from the wall.
- Rendering is stylized enough to read, but not physically based.

## Usefulness

This prototype is still useful for testing:

- trackball control feel
- container readability
- gather-and-merge pacing
- whether the core interaction is satisfying before a heavier simulation investment
