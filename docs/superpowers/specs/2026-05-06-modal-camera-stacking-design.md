# Modal Camera Stacking Design

## Summary

Fix editor modal layering at the camera-system level so modal dialogs always render above secondary panel-owned UI content cameras.

## Current Behavior

The editor currently uses:

- one shared editor UI camera on `EditorLayerMasks.EditorUi`
- one separate Scene Hierarchy content camera on `EditorLayerMasks.SceneHierarchyContent`

Both cameras currently use `CameraDrawOrder = 255`.
That means modal dialog render order is only well-defined inside the shared editor UI camera, but not against panel content rendered by a separate camera.
As a result, Scene Hierarchy row content can appear above modal dialogs even though the dialog components use modal render orders.

## Goals

- Define a shared camera stacking rule for editor UI.
- Ensure modal dialogs always render above panel-owned secondary UI content cameras.
- Keep dedicated panel content cameras where they are still useful for viewport clipping and virtualization.
- Add regression coverage that proves Scene Hierarchy content cannot visually outrank a modal.

## Non-Goals

- No refactor of Scene Hierarchy row virtualization.
- No migration of panel content to the shared editor UI camera unless later work requires it.
- No panel-specific modal suppression workaround.

## Design

### Camera Tier Rule

Introduce explicit editor UI camera tiers:

- primary editor UI camera tier
- secondary panel-content camera tier
- modal dialog tier

The important rule is that modal dialog rendering must happen after any panel-content camera rendering.

In the current codebase, that means the shared editor UI camera used for modal dialogs must be assigned a draw order strictly greater than the draw order used by `SceneHierarchyPanel`'s `contentCameraComponent`.

### Ownership

The draw-order constants should live in shared editor infrastructure rather than inside `SceneHierarchyPanel`.
The panel should consume a shared non-modal panel-content camera priority instead of hardcoding `255`.

The modal behavior should remain implicit through the shared editor UI camera used by modal dialog entities.
No panel should need to know whether a modal is open in order to stack correctly.

### Initial Scope

Current scope covers:

- `EditorSession` shared editor UI camera ordering
- `SceneHierarchyPanel` content camera ordering

This is still a general fix because it establishes the rule that any future panel-owned content camera must render below the modal camera tier.

## Testing

Add regression coverage that proves:

- Scene Hierarchy content uses the shared non-modal content-camera draw-order tier
- the shared editor UI camera uses a strictly later draw-order tier than Scene Hierarchy content
- modal dialog visuals therefore remain above hierarchy content

The tests should validate the tier relationship directly and, where practical, through one modal-over-hierarchy scenario.

## Risks

- If another panel later introduces a dedicated content camera and bypasses the shared tier constants, this regression can return.
- If any existing non-modal overlay currently depends on matching the shared UI camera draw order exactly, the new tier constants may require one small adjustment in that feature.
