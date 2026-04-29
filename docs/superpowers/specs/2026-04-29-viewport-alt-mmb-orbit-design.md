## Summary

The editor viewport currently supports:

- right-mouse freelook
- middle-mouse pan
- scroll-wheel zoom

It does not support orbit navigation around a target point. The editor needs a 3ds Max-style orbit interaction using `Alt + middle mouse button`, with selection-aware pivoting and a stable fallback when nothing is selected.

## Goals

- Add viewport orbit navigation on `Alt + middle mouse button`.
- Match 3ds Max behavior closely enough to feel familiar.
- Orbit around the selected entity position when a scene entity is selected.
- Fall back to a stable virtual view target when nothing is selected.
- Keep existing RMB freelook, MMB pan, and wheel zoom working.
- Keep orbit active after the pointer leaves the viewport when the drag started inside it.
- Preserve separate per-viewport camera state.

## Non-Goals

- Rebuild the full viewport navigation model to exactly mirror every 3ds Max orbit mode.
- Add a visible orbit gizmo or explicit orbit target widget.
- Change the existing RMB freelook shortcut.
- Add sub-object orbit behavior in this change.

## Current Problems

`EditorViewportCameraController` currently treats the viewport camera as a free camera with:

- yaw/pitch mouse look on RMB
- camera-relative panning on MMB
- forward/back wheel zoom

There is no persistent orbit target state, so `Alt + middle mouse button` cannot rotate the camera around a meaningful pivot. The editor also lacks a fallback target definition for the no-selection case.

## Interaction Design

### Orbit Shortcut

Orbit begins when:

- `Alt` is held
- the middle mouse button is pressed
- the press starts inside the viewport
- viewport input is not blocked by editor UI

Once orbit starts, it should continue until the middle mouse button is released, even if the pointer leaves the viewport rectangle.

### Orbit Pivot

Orbit pivot selection follows these rules:

1. If a scene entity is currently selected, use that entity's world position as the orbit target.
2. Otherwise, use the viewport camera's stored virtual target.

This mirrors the practical 3ds Max expectation:

- selection present: orbit around selection
- no selection: orbit around the current view target instead of failing

### Orbit Motion

During orbit:

- horizontal mouse delta adjusts yaw around the target
- vertical mouse delta adjusts pitch around the target
- pitch remains clamped to avoid gimbal lock
- camera orientation updates to face the orbit target
- camera position is recomputed from the orbit target and stored orbit distance

The target point itself must remain fixed during the orbit drag.

## Virtual Target Model

The viewport camera controller needs persistent target state in addition to its current yaw/pitch state.

### Stored State

Per viewport camera:

- current virtual target world position
- current orbit distance from camera to target
- current orbit-active drag state
- last mouse position used for orbit deltas

### Target Coherence Rules

To keep navigation coherent across pan, zoom, freelook, and orbit:

- panning moves both camera position and virtual target together
- wheel zoom moves the camera along forward and updates orbit distance relative to the current target
- RMB freelook preserves the current orbit distance and refreshes the virtual target from the camera's new forward direction
- selected-entity orbit can temporarily override the target at orbit start without destroying the no-selection fallback model

## Relationship To Existing Controls

### Right Mouse Freelook

RMB freelook remains unchanged as the editor's free-camera look mode.

After RMB look changes the orientation, the virtual target should be recomputed so the next no-selection orbit still behaves predictably.

### Middle Mouse Pan

Plain MMB pan remains unchanged in shortcut, but it must update the virtual target together with the camera position.

### Scroll-Wheel Zoom

Wheel zoom remains camera-forward zoom, but it must keep the stored orbit distance synchronized with the current target so subsequent orbiting uses the right radius.

## Architecture

This feature can remain inside `EditorViewportCameraController`.

Why this is the right scope:

- the current controller already owns viewport-local navigation state
- orbit state is tightly coupled to yaw/pitch/pan/zoom behavior
- adding a separate service now would increase surface area without reducing complexity

The controller should gain explicit helper methods for:

- synchronizing virtual target and orbit distance from the current camera state
- computing selected-orbit target versus fallback target
- applying orbit deltas around a target

These helpers should remain on the controller or move to a focused editor camera utility if reuse becomes necessary, but this change should not introduce UI logic into service classes or vice versa.

## Error Handling

- If the camera has no parent entity, preserve current failure behavior and throw.
- If orbit distance becomes too small, clamp it to a small positive minimum instead of allowing the camera to collapse into the target.
- If selection is invalid or becomes unavailable, fall back to the current virtual target instead of aborting input handling.

## Testing Strategy

Add focused regression coverage for:

- `Alt + middle mouse` starting orbit only when the press began inside the viewport
- orbit around selected entity preserving the selected pivot while camera position changes
- orbit with no selection using the stored virtual target
- pan moving both camera and target together
- wheel zoom updating stored orbit distance for later orbit
- orbit drag remaining active after the pointer leaves the viewport until release

## Recommended Implementation Order

1. Add failing controller tests for selected-target orbit and no-selection fallback orbit.
2. Add persistent virtual target/orbit distance state to `EditorViewportCameraController`.
3. Wire `Alt + middle mouse` orbit behavior into the controller while preserving plain MMB pan.
4. Synchronize virtual target updates across pan, wheel zoom, and RMB freelook.
5. Add the viewport-leave drag regression for orbit continuation.
6. Run focused editor tests and build verification.
