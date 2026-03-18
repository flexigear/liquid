# Physics Authenticity Principles

## Design Target

Aim for materially credible water behavior inside glass, not merely a liquid-looking game object.

## Non-Negotiable Phenomena

- Volume should be conserved.
- Motion should show inertia and lag.
- Liquid should deform on impact and recover through surface tension.
- Water should interact with the glass wall through adhesion and contact-angle behavior.
- Separated drops should be able to merge into larger drops.

## Important Realism Note

Pure water in a glass container will not always stay as neat isolated beads. Depending on wetting behavior it may spread, pin to the wall, form films, or leave trailing residue.

This is not a problem to hide. It should guide the simulation model and puzzle design.

## Acceptable Simplifications

- Controls may be slightly stabilized relative to reality.
- Parameter tuning may bias toward readable drop formation.
- The simulation may start with a small number of larger drops instead of many tiny droplets.

## Unacceptable Simplifications

- Treating the liquid as rigid marbles for shipped gameplay.
- Directly dragging water independent of container motion.
- Removing wall interaction and relying only on gravity.

## Practical Product Consequence

The best "realistic but playable" target is likely a main liquid body plus smaller wall-bound drops, not a cloud of perfectly round beads.
