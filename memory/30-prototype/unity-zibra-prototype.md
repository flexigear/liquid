# Unity Plus Zibra Prototype

## Purpose

Capture the actual implementation pattern, verified behavior, and known failure modes of the first GPU-driven liquid prototype.

## Current Stack

- Unity editor baseline: `6000.3.11f1`
- C# control and tooling scripts
- Universal Render Pipeline (`URP`)
- Zibra Liquid
- Editor launch preference for stability: `-force-d3d11`

## Repository Boundary

- The public repository should keep the Unity project structure and local control code.
- The commercial `Zibra Liquid` plugin itself should be imported locally and not pushed to the public remote.
- Any local import patches against the plugin should be documented in memory rather than treated as generally committed source.

## Current Scene Architecture

- Bootstrap entry: `Liquid/Bootstrap/Setup Project`
- Evaluation scene: `Assets/Scenes/SquareContainerPrototype.unity`
- Container control pivot: `ContainerPivot`
- Visible container: six thin glass walls plus a cube frame
- Fluid solver object: `ZibraLiquidVolume`
- Reflection support: a hidden reflection rig that is visible to reflections but excluded from the main camera

The liquid solver object is intentionally not parented under the rotating pivot. The rotating glass walls define the effective container boundary through Zibra colliders, while the solver keeps its own larger axis-aligned container volume.

## Control Model

- Left mouse drag rotates the container
- Right mouse drag plus `WASD`, `Q`, `E`, and wheel moves the camera
- `R` resets camera and container state
- `F5` reloads the scene

The player is manipulating the container, not the world and not the liquid directly.

## Rendering Approach

- Glass uses real thin wall geometry instead of coplanar transparent quads
- Liquid rendering is tuned toward clean water rather than opaque blue gel
- Reflection cards are hidden from the gameplay camera and only exist to make glass and liquid reflections readable

This is a deliberately staged realism pass. It is not yet a production-grade glass shader stack.

## Confirmed Wins

- The Unity prototype now has real liquid inside a readable square glass container.
- Ordinary slow and medium rotations read much more like liquid than the browser approximation.
- Water amount, glass readability, and quick reset flow are now tunable in one scene.
- The browser prototype has been successfully retired as an active implementation path and preserved as historical baseline only.

## Confirmed Pitfalls

- `Unity 6.3 + Zibra Liquid` needed an extra `Timeline` package import to compile cleanly.
- Zibra shipped editor-side warnings and minor breakages that had to be patched locally after import.
- `URP` on Unity `6.3` required compatibility mode plus the `URP_COMPATIBILITY_MODE` scripting define for the current Zibra render path.
- Editor stability on Direct3D 12 was poor. Exiting play mode could hang the editor. For this prototype, launching Unity with `-force-d3d11` is materially safer.
- Coplanar double-quad glass looked plausible at first but produced inconsistent highlights and face readability. Replacing it with thin glass wall geometry was the right correction.
- Trying to harden leakage by forcing a tighter fixed-step simulation plus extra iterations made the liquid feel worse and still did not solve aggressive high-speed spill-through. That experiment was reverted.
- The current `Unity + Zibra` setup is acceptable for ordinary motion validation, but not yet trustworthy for extreme fast closed-container motion with a strict "no leakage under violent shaking" requirement.

## Important Technical Constraint

Zibra's liquid container model is still fundamentally axis-aligned at the solver level. The rotating cube behavior is approximated by driving rotating colliders inside a larger solver volume. This works for the prototype's ordinary interaction range, but it is a real constraint when evaluating extreme motion.

## Recommended Interpretation

- Use the current prototype to judge ordinary liquid feel, readability, and glass presentation.
- Do not treat this implementation as proof that the project has solved high-speed sealed-container fluid behavior.
- If strict no-leakage behavior under violent container motion becomes non-negotiable, plan for deeper solver research rather than assuming more parameter tuning will finish the job.

## Near-Term Next Steps

- Capture reference videos of slow, medium, and fast motion in the current Unity scene.
- Define the acceptable motion envelope for this prototype instead of treating "any possible shake speed" as the same requirement.
- Research solver approaches that handle fast moving solid boundaries more robustly before committing to production architecture.
