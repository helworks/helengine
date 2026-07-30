# Viewport Gizmo Label Drag Freeze Design

## Goal

Keep transform-gizmo axis-label billboards synchronized with the translation-arrow lifecycle. While a translation gizmo drag is active, labels must retain the text, position, orientation, and scale that were last applied before the drag. They refresh only after the drag finishes.

## Current behavior

`TransformTranslationGizmoFollowComponent` uses `EditorGizmoDragService.IsDragging` to avoid recalculating arrow facing and scale during a drag. `EditorViewportCameraAngleOverlayComponent` independently recomputes its axis labels every update, so their signed text and billboard transforms can change while the arrows remain frozen.

## Design

`EditorViewportCameraAngleOverlayComponent.UpdateAxisLabels` will query the same camera-scoped `EditorGizmoDragService.IsDragging` state used by the translation-gizmo follow component.

When no drag is active, the overlay continues resolving and applying all three label models and transforms normally. When a drag is active, it will not apply a newly calculated label model, position, orientation, or scale. The last non-drag label state remains rendered until drag completion. Visibility rules remain active so switching away from translation mode or clearing the selection still hides labels immediately.

## Tests

Add an overlay-component regression test that establishes label state, starts a drag for the same camera, changes the camera/selection inputs, and verifies that each label entity retains its prior model and transform. Ending the drag and updating again must refresh the label state.

## Scope

No changes to transform drag mechanics, arrow mesh construction, picker behavior, or render-queue ownership. The shared drag service remains the single lifecycle authority.
