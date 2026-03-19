# Unity SPlisHSPlasH Prototype

## Current Purpose

Use `SPlisHSPlasH` as the strict-boundary liquid solver inside the Unity prototype when the project needs stronger sealed-container behavior than the current `Zibra` route can provide under aggressive motion.

## Current Stack

- `Unity 6000.3.11f1`
- `C#`
- `Universal Render Pipeline`
- native `SPlisHSPlasH` bridge DLL under `prototype/engine/unity/Assets/Plugins/x86_64`
- bridge source under `prototype/engine/unity/native/splishsplash_bridge`
- launch Unity with `-force-d3d11` on the current machine

## Runtime Architecture

- `ContainerCommand` captures mouse drag and produces trackball-like target rotation plus angular velocity.
- `ContainerPivot` displays the solver-owned rigid body pose.
- `SPlisHSPlasHRealtimeController` steps the native solver and copies back particle positions plus the actual container pose.
- `LiquidCubeRealtimeBridge.json` defines the current closed dynamic wall cube scene for the solver.
- The current realtime step target is `1/240` with up to `8` substeps per rendered frame.

## Current Visual Stack

- Raw blue particle rendering has been replaced by a two-stage liquid display.
- The realtime particle system now writes a fluid mask using `PrototypeFluidMaskParticle.shader`.
- `SPlisHSPlasHFluidSurfaceRenderer` captures that mask to a render texture and composites a continuous screen-space water surface using `PrototypeFluidSurfaceComposite.shader`.
- This is now visibly more water-like than the old billboard-only view, but it is still an early custom surface renderer and not yet at `Zibra` polish or performance.

## Current Solver And Performance Facts

- Current realtime particle count: about `5292`.
- The current `SPlisHSPlasH` Unity route is CPU-side, not GPU fluid simulation.
- The native bridge build uses `OpenMP` and `AVX2`, but not CUDA, DirectCompute, or another dedicated GPU solver path.
- Unity still pays a per-step native-to-managed particle copy cost before drawing.

## What Works

- Realtime Unity interaction is working.
- The solver now responds to container dragging inside Unity instead of only offline playback.
- The container and fluid are now coupled strongly enough that ordinary motion keeps the fluid constrained inside the cube.
- The previous hard crashes from aggressive motion have been reduced significantly compared with earlier realtime bridge attempts.

## Current Limits

- Strong wall impacts can still cause visible frame hitches.
- Under stronger motion the fluid can still spread through most of the cube volume instead of settling into a convincing compact slosh.
- The current liquid surface is only a first custom renderer pass and still needs quality tuning.
- Overall smoothness is still behind `Zibra`, partly because this route is CPU-only and currently renders through Unity particle data rather than a dedicated GPU liquid pipeline.

## Key Pitfalls Already Discovered

- Directly forcing the container pose from Unity every rendered frame caused unstable behavior and leakage; command input must be translated into solver-side motion rather than naive transform teleporting.
- The first realtime bridge accidentally selected the wrong boundary body: the small anchor body instead of the wall container. This made the visible cube and the physical cube diverge.
- Native bridge changes are safer to validate after fully restarting Unity.
- The current machine is more stable with `Direct3D 11` than `Direct3D 12` for this project.
- `Zibra` and `SPlisHSPlasH` should be treated as separate solver and renderer stacks. Replacing one solver does not automatically carry over the other stack's visual results.

## Current Next Steps

- Improve the custom water surface toward a more stable and convincing screen-space liquid.
- Reduce frame hitches by moving more of the display path toward GPU-side rendering.
- Decide whether `SPlisHSPlasH` should remain the high-speed sealed-container route only, or replace `Zibra` as the main prototype path.
