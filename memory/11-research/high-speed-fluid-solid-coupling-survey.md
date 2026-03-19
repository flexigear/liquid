# High-Speed Fluid-Solid Coupling Survey

## Problem

The current `Unity + Zibra` prototype is good enough for ordinary motion feel validation, but it does not reliably preserve sealed-container behavior under violent fast shaking. The target technical problem is not just collision detection. It is robust fluid-solid coupling for a fast-moving closed rigid boundary with low visible leakage.

## What The Problem Actually Requires

- Robust moving-boundary treatment
- Pressure solve or incompressibility handling that respects closed moving containers
- Stronger fluid-solid coupling than a visual-effects-oriented liquid plugin usually guarantees
- Good behavior under fast rigid motion, not only slow or medium sloshing

Continuous collision detection may help with geometric miss cases, but it is not sufficient by itself.

## Candidate Routes

### 1. SPlisHSPlasH

- Why it is attractive:
  - Open-source and directly focused on fluids and solids
  - Implements multiple incompressible SPH solvers
  - Closely tied to strong boundary-handling and rigid-fluid-coupling research
- Why it matches the current blocker:
  - The associated research line explicitly addresses boundary pressure, dynamic rigid boundaries, and stronger coupling behavior
- POC goal:
  - Build a square closed container test and evaluate whether aggressive rotational motion leaks less than the current Unity + Zibra setup
- Attempt status on `2026-03-19`:
  - Cloned locally into `.experiments/SPlisHSPlasH`
  - Configured successfully on Windows with `Visual Studio 18 2026`
  - Built `SPHSimulator.exe` successfully
  - Verified a headless dynamic-boundary run with the bundled `MotorScene.json`
  - Verified that the default build does not include embedded Python support
  - Added a first local custom scene `LiquidCubeShakePOC.json` using a dynamic wall cube plus `TargetVelocityMotorHingeJoints`
  - Manual GUI check confirmed that the custom shaking-square scene keeps the fluid inside the boundary even under aggressive motion, although the visualization is raw particle rendering rather than a rendered liquid surface
  - Built a native Unity bridge and hooked `SPlisHSPlasH` back into the Unity prototype as a realtime interactive scene
  - Confirmed a key early pitfall: the first Unity bridge version selected the wrong boundary body, so the visible cube and physical cube diverged
  - Confirmed a second key pitfall: directly forcing the container pose from Unity is much less stable than driving a solver-owned dynamic body
  - The current Unity route is now interactive and visually beyond raw points, but it is still CPU-side, currently runs at about `5292` particles, and can still hitch or spread fluid through most of the container under harder impacts
- Source anchors:
  - <https://splishsplash.physics-simulation.org/>
  - <https://animation.rwth-aachen.de/publication/0554/>
  - <https://animation.rwth-aachen.de/publication/0565/>
  - <https://animation.rwth-aachen.de/publication/0563/>
  - <https://www.animation.rwth-aachen.de/publication/0584/>

### 2. SPHinXsys

- Why it is attractive:
  - Strong-coupling multiphysics orientation
  - Explicit fluid-solid and multibody-coupling framing
  - Better aligned with serious research validation than a game-middle-ware plugin
- Why it is not first:
  - More framework-heavy than SPlisHSPlasH for a first leakage-focused POC
- POC goal:
  - Confirm whether a stronger SPH multiphysics framework materially improves sealed-container behavior under aggressive motion
- Attempt status on `2026-03-20`:
  - Cloned locally into `.experiments/SPHinXsys`
  - Installed the required Windows `vcpkg` toolchain and core dependencies locally in `.experiments/vcpkg`
  - Completed a successful Windows `cmake` configure with the local `vcpkg` toolchain and Python interface disabled
  - Built `test_2d_dambreak.exe` successfully
  - Verified a successful runtime of the bundled dambreak example using `--regression 1`
  - Added a local cube-specific route-2 test target `test_2d_liquid_cube_shake`
  - Implemented prescribed oscillating square-wall motion with per-particle rigid rotation, velocity update, and rotated wall normals
  - Ran the local shaking-square test successfully to `physical_time ~= 6.01`
  - The current route-2 result is promising: one verified local run finished with `max escaped particles = 0` and `max overflow distance = 0.000000`
- Source anchors:
  - <https://www.sphinxsys.org/>
  - <https://www.sphinxsys.org/html/installation.html>
  - <https://github.com/Xiangyu-Hu/SPHinXsys>

### 3. PreonLab

- Why it is attractive:
  - Probably the fastest way to see whether the target behavior is achievable with a more serious commercial particle solver
  - GPU-first direction and engineering-oriented feature set
- Why it is not first:
  - Commercial and not a natural long-term runtime stack for the current project
- POC goal:
  - Use as a truth probe: determine whether the desired closed-container high-speed behavior is realistic without investing in custom solver work immediately
- Source anchors:
  - <https://www.fifty2.eu/innovation/preonlab-6-1-released/>
  - <https://www.fifty2.eu/innovation/preonlab-7-0-released/>

### 4. Custom Kinetic Or LBM Solver

- Why it is attractive:
  - Strong fit for GPU execution
  - Recent research directly targets fast moving solids, pressure oscillations, and unstable fluid-solid coupling cases
- Why it is not first:
  - Highest implementation cost among practical options
- POC goal:
  - Only pursue if off-the-shelf or open-source SPH paths clearly fail the requirement
- Source anchors:
  - <https://www.physicsbasedanimation.com/2021/09/25/fast-and-versatile-fluid-solid-coupling-for-turbulent-flow-simulation/>
  - <https://geometry.caltech.edu/pubs/LD23.pdf>

### 5. Custom Cut-Cell Or Variational FLIP / Eulerian Solver

- Why it is attractive:
  - Technically very aligned with moving rigid boundaries and enclosed incompressible regions
  - The pressure-projection literature is highly relevant to sealed-container motion
- Why it is not first:
  - Excellent long-term direction, but expensive as an initial POC
- POC goal:
  - Treat as the deeper self-owned route if simpler candidates fail and strict no-leakage remains mandatory
- Source anchors:
  - <https://www.cs.ubc.ca/labs/imager/tr/2007/Batty_VariationalFluids/>
  - <https://www.physicsbasedanimation.com/2016/05/>
  - <https://www.cs.ucr.edu/~shinar/papers/2021_bpp.pdf>

## Hard Ranking For This Project

1. `SPlisHSPlasH`
2. `SPHinXsys`
3. `PreonLab`
4. `Custom kinetic / LBM`
5. `Custom cut-cell / FLIP-Eulerian`

## Why The Ranking Looks Like This

- `SPlisHSPlasH` is the best balance of relevance, openness, research depth, and realistic proof-of-concept cost.
- `SPHinXsys` is also serious, but heavier for the first pass.
- `PreonLab` may be the fastest practical answer if budget is acceptable, but it is not the best first project-owned route.
- The custom solver paths are more likely to be correct long-term than cheap short-term.

## Recommended POC Sequence

1. Attempt `SPlisHSPlasH` first.
2. If the setup cost or results are poor, attempt `SPHinXsys`.
3. If a commercial truth-check is acceptable, evaluate `PreonLab`.
4. Only move to custom solver work after the open-source and commercial probes establish that the requirement is still unsatisfied.

## Decision Rule

- If `SPlisHSPlasH` already handles the motion envelope well enough, do not escalate to a custom solver.
- If both `SPlisHSPlasH` and `SPHinXsys` fail the violent sealed-container test, assume the requirement is outside easy middleware territory and plan either a commercial engineering tool phase or custom solver research.

## Current Experiment Status

- `SPlisHSPlasH`: local build, first custom shaking-cube scene, manual aggressive-motion test, Unity realtime bridge, and first custom liquid-surface pass are complete.
- `SPHinXsys`: local build, bundled-example runtime, and first local shaking-square test are complete.
- `PreonLab`: not yet attempted.
- `Custom kinetic / LBM`: not yet attempted.
- `Custom cut-cell / FLIP-Eulerian`: not yet attempted.
