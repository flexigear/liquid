# Current Context

## Confirmed Direction

- The project is about physically credible water behavior in transparent 3D containers.
- The current Unity work now has two paths: `Zibra` as the earlier GPU liquid baseline and `SPlisHSPlasH` as the stricter sealed-container route.
- The liquid should remain readable through the glass while colliding with the rotating inner walls.
- Input should use a trackball-style container control.
- The browser prototype has done its job as the minimum testable interaction baseline.
- The current high-speed sealed-container attempt is `Unity + C# + URP + native SPlisHSPlasH bridge`.

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
- A technical survey has been created for high-speed fluid-solid coupling alternatives, and the first `SPlisHSPlasH` attempt has already reached a successful local build plus first custom shaking-cube scene.
- The second-ranked `SPHinXsys` route has now reached successful local configure, local dependency installation, example build, bundled-example runtime, and a first custom shaking-square route-2 test on the current machine.
- `SPlisHSPlasH` has now been integrated back into Unity through a native bridge and can be driven interactively in a realtime scene.
- The current `SPlisHSPlasH` realtime path uses about `5292` particles and is CPU-side rather than GPU fluid simulation.
- The current `SPlisHSPlasH` visual layer is no longer raw blue dots; it now uses a first custom screen-space-style water surface composite over a particle mask.
- The `Zibra` route is currently paused while the `SPlisHSPlasH` route is evaluated for stricter sealed-container behavior.

## Open Questions

- Whether the current `SPlisHSPlasH` realtime route can be pushed far enough in performance and visual quality to replace `Zibra` for the main prototype.
- Whether the current `Unity + Zibra` path is still worth keeping as the better-looking baseline if strict no-leakage is relaxed.
- Which of the surveyed alternatives should replace or supplement the current Unity path if `SPlisHSPlasH` remains too CPU-heavy.
- Which liquid parameters produce the most convincing glass-wall adhesion, settle behavior, and water volume for the square container target.
- What minimum mobile target should be considered after the desktop prototype proves the feel.
- Which measurable criteria should gate the move from prototype feel validation to production planning.

## Suggested Next Tasks

- Tune the new `SPlisHSPlasH` liquid surface renderer so it reads as water rather than a mask-based overlay.
- Reduce occasional hitches when particles slam into the cube walls.
- Decide whether to move the current Unity liquid display path toward GPU instancing or a fuller screen-space fluid pipeline.
- Compare the current `SPlisHSPlasH` visual result against the paused `Zibra` baseline and decide whether the solver gain is worth the rendering and performance cost.
- Keep `SPHinXsys` as the next reference route, but do not continue route exploration until the current `SPlisHSPlasH` Unity route is judged.
- Set measurable visual and physical validation criteria.
