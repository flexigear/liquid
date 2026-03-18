# Current Context

## Confirmed Direction

- The project is about physically credible water behavior in transparent 3D containers.
- The current minimum prototype is a transparent square glass container with water drops.
- The liquid should merge into larger drops while colliding with the glass walls.
- Input should use a trackball-style container control.

## What Has Been Decided

- Realism is a priority over arcade abstraction.
- The player manipulates the container, not the water directly.
- The first milestone is not a level; it is a convincing liquid interaction prototype.
- A layered memory system is now part of the project foundation.
- The Git repository has been initialized and pushed to GitHub.
- The repository now includes baseline setup files and a neutral prototype workspace.
- The first minimal testable implementation stack is a dependency-free web prototype under `prototype/engine/web`.
- The current prototype approximates water as wall-bound droplets on the supporting inner face, driven by gravity, angular inertia, and merge-by-volume behavior.
- The current browser prototype has been tuned to reduce unrealistic rapid sliding by smoothing support gravity, reducing inertial forcing, and adding wall-pinning thresholds.

## Open Questions

- Whether the browser prototype is sufficient for the next phase of feel validation.
- How far to push realism on mobile versus desktop targets.
- Whether the next realism step should be continuous wall-constrained motion before moving to a fuller 3D fluid simulation.

## Suggested Next Tasks

- Play-test and tune the current web prototype.
- Verify whether wall pinning now feels plausible under slow and medium trackball motion.
- If rapid sliding still feels wrong, replace face-bound wall motion with a continuous inner-surface flow model.
- Decide when to move to Unity, Godot, or a custom simulation stack.
- Set measurable visual and physical validation criteria.