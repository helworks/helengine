# Scene Settings Canvas Profile Design

## Goal

Move authored 2D presentation resolution out of camera viewports and into one scene-owned settings model so anchors, 2D layout, and preview rendering all resolve against a single logical canvas.

## Problem

The current editor mixes two different concerns:

- camera viewport size
- logical 2D authoring surface size

That works poorly for authored 2D scenes such as `DemoDiscMainMenu`, because shrinking the camera viewport to fit one preview panel also shrinks the coordinate system the 2D content was authored against. The result is that anchors and other screen-space layout systems cannot reliably represent the authored scene surface.

There is already an editor-only `EditorViewportCanvasPreviewSettings` model, but it is viewport-local and therefore not the correct place to store authored scene intent.

## Decision

Each scene will own exactly one scene-level settings payload. The first setting in that payload is a single `CanvasProfile` with:

- `CanvasWidth`
- `CanvasHeight`

This is the logical 2D authoring surface for the scene. It is not a camera property and it is not a renderer-specific implementation detail.

## Architecture

### Scene-Owned Settings

`SceneAsset` gains one scene-level settings payload that persists with the scene asset itself rather than through an entity or component.

The first version of that payload contains one `CanvasProfile`:

- `CanvasWidth`
- `CanvasHeight`

There is exactly one active profile per scene. The system does not support multiple named scene profiles in this slice.

### Camera Responsibility

Cameras keep responsibility for:

- world view
- draw order
- layer filtering
- render-target placement

Cameras stop being the source of truth for the logical 2D authoring resolution.

The practical rule is:

- preview and presentation systems choose render-target size from the scene canvas profile
- cameras render into that chosen surface
- viewport rectangles still describe where the camera lands on the target

### Renderer Responsibility

Render backends consume the logical scene canvas but remain free to realize it however they want.

That means future rendering modes such as tiled rendering, lower internal resolution, or platform-specific presentation paths must not be encoded into `CanvasProfile`. Those remain backend execution details layered on top of the scene’s logical presentation contract.

## Editor UX

### Scene Settings Entry Point

The editor gains a dedicated `Scene Settings` entry point for the active scene. This is separate from entity/component properties.

The first scene settings section is `Canvas Profile` with:

- `Canvas Width`
- `Canvas Height`

### Behavior

When the active scene settings change:

- the active scene preview updates immediately
- scene-view canvas simulation updates immediately
- anchor/layout evaluation uses the new logical canvas size

When the scene is saved:

- scene settings persist into the scene asset

When the scene is loaded:

- scene settings are restored before preview/layout systems consume them

## Preview Integration

### Camera Preview

Camera preview render targets use the active scene `CanvasProfile` dimensions, not the preview panel pixel size.

The preview panel still displays the resulting texture scaled to fit its available area, but the authored render surface remains the logical scene canvas.

### Scene View Canvas Simulation

The existing viewport canvas simulation path should read from scene settings instead of an editor-local preview-only width/height store.

The existing overlay and controls can stay conceptually similar, but they must edit scene-owned canvas settings instead of one viewport-local scratch model.

## Persistence

### Scene Asset Format

`SceneAsset` is extended with a scene settings payload. The first persisted version stores:

- `CanvasWidth`
- `CanvasHeight`

The initial migration rule is:

- scenes without persisted settings load with the current default logical canvas size

This preserves existing scenes without forcing immediate hand migration.

## Forward Compatibility

This design intentionally reserves scene settings as the place for future scene-level authored metadata, for example:

- aspect handling policy
- safe-area data
- presentation scale mode

Those are explicitly out of scope for this slice, but the storage and UI structure must not block them.

## Non-Goals

This slice does not:

- add multiple named canvas profiles per scene
- add per-parent or per-subtree UI root components
- encode tiled rendering or hardware-specific render partitioning into the scene profile
- redesign camera projection behavior beyond separating logical canvas size from camera viewport ownership

## Testing

The implementation must cover:

- scene load/save persistence for `CanvasWidth` and `CanvasHeight`
- default fallback when older scenes do not contain scene settings
- editor preview reading scene-owned canvas settings
- immediate editor update when scene settings change
- menu/2D scenes authored for `1280x720` continuing to render correctly when preview panels are smaller than the authored canvas
- replacement or migration of existing `EditorViewportCanvasPreviewSettings` tests so they verify scene-owned behavior rather than viewport-local behavior

## Recommended Rollout

Implement in this order:

1. Add scene-level settings model and persistence on `SceneAsset`.
2. Route editor scene load/save through the new settings payload.
3. Move preview and scene-view canvas simulation to read from scene settings.
4. Add the scene settings UI for editing `Canvas Width` and `Canvas Height`.
5. Remove or reduce the old viewport-local canvas-resolution ownership path once the scene-owned flow is complete.
