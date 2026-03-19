# Current Context

## Confirmed Direction

- The project is about physically credible water behavior in transparent 3D containers.
- The current minimum Unity prototype is a transparent square glass container with Zibra liquid inside.
- The liquid should remain readable through the glass while colliding with the rotating inner walls.
- Input should use a trackball-style container control.
- The browser prototype has done its job as the minimum testable interaction baseline.
- The next implementation stack is `Unity + C# + URP + Zibra Liquid`.

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
- The browser approximation will be preserved as reference history instead of being extended into the realism phase.
- The realism phase should use GPU-driven liquid simulation in Unity, with C# owning gameplay and control logic while Zibra Liquid handles the core fluid simulation.
- Desktop feel validation comes before mobile optimization.
- A Unity project now exists under `prototype/engine/unity` with `URP`, a bootstrap scene, a six-wall glass container, liquid controls, reset flow, and a playable Zibra-based test scene.
- For editor stability on the current machine, the Unity project should be launched with `-force-d3d11`.
- The current visible glass solution uses thin wall geometry rather than coplanar transparent quads.
- A hidden reflection rig is now part of the scene to keep glass and liquid reflections readable without putting bright helper boards in the gameplay view.
- A tighter fixed-step leakage-hardening experiment was tried and then reverted because it hurt feel and still failed to stop aggressive high-speed leakage.

## Open Questions

- Whether the current `Unity + Zibra` path is good enough if the project requires sealed-container behavior under violent fast shaking with no visible leakage.
- Which liquid parameters produce the most convincing glass-wall adhesion, settle behavior, and water volume for the square container target.
- What minimum mobile target should be considered after the desktop prototype proves the feel.
- Which measurable criteria should gate the move from prototype feel validation to production planning.

## Suggested Next Tasks

- Capture and review short videos of slow, medium, and aggressive rotations in the Unity scene.
- Validate four core behaviors: lag under rotation, wall accumulation, readable merge behavior, and natural settling after release.
- Decide whether extreme fast-motion no-leakage behavior is a prototype requirement or a future production-solver requirement.
- Research solver and coupling approaches that are better suited to fast moving closed solid boundaries.
- Set measurable visual and physical validation criteria.
