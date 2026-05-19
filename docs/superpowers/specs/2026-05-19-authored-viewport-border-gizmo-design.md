# Authored Viewport Border Gizmo Design

## Goal

Add an editor-only world-space border gizmo for every authored `ViewportComponent` so scene-view users can see where each viewport lives in 3D space.

## Scope

- Editor side only.
- No `helengine.core` behavior changes.
- No runtime/export impact.
- No picking changes beyond existing internal-entity filtering.

## Behavior

- Every authored `ViewportComponent` gets a matching internal editor gizmo entity.
- The gizmo is a non-solid border rectangle.
- The gizmo sits on the authored viewport entity's local XY plane at `z=0`.
- The gizmo width and height come from `ViewportComponent.ResolvedViewportSize`.
- Internal editor viewport infrastructure does not get gizmos.

## Rendering

- Render the gizmo as an unlit border-only material on a centered plane mesh.
- Use shader masking so only the border is visible and the center stays transparent.
- Keep the gizmo double-sided and alpha blended for editor visibility.

## Integration

- Attach the synchronization component to each editor scene viewport stack alongside the other editor-only scene-view helpers.
- Keep the feature isolated from runtime scene content and game builds.

## Verification

- Add editor tests proving authored viewports create one gizmo and internal editor viewports do not.
- Add editor tests proving the gizmo follows resolved viewport size and source transform.
- Rebuild `helengine.editor.app`.
