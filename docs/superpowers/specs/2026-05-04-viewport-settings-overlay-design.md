## Summary

The editor viewport should gain a top-right settings button that opens a non-modal overlay for viewport-specific presentation and camera controls. That overlay replaces the current in-toolbar grid toggle and adds live near-plane and far-plane controls.

This is not only a UI change. The current 3D renderers hardcode perspective clip planes to `0.1f` and `100f`, so editable near and far planes require camera clip-plane state that flows through `ICamera`, `CameraComponent`, and both 3D backends.

## Goals

- Add a dedicated viewport settings button aligned to the right side of the viewport toolbar.
- Open a non-modal overlay panel anchored beneath that button.
- Move the existing grid toggle out of the main toolbar and into the overlay.
- Add live near-plane and far-plane controls that update the active viewport camera while dragging.
- Make the overlay fully keyboard navigable.
- Close the overlay automatically when the user clicks outside it.
- Keep the change scoped to the viewport and renderer architecture without introducing modal behavior.

## Non-Goals

- Add a general-purpose property inspector for all camera settings.
- Persist viewport settings to project files in this change.
- Add text-entry fields for near and far planes in this first pass.
- Redesign the rest of the viewport toolbar layout beyond the new right-side settings button and removal of the old grid button.

## Current Problems

The current `EditorViewport` toolbar mixes tool buttons, snap controls, and a dedicated grid button in one fixed left-aligned strip. The grid toggle is functional, but it does not scale to multiple viewport settings and it consumes permanent toolbar space for a single boolean option.

The current render path also hardcodes perspective clip planes:

- `VulkanRenderer3D` uses `0.1f` near and `100f` far.
- `DirectX11Renderer3D` uses `0.1f` near and `100f` far.

Because those values are not camera state, adding UI alone would create a fake control that cannot actually drive rendering.

## Interaction Design

### Settings Button

`EditorViewport` gains a new settings button placed on the toolbar's far right edge. The button should use the same button chrome language as the existing toolbar controls so it reads as part of the viewport chrome rather than as floating content.

The button acts as a toggle:

- click when closed: open the overlay
- click when open: close the overlay
- `Enter` or `Space` when focused: open the overlay

When the overlay closes through keyboard or pointer interaction, focus returns to the settings button.

### Overlay Behavior

The settings surface is an overlay, not a modal dialog.

Behavior:

- it is visually anchored below the settings button
- it does not block the rest of the editor with a modal backdrop
- clicking anywhere outside the overlay closes it
- pressing `Esc` while focus is inside the overlay closes it
- closing the overlay does not change the active dock tab or viewport focus ownership beyond returning focus to the settings button

The overlay remains above viewport scene content and toolbar chrome using the editor overlay render band.

### Overlay Contents

The overlay contains the following controls in order:

1. `Show Grid` toggle
2. `Near Plane` slider
3. `Far Plane` slider
4. `Close` button

Each slider row includes:

- a text label
- a draggable track with thumb
- a live numeric value label

There is no separate Apply or OK step. Slider movement updates the viewport camera immediately.

### Keyboard Navigation

The overlay must participate in the editor keyboard focus system.

Expected flow:

1. Tab to the viewport settings button.
2. Press `Enter` or `Space` to open the overlay.
3. Focus moves to the first overlay control.
4. `Tab` and `Shift+Tab` move through every overlay control in order.
5. `Enter` or `Space` activates the focused toggle or close button.
6. Left and right arrow keys adjust the focused slider.
7. `Esc` closes the overlay and restores focus to the settings button.

The bottom `Close` button exists specifically to give keyboard users a clear exit target without requiring `Esc`.

## Near And Far Plane Model

### Camera State

The near and far clip planes should become camera state instead of renderer constants.

Add shared clip-plane properties to the camera abstraction:

- `NearPlaneDistance`
- `FarPlaneDistance`

These values belong on `ICamera` and `CameraComponent`, with `CameraComponent` initialized to the current effective defaults:

- near: `0.1f`
- far: `100f`

### Validation Rules

The camera must always remain in a valid projection state.

Validation rules:

- near must stay greater than zero
- far must stay greater than near
- invalid assignments should be clamped to the nearest legal value, not ignored

To keep behavior deterministic, enforce a minimum separation of `0.01f` between near and far.

### Slider Ranges

Clip-plane distances span a wide range, so the sliders should not use the same linear feel for both controls.

Use these authored ranges:

- near slider range: `0.01f` to `10f`
- far slider range: `1f` to `5000f`

Use logarithmic mapping inside the slider control for these rows so low values remain adjustable while still allowing large far-plane distances without an unusably long track.

Displayed numeric values should be formatted compactly:

- up to three fractional digits for values below `10`
- up to two fractional digits for values below `100`
- integer formatting above that

### Cross-Constraint Behavior

The sliders must preserve projection validity live while dragging.

If the near slider is dragged too high:

- clamp it to `FarPlaneDistance - 0.01f`

If the far slider is dragged too low:

- clamp it to `NearPlaneDistance + 0.01f`

This keeps the rendered view stable during live updates and avoids transient invalid projection matrices.

## Architecture

### `EditorViewport`

`EditorViewport` remains responsible for:

- creating the top-right settings button
- owning the open/closed state
- anchoring and sizing the overlay
- handing the viewport camera reference to overlay controls
- removing the old standalone grid button from the main toolbar flow

The current dedicated grid button focus target should be replaced by the settings button focus target.

### New Viewport Settings Overlay Component

Add a dedicated overlay component for this feature instead of pushing more state into `EditorViewport`.

Responsibilities:

- build overlay entities and background chrome
- host the grid toggle row
- host near/far slider rows
- host the close button
- manage outside-click dismissal
- register and unregister overlay focus targets
- raise value-change events back to the owning viewport

This keeps `EditorViewport` as the presentation host while moving overlay-specific layout and interaction logic into a focused unit.

### New Reusable Slider Control

Add a reusable editor slider control rather than building one-off drag math inside the viewport overlay.

Responsibilities:

- draw the slider track and thumb
- support pointer hover, press, drag, and release
- support keyboard focus styling
- support left/right keyboard adjustment
- expose `ValueChanged` events during live interaction
- support linear and logarithmic value mapping modes

This control should live in the editor UI layer so it can be reused later by other settings surfaces.

### Rendering Backends

Both 3D backends must stop hardcoding perspective clip planes and instead read them from the active camera:

- `DirectX11Renderer3D`
- `VulkanRenderer3D`

That backend change is part of this feature and should ship together with the overlay so the UI always reflects real camera behavior.

## Layout

### Toolbar Layout

The existing toolbar continues spanning the viewport width, but the settings button is right-aligned instead of joining the left-aligned tool cluster.

Result:

- tool buttons stay on the left
- snap controls stay in their current band
- viewport settings move to a dedicated right-side entry point

### Overlay Layout

The overlay should be a compact rectangular panel sized to its content, with enough width for readable labels and value readouts.

Recommended structure:

- top padding
- grid toggle row
- near slider row
- far slider row
- bottom close button row

The overlay should open downward by default. If the viewport is too short to fit below the button, it may open upward, but this fallback is not required for the first implementation if the current viewport layout already guarantees space.

## Error Handling

- If the overlay is asked to bind a null camera, preserve failure behavior and throw rather than fabricating default camera state.
- If a clip-plane assignment becomes invalid during live adjustment, clamp it immediately to the closest legal value.
- If the overlay loses focus because the viewport becomes disabled or hidden, close the overlay and unregister its targets.
- If the settings button is disabled because the viewport is disabled, the overlay must not remain visible.

## Testing Strategy

Add focused tests for:

- settings button focus and keyboard activation
- overlay open and close through pointer interaction
- outside click dismissal
- `Esc` dismissal and focus restoration
- tab traversal through overlay controls
- close button keyboard activation
- grid visibility toggling after the control moves into the overlay
- near slider live updates changing camera clip state
- far slider live updates changing camera clip state
- near/far clamp behavior when one slider approaches the other
- renderer projection creation reading per-camera near and far values instead of constants

## Recommended Implementation Order

1. Add failing tests for settings-button keyboard activation, overlay dismissal, and moved grid-toggle behavior.
2. Add failing tests for camera near/far clip-plane state and renderer projection usage.
3. Introduce clip-plane properties on `ICamera` and `CameraComponent` with the current defaults.
4. Update DirectX11 and Vulkan perspective projection code to consume the camera clip-plane values.
5. Add the reusable editor slider control with pointer and keyboard behavior.
6. Add the viewport settings overlay component and wire it into `EditorViewport`.
7. Remove the old standalone grid button from the toolbar and route the grid setting through the overlay.
8. Run focused editor tests and renderer tests for verification.
