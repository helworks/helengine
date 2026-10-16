### Task 2: Projection-aware rays, camera motion, framing, and gizmos

**Files:**
- Modify `engine/helengine.editor/managers/gizmo/EditorViewportPointerRayBuilder.cs`.
- Modify `engine/helengine.editor/components/EditorViewportCameraController.cs` and `EditorViewportPicker.cs`.
- Modify `engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs`.
- Modify translation/rotation/scale drag and follow components in `engine/helengine.editor/managers/gizmo/`, plus `TransformScaleGizmoScaleResolver.cs` where its scale formula is selected.
- Modify `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`.
- Extend matching tests: `EditorViewportPointerRayBuilderTests`, `EditorViewportCameraControllerTests`, `EditorViewportSelectionFramingServiceTests`, gizmo drag/follow/scale tests, and `EditorViewportCameraAngleOverlayComponentTests`.

**Interfaces:** Add `TryBuildCameraRay(CameraComponent camera, int2 pointer, out float3 origin, out float3 direction)` using existing viewport coordinate conventions. Keep the existing explicitly perspective-named API compatible for its callers. Add `EditorViewportCameraController.SetViewPose(float3 pivot, float4 orientation, double distance)` to atomically synchronize pose, pivot, distance, and cached yaw/pitch. Add `SetProjectionMode(CameraProjectionMode mode)` using task 1's optional settings. Expose the existing controller's navigation activity through a read-only boolean for transition cancellation.

- [ ] Add tests for parallel orthographic rays with different origins, perspective rays with a common origin, off-center viewports, zero viewport dimensions, and non-default FOV. Run the pointer-ray filter red.
- [ ] Add tests for projection round trips preserving pivot/orientation and projected scale; orthographic pan, wheel zoom, and focus-selection with portrait/wide viewports. Include tiny and off-origin selections. Run controller/framing filters red.
- [ ] Implement span/distance conversion from the spec, orthographic zoom and pan, and projection-aware framing. Reject navigation on zero-size viewports. Use camera FOV instead of fixed 45-degree assumptions.
- [ ] Route all transform tools through the projection-aware ray builder and use task 1's world-units-per-pixel calculation for gizmo and label scaling. Preserve frozen drag-time scale behavior.
- [ ] Copy optional projection settings in picker synchronization. Add behavioral tests that dragging in orthographic view maps pixels to expected world displacement and that gizmo/label size stays constant on screen. Run `FullyQualifiedName~ViewportPointerRay|FullyQualifiedName~ViewportCameraController|FullyQualifiedName~SelectionFraming|FullyQualifiedName~Gizmo|FullyQualifiedName~CameraAngleOverlay` green.
- [ ] Review and commit only the task diff: `feat(editor): make viewport interaction projection-aware`.

