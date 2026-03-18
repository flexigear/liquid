# Trackball Control

## Input Model

The container is controlled by a virtual trackball, not by direct liquid dragging.

## Control Chain

`pointer drag -> trackball delta rotation -> target container rotation -> smoothed angular motion -> fluid response`

## Requirements

- The trackball should feel like rotating a held object, not spinning a menu model.
- Container orientation changes should be smooth and physically suggestive.
- Fluid response must depend on container angular velocity and angular acceleration, not only on the final pose.
- Releasing input should still leave enough rotational aftereffect for the liquid to continue moving briefly.

## Practical Guidance

- Treat user input as a driver for target rotation or angular impulse.
- Add spring-damper smoothing to the vessel motion.
- Feed vessel motion into the fluid simulation as inertial influence.
- Avoid instantaneous pose snapping.

## Reason

Without this chain, the player will feel like they are rotating a visual box around static water instead of physically moving a vessel full of liquid.
