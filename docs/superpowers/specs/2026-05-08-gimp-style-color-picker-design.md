# GIMP-Style Color Picker Design

## Summary

The editor should replace the current RGB slider popup with a larger, GIMP-style color picker that is reusable across the engine.

The new picker keeps the existing `EditorColorFieldControl` entry point, but its overlay changes from a simple RGB slider panel into a hue wheel with an inner triangle for saturation and value, plus a separate alpha slider. The hex textbox stays visible and synchronized so users can still type a color directly.

## Goals

- Replace the current color popup with a GIMP-style wheel-and-triangle picker.
- Keep the picker reusable for future `Color` fields across the editor.
- Keep the hex textbox in sync with the picker and continue accepting HTML color codes.
- Add a separate alpha slider instead of mixing alpha into the wheel/triangle interaction.
- Make the popup larger so the wheel and triangle are comfortably readable and usable.
- Preserve the existing overlay hosting model so the picker stays above the material editor.

## Non-Goals

- No docked, persistent color panel in this slice.
- No gradient editor.
- No palette browser.
- No dynamic shader-parameter discovery work.
- No redesign of the material schema system itself.

## Current Problem

The current picker is a small RGB slider popup. That shape is functional, but it does not match the interaction model the user wants for color authoring. It also does not provide the center wheel/triangle interaction that GIMP uses, which makes it harder to pick hue and saturation/value naturally.

The current picker also needs to remain reusable. The goal is not to create a material-only color UI, but to move the engine toward one shared color editing control that can be used anywhere a `Color` field appears.

## Proposed Design

### 1. Keep The Reusable Field Control Entry Point

`EditorColorFieldControl` remains the public field control used by material rows and future editor `Color` fields.

It still provides:

- an HTML color textbox
- a clickable color swatch
- a request to open the shared overlay when the swatch is clicked

This keeps existing schema-driven material rendering intact while swapping only the popup interaction model.

### 2. Replace The Popup With A GIMP-Style Picker

The overlay should be redesigned around three authored areas:

- a hue wheel
- an inner triangle for saturation and value
- a separate alpha slider

The picker should also keep a live preview square visible so the user can compare the current result at a glance.

Recommended layout:

- left side: hue wheel with inner triangle
- right side: live preview, current hex value, and optional numeric readout
- bottom: alpha slider
- footer: close button

The overlay should be noticeably larger than the current popup so the wheel and triangle remain usable in the editor.

### 3. Keep The Textbox As A Live Synchronized Entry Path

The HTML textbox should stay in sync with the visual picker.

Rules:

- typing a valid hex color updates the wheel/triangle/preview immediately
- dragging the wheel or triangle updates the textbox immediately
- alpha changes through the separate slider also update the textbox immediately
- invalid typed values should remain invalid until corrected, matching the current text-field behavior

This preserves the current direct-entry workflow while adding a visual picker for users who prefer mouse input.

### 4. Keep Alpha Separate

The picker should not fold alpha into the wheel or triangle.

Alpha is a separate slider because:

- it keeps the wheel/triangle interaction simple
- it matches the user request
- it avoids making the core color interaction harder to learn

### 5. Keep The Popup Modal-Style And Clickable

The picker should continue to open as an overlay attached to the editor modal host, not as inline material content.

That means:

- it must render above the properties panel
- it must remain clickable
- it must continue to dismiss on outside clicks and `Esc`
- it must not recreate the hit-test bug that the earlier background interactable caused

## Architecture

### `EditorColorFieldControl`

This control remains the field-level entry point.

Responsibilities:

- render the hex textbox and swatch
- keep a current `byte4` value
- raise `PickerRequested`
- synchronize typed text with the visual picker state

### `EditorColorPickerOverlayComponent`

This component becomes the reusable popup host for all color editing.

Responsibilities:

- build the larger picker overlay
- render the hue wheel
- render the inner triangle selector
- render the alpha slider
- render the preview square and close button
- update the current color live as the user drags
- sync the active field control through `ColorChanged`

### Color State

The picker should operate on a single current color value in a normalized, reusable format.

Expected behavior:

- hue comes from the wheel angle
- saturation/value come from the triangle position
- alpha comes from the separate slider
- the final value is published as the current `byte4`

The overlay does not own a second, hidden model. The current picker value remains the source of truth.

## Interaction Model

### Wheel

Dragging the wheel changes hue.

The current hue is reflected immediately in:

- the triangle background
- the preview square
- the hex textbox

### Triangle

Dragging inside the triangle changes saturation and value.

The triangle should behave as the main body of the picker, giving the user the familiar GIMP-style full color range inside the chosen hue.

### Alpha Slider

Dragging the alpha slider changes only alpha.

The alpha slider should live outside the wheel/triangle control area so the user can see that it is separate.

### Preview

The preview square should show the current picked color with alpha awareness so the user can see transparency changes immediately.

## Data Flow

1. The material editor renders `EditorColorFieldControl` for a schema `Color` field.
2. The user clicks the swatch.
3. The control raises `PickerRequested`.
4. `MaterialAssetView` opens the shared color picker overlay and seeds it from the current value.
5. The overlay updates hue, saturation, value, and alpha live as the user interacts.
6. The overlay publishes changes back to the active field control.
7. The field control updates the textbox and swatch immediately.
8. On close, the final value is committed back to the active material settings.

## Error Handling

- If the picker is asked to open before it is attached to an editor host, preserve failure behavior and throw.
- If the wheel or triangle math receives an impossible input, clamp to the nearest legal value instead of inventing defaults.
- If the active field disappears while the picker is open, close the picker cleanly rather than leaving stale references behind.
- If typed text is invalid, keep the field invalid until the user corrects it.

## Testing Strategy

Add focused tests for:

- clicking the swatch opens the picker
- the picker renders with the larger wheel-based layout
- wheel interaction changes hue and updates the hex textbox
- triangle interaction changes saturation/value and updates the preview
- alpha slider changes alpha without affecting hue selection
- typed hex values still update the picker state
- the picker remains clickable when hosted under the modal root
- outside clicks and `Esc` still close the picker

## Recommendation

Implement the picker as a larger reusable overlay with a hue wheel, inner triangle, separate alpha slider, live preview, and synchronized hex textbox.

That gives the engine a better long-term color editing primitive without tying the first implementation to materials only.
