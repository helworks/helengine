## Summary

The editor scene viewport should present 2D scene content as a fixed world-space canvas plane instead of requiring a separate screen-space 2D viewing mode. The plane should always be visible in the 3D viewport, render the authoritative 2D layout output, and participate in normal depth-based picking.

This is not only a viewport presentation change. The plane must bridge world-space clicks back into the existing 2D editor selection path so the same selection model and transform gizmos continue to work. The viewport settings overlay should become the source of truth for the simulated 2D canvas size and world scale used by that plane.

## Goals

- Always show 2D scene content inside the main 3D scene viewport on a fixed world plane.
- Preserve the existing 2D layout model so anchors, padding, and canvas-relative positioning continue to behave correctly.
- Make the simulated 2D canvas size configurable from the existing viewport settings overlay.
- Start with a default simulated canvas size of `1280x720`.
- Start with a default world scale of `100 pixels = 1 world unit`.
- Keep the plane anchored so its bottom-left corner is at world `(0, 0, 0)`.
- Let normal 3D depth decide whether scene geometry or the canvas plane receives the winning pick.
- When the plane wins the pick, translate that hit back into canvas coordinates and reuse the existing 2D selection flow.
- Continue using the existing gizmo system after selection instead of creating a parallel 2D manipulation model.

## Non-Goals

- Add a separate viewport toggle that hides or shows the 2D plane.
- Add a screen-space billboarded 2D preview mode.
- Rebuild the runtime 2D system as native world-space geometry in this change.
- Add a second gizmo system or custom drag handles specifically for 2D-on-plane editing.
- Stretch the 2D canvas to match arbitrary scene viewport aspect ratios.
- Persist these viewport settings into project scene files in the first pass.

## Current Problems

The current editor has a scene viewport and a 2D rendering system, but those systems do not yet meet in a way that makes 2D scene content feel like part of the world editing surface.

The viewport camera exposes viewport ratios, not an authored canvas resolution. That makes it unsuitable as the source of truth for anchor and padding evaluation, because moving or resizing the viewport would implicitly change layout behavior.

The existing viewport settings overlay already owns viewport-local presentation controls such as grid visibility and camera clip planes. Adding simulated canvas settings anywhere else would fragment viewport configuration and create two different entry points for scene-preview behavior.

The 3D viewport also cannot stop at visual preview. The user must be able to click what they see on the plane, route that click back into the 2D selection system, and then continue editing through the same gizmos already used elsewhere in the editor.

## Interaction Design

### Viewport Presentation

The scene viewport always shows the 2D canvas plane when a scene is open. There is no visibility toggle for the first pass.

The plane behaves as a fixed world object:

- it lies on the XY plane
- its bottom-left corner is at `(0, 0, 0)`
- it faces `+Z`
- its world width is `CanvasWidth / PixelsPerWorldUnit`
- its world height is `CanvasHeight / PixelsPerWorldUnit`

With the default values, the plane size is `12.8 x 7.2` world units.

The editor camera can orbit, pan, and zoom around the plane like any other world object. The plane does not billow, rotate toward the camera, or otherwise behave like screen-space content.

### Viewport Settings Overlay

The existing viewport settings overlay should gain three new controls:

1. `Canvas Width`
2. `Canvas Height`
3. `Pixels Per World Unit`

Those values define the simulated 2D layout canvas and the plane's world scale.

Defaults:

- `Canvas Width = 1280`
- `Canvas Height = 720`
- `Pixels Per World Unit = 100`

These settings are viewport-local editor preview settings. They are not derived from the scene camera viewport ratios.

### Picking And Editing

Picking remains depth-based.

Expected behavior:

- if opaque or otherwise pickable 3D geometry is closer than the plane, that geometry wins
- if the plane is the closest winning hit, the editor treats the click as a 2D canvas click

When the plane wins:

1. the editor resolves the hit position on the plane in world space
2. the editor converts that world-space hit into simulated canvas coordinates
3. the editor forwards those canvas coordinates into the existing 2D selection and hit-test path
4. the existing selection system updates normally
5. the existing gizmos continue to operate on the selected entity

This keeps the plane responsible only for viewport-space bridging. It does not become a second editing model.

## Canvas Model

### Source Of Truth

The simulated canvas size must come from viewport settings, not from camera viewport ratios and not from render-target dimensions inferred elsewhere.

That source of truth is:

- `CanvasWidth`
- `CanvasHeight`
- `PixelsPerWorldUnit`

The 2D layout system should render against `CanvasWidth x CanvasHeight` exactly, because anchor and padding behavior depend on those values staying stable while the user moves the editor camera.

### Validation Rules

All three values must remain valid:

- `CanvasWidth > 0`
- `CanvasHeight > 0`
- `PixelsPerWorldUnit > 0`

The UI should clamp these settings to sane minimums rather than accepting invalid values.

Recommended minimums:

- `CanvasWidth >= 1`
- `CanvasHeight >= 1`
- `PixelsPerWorldUnit >= 1`

The first pass should avoid free-form text entry. Use the existing viewport-settings control style, such as sliders or step-based controls, so values remain valid and consistent with the rest of the overlay.

## Architecture

### Viewport-Owned Preview Settings

Add one viewport-owned editor model that stores the simulated canvas settings for the scene viewport.

Responsibilities:

- store canvas width
- store canvas height
- store pixels per world unit
- expose current values to the viewport settings overlay
- notify the preview plane pipeline when values change

This state belongs with the viewport because it controls preview behavior, not runtime scene content.

### Canvas Preview Plane Pipeline

Add an editor-only canvas preview pipeline that bridges the 2D scene and 3D viewport.

Responsibilities:

- allocate and own the offscreen render target used for the 2D canvas preview
- render the active 2D scene into that target using the simulated canvas size
- create and update the world-space plane material or texture binding
- keep the plane's world size synchronized with the viewport settings
- expose coordinate conversion helpers between world plane space and canvas pixel space

This preview pipeline should not live in runtime scene systems. It is editor-specific behavior.

### Offscreen Rendering

The active 2D scene should continue rendering through the authoritative 2D path. The new behavior is that this render goes into an offscreen target sized to the simulated canvas instead of being treated only as a screen-space result.

That render target output then becomes the texture shown on the fixed world plane in the scene viewport.

This approach preserves one real 2D layout implementation rather than introducing a second layout engine for world-space preview.

### Scene Viewport Integration

The scene viewport remains responsible for:

- owning the viewport settings overlay
- owning the preview settings state
- hosting the preview plane lifecycle
- triggering preview-plane updates when viewport settings change
- forwarding successful plane hits into the 2D selection bridge

The viewport should not absorb the internal rendering math for the preview plane. That logic belongs in focused editor services or components.

### Input Bridge

Add one editor-only bridge that takes a winning plane hit and converts it into the existing 2D selection input shape.

Responsibilities:

- confirm the plane was the depth-winning pick
- resolve local plane coordinates
- convert local plane coordinates into canvas pixel coordinates
- forward those coordinates into the current 2D hit-test and selection path

The bridge should not duplicate 2D selection logic. The existing 2D editor picking flow remains authoritative after coordinate translation.

## Coordinate Mapping

### World To Canvas

Because the plane bottom-left is at world `(0, 0, 0)`, coordinate mapping stays direct.

Given a world hit on the plane:

- `canvasX = worldX * PixelsPerWorldUnit`
- `canvasY = worldY * PixelsPerWorldUnit`

This assumes the hit is already expressed in the plane's local XY space.

The hit is valid only when:

- `0 <= canvasX < CanvasWidth`
- `0 <= canvasY < CanvasHeight`

Any hit outside those bounds should not produce a 2D selection result.

### Canvas To World

For editor visualization helpers and future overlays, the reverse mapping is:

- `worldX = canvasX / PixelsPerWorldUnit`
- `worldY = canvasY / PixelsPerWorldUnit`

This keeps the plane scale deterministic and reversible.

## Rendering Behavior

### Aspect Ratio

The canvas plane always preserves the simulated canvas aspect ratio exactly.

Behavior:

- the offscreen target is created at the authored canvas size
- the world plane aspect matches the same width and height ratio
- the scene viewport does not stretch the canvas to fill its own aspect ratio

This prevents anchor and padding behavior from drifting just because the docked viewport is resized.

### Empty Or Mixed Scenes

If a scene has no visible 2D content, the plane should still exist as the 2D editing surface.

If a scene has both 2D and 3D content:

- the plane still renders the 2D canvas
- 3D depth continues deciding which object receives the winning click

## Performance Strategy

Correctness comes first.

The first pass may redraw the offscreen canvas preview every frame if that is the most reliable way to land the feature cleanly.

After correctness is established, optimization can reduce cost by:

- recreating the render target only when the simulated canvas size changes
- reusing material and plane resources across normal editor updates
- introducing dirty invalidation for 2D scene changes in a later optimization pass

No first-pass design work should weaken the correctness of preview rendering or picking just to avoid redraw cost prematurely.

## Testing

### Settings Verification

- The viewport settings overlay shows controls for width, height, and pixels per world unit.
- The default values are `1280`, `720`, and `100`.
- Changing width or height updates the offscreen target dimensions and plane world size.
- Changing pixels per world unit updates plane world size without changing canvas pixel layout.

### Rendering Verification

- A 2D-only scene renders onto the world plane with the expected aspect ratio.
- Anchor and padding behavior match the normal 2D layout output for the same simulated canvas size.
- The plane remains anchored with bottom-left at `(0, 0, 0)`.
- The plane remains visible while moving the editor camera around the scene.

### Interaction Verification

- When 3D geometry is in front of the plane, the geometry wins the pick.
- When the plane is the nearest winning hit, the hit converts into the correct canvas coordinates.
- 2D hit testing and selection work through the existing 2D selection path after plane-hit translation.
- Existing transform gizmos continue to work after selecting 2D entities through the plane.

## Implementation Boundaries

The first pass should include:

- viewport-owned simulated canvas settings
- viewport settings overlay controls for those settings
- an always-visible editor-only 2D canvas plane
- offscreen 2D rendering into that plane
- depth-based picking that can resolve the plane as the winning hit
- plane-hit translation into the existing 2D selection path
- reuse of the existing gizmo system after selection

The first pass should not include:

- a dedicated show or hide toggle for the plane
- a billboarded screen-facing plane mode
- a second manipulation system for 2D entities
- a reimplementation of 2D layout rules in world space
- persistence of these settings into runtime scene content
