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
- Supporting gravity is smoothed over time before it is used for face selection and droplet motion.
- Each droplet moves in face coordinates under projected gravity plus a reduced angular-acceleration term.
- Small wall-parallel forces are suppressed by a pinning-style threshold so droplets do not immediately slide under minor container motion.
- Droplets attract each other on the same face and merge by conserved volume.
- Face transfer uses an edge-based approximation plus a short visual transition rather than true continuous flow over the mesh.
## Why This Exists
This model is not the final realism target. It exists to validate hand-feel quickly without committing to a full fluid solver.
## Known Gaps
- Droplets do not yet form a continuous free-surface body.
- Face-to-face transfer is still an approximation, not a full edge-flow simulation.
- The prototype does not model films, wet trails, or true detachment from the wall.
- Motion is more stable now, but wall travel is still face-bound rather than continuously constrained over the full inner surface.
- Rendering is stylized enough to read, but not physically based.
## Recent Tuning
- Increased wall drag to damp excessive sliding.
- Reduced attraction strength to avoid droplets yanking each other unnaturally during transitions.
- Reduced angular inertia contribution and clamped maximum surface acceleration.
- Added static and kinetic slip thresholds plus a settle threshold to approximate contact-angle pinning on glass.
## Usefulness
This prototype is still useful for testing:
- trackball control feel
- container readability
- gather-and-merge pacing
- whether the core interaction is satisfying before a heavier simulation investment
