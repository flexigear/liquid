# Unity Prototype

This directory now contains the first GPU-driven implementation workspace for `Liquid`.

## Target Stack

- Unity LTS
- C#
- URP
- Zibra Liquid

## Current State

- Unity editor baseline: `6000.3.11f1`
- Render pipeline: `URP`
- Bootstrap scene: `Assets/Scenes/SquareContainerPrototype.unity`
- Bootstrap menu: `Liquid/Bootstrap/Setup Project`
- Current control script: `Assets/Scripts/TrackballContainerController.cs`
- Reset hotkey during play: `R`
- Full scene reload during play: `F5`
- Local working copy has `Zibra Liquid` imported and active in the current scene
- The scene now includes liquid, glass walls, reflection support, and inspection controls
- Editor usage on the current machine is safer with `-force-d3d11`

This public repository does not check in the commercial `Zibra Liquid` plugin directory. After cloning, import the plugin locally before opening the scene for real liquid playback.

The project already includes a repeatable bootstrap step that:

- ensures the core `Assets/*` folders exist
- ensures the URP package is installed
- creates a persistent URP asset and default renderer under `Assets/Settings`
- assigns the same URP asset to all quality levels
- creates the baseline square-container evaluation scene
- builds a six-wall glass container rig instead of a solid debug cube
- configures a first-pass glass presentation and liquid rendering setup for inspection

## Import Notes And Pitfalls

- Add `com.unity.timeline` if the imported Zibra package complains about `UnityEngine.Timeline` types.
- On Unity `6.3`, the current Zibra render path still needs `URP_COMPATIBILITY_MODE`.
- If the editor hangs when leaving play mode, launch Unity with `-force-d3d11`.
- The public repo does not include the local Zibra patch files because the plugin is not committed here.

## Goal

Use a real-time liquid setup to answer one question clearly:

Does rotating a transparent square glass container feel like moving real liquid rather than moving a clever fake?

## Evaluation Scene

The current scene contains:

- a transparent square glass container
- one controllable camera view sized for close inspection
- trackball-style container rotation
- liquid inside the container driven by Zibra Liquid
- quick reset controls for repeated tuning
- a hidden reflection rig to keep glass readability usable

## Suggested Project Layout

Once the Unity project is created here, keep the structure simple:

- `Assets/Scenes`: evaluation scenes
- `Assets/Scripts`: C# control and helper scripts
- `Assets/Materials`: glass, liquid, and debug materials
- `Assets/Prefabs`: container and reusable scene pieces
- `Assets/Settings`: URP and project-level assets

## Immediate Next Steps

1. Record and review the current scene under slow, medium, and aggressive rotations.
2. Tune liquid volume, settle behavior, and glass readability against the prototype goals.
3. Decide whether the current solver is sufficient for the acceptable motion envelope.
4. Treat extreme high-speed sealed-container no-leakage behavior as a separate architecture decision if it remains required.
