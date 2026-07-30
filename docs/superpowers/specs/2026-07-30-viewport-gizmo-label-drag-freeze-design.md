# Viewport Gizmo Label Drag-Facing Design

## Goal

Keep transform-gizmo axis labels spatially attached to the moving translation gizmo while preserving the same signed-axis facing as the translation arrows throughout a drag. The labels update their facing and text only after the drag ends.

## Current behavior

`TransformTranslationGizmoFollowComponent` updates its root position before checking `EditorGizmoDragService.IsDragging`, so the translation gizmo follows the selected entity during a drag. It deliberately avoids recalculating handle yaw and scale until the drag ends.

`EditorViewportCameraAngleOverlayComponent` must follow that split lifecycle. Freezing its entire label transform leaves the labels behind; independently recalculating the yaw changes the signed text before the arrows rotate.

## Design

The translation-gizmo follow component retains the snapped yaw orientation it actually applies to its handles. The axis-label overlay uses the presented world-space selection anchor on every update, including active drags, so the labels move together with the gizmo.

While a drag is active, the overlay takes the yaw orientation from the translation-gizmo follow component instead of recomputing it. This keeps axis directions and labels aligned with the arrows. After the drag ends, both systems resume resolving the snapped yaw from the current camera and gizmo position. Existing frozen scale behavior remains unchanged.

## Tests

Extend the translation-gizmo follow test to verify that its exposed applied yaw remains unchanged through a drag and changes once the drag ends. Retain overlay axis-direction coverage, and run both affected test classes together.

## Scope

No changes to transform drag mechanics, arrow mesh construction, picker behavior, or render-queue ownership. `EditorGizmoDragService` remains the single authority for drag lifetime.
