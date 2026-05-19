# Selection-Size Viewport Camera Speed Design

## Summary

Editor viewport navigation should adapt to the size of the currently selected object by default, while still allowing a per-viewport manual override from the viewport settings overlay.

This is an editor-only behavior change. It must not require `helengine.core` changes and it must not alter runtime scene behavior.

## Problem

The current editor viewport camera controller uses fixed defaults:

- `MoveSpeed`
- `PanSpeed`
- `WheelZoomSpeed`

Those values are acceptable for only a narrow range of scene scales. When the selected object is much smaller or much larger than the default assumptions, camera navigation feels wrong:

- tiny selections feel too fast
- large selections feel too slow
- users have to fight the camera instead of navigating relative to the thing they are editing

There is also no per-viewport control surface for users who want a stable manual speed instead of adaptive behavior.

## Goals

- Make viewport camera movement speed adapt to the selected object size by default.
- Keep the behavior viewport-local, not global.
- Expose a per-viewport manual override in the viewport settings overlay.
- Persist the chosen speed mode and manual override value with workspace viewport state.
- Reuse existing editor-only bounds resolution where possible.
- Keep the system predictable and easy to understand.

## Non-Goals

- No runtime engine behavior changes.
- No `helengine.core` contract changes.
- No distance-based auto speed mode in this pass.
- No hybrid size-plus-distance speed mode in this pass.
- No per-axis speed customization in this pass.

## User Experience

Each viewport gets a camera speed mode:

- `Auto From Selection`
- `Manual Override`

### Auto From Selection

When auto mode is active, the viewport camera controller derives navigation speeds from the current selected entity bounds:

- larger selection -> faster movement, pan, and zoom
- smaller selection -> slower movement, pan, and zoom

If there is no selection, or the selected entity does not expose useful bounds, the controller falls back to the existing default speed values.

### Manual Override

When manual mode is active, the viewport camera controller ignores selection-size adaptation and uses one viewport-local authored speed value from the settings overlay.

This lets the user pin one viewport to a stable navigation speed even if selection size changes.

## Bounds Source

Auto speed uses the same editor-only selection-bounds seam used by viewport focus behavior.

Selection extent resolves in this order:

1. `ViewportComponent`
   - Use the full authored viewport rectangle.
   - Extent is derived from the resolved viewport size.

2. `MeshComponent`
   - Use runtime model bounds transformed by entity scale.
   - Extent is derived from the maximum model-space dimension after entity scale.

3. `SpriteComponent`
   - Use sprite width and height.
   - Extent is derived from the maximum sprite dimension.

4. Fallback
   - When no supported bounds source exists, use the current default camera speed values.

This keeps speed derivation consistent with the editor-side framing model and avoids multiple competing interpretations of selection size.

## Speed Derivation

The adaptive system derives one scalar `SelectionExtent` from the selected entity.

Recommended rule:

- `SelectionExtent = max(width, height, depth-equivalent)`

Then derive speeds from that extent:

- movement speed scales from selection extent
- pan speed scales from selection extent
- wheel zoom speed scales from selection extent

All derived values must be clamped to sensible editor-safe minimum and maximum limits so extreme object sizes do not make navigation unusable.

The current default values remain the fallback baseline:

- `MoveSpeed = 0.15f`
- `PanSpeed = 0.01`
- `WheelZoomSpeed = 1.0`

The exact scaling constants should be chosen so that common scene sizes still feel close to the current defaults.

## Viewport Settings Overlay

The viewport settings overlay gains a camera speed section.

It must expose:

- speed mode selector:
  - `Auto From Selection`
  - `Manual Override`
- manual speed slider or numeric control

Behavior:

- in auto mode, the manual speed value is retained but not applied
- in manual mode, the manual speed value drives movement speed
- the overlay should make the active mode visually obvious

The speed section belongs with existing viewport-local camera settings such as near and far clip plane controls.

## Persistence

Viewport workspace state must persist:

- speed mode
- manual override speed value

That data should round-trip through the same viewport workspace save/load path that already persists clip planes, tool mode, and snap settings.

## Architecture

### EditorViewportCameraController

The controller remains the owner of effective camera movement values.

It gains:

- one viewport-local speed mode
- one viewport-local manual speed value
- one editor-only adaptive-speed update path

The controller should resolve effective movement values from:

- current mode
- selected entity bounds extent
- fallback defaults

### Shared Bounds Logic

Adaptive speed must reuse the same editor-only selection bounds service or helper used by viewport framing.

That avoids duplicate mesh/sprite/viewport extent logic and keeps behavior coherent:

- focus uses the same notion of bounds as speed
- future editor camera features can reuse the same seam

### Overlay Integration

The viewport settings overlay should edit controller-owned state through the viewport/controller path, not by duplicating speed math locally.

The overlay is an editor presentation surface. The controller remains the behavior owner.

## Persistence Model

Workspace viewport state serialization gains two additional fields:

- speed mode
- manual speed override value

On restore:

- if values are missing from old saved state, defaults apply
- default mode should be `Auto From Selection`
- default manual speed should match the current fixed movement baseline

## Validation

Tests should cover:

1. Controller adaptive speed
   - large selected object increases effective movement speed
   - small selected object decreases effective movement speed
   - unsupported selection falls back to defaults

2. Mode switching
   - auto mode uses selection size
   - manual mode ignores selection size

3. Overlay behavior
   - overlay exposes the new controls
   - toggling mode updates the controller
   - manual value changes update the controller

4. Workspace persistence
   - speed mode and manual value round-trip through save/load

5. Reuse consistency
   - viewport selections use viewport extent
   - mesh selections use model bounds
   - sprite selections use sprite size

## Risks

- If scaling constants are chosen poorly, the camera may feel unstable across ordinary asset sizes.
- If adaptive speed logic is duplicated instead of shared, focus and movement behavior will drift apart over time.
- If the overlay owns speed math instead of editing controller state, the system will become harder to maintain.

## Recommendation

Implement adaptive camera speed as an editor-only controller feature backed by shared selection-bounds resolution, with a viewport-local manual override exposed and persisted through viewport settings.

That gives the default behavior users want without removing explicit control for cases where a fixed speed is preferable.
