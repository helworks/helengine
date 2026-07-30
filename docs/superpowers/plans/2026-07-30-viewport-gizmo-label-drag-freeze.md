# Viewport Gizmo Label Drag-Facing Implementation Plan

**Goal:** Axis labels move with the translation gizmo during a drag while retaining the arrows' current signed-axis facing until release.

**Architecture:** The translation-gizmo follow component continues to own scale and arrow-facing updates. It exposes the snapped yaw it has applied to the handles. The camera-angle overlay resolves the presented gizmo anchor on every frame; during an active drag it uses the follow component's applied yaw rather than independently recomputing it.

## Implementation

1. Add `CurrentYawFacingOrientation` to `TransformTranslationGizmoFollowComponent` and update it whenever handle facing is applied.
2. Resolve the overlay's axis-label origin through `EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition` each frame.
3. During an active drag, resolve label axis directions from the follow component's exposed applied yaw. Outside a drag, compute snapped yaw normally.
4. Remove the whole-label update gate: position, billboard placement, and existing scale behavior must remain live while drag-facing is frozen.
5. Extend the existing translation-gizmo drag test to assert that the applied yaw remains stable during drag and updates after release.

## Validation

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportCameraAngleOverlayComponentTests|FullyQualifiedName~TransformTranslationGizmoFollowComponentTests"
```

Expected: all discovered tests pass.
