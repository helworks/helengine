### Task 1: Optional orthographic projection and desktop rendering

**Files:**
- Create `engine/helengine.core/model/CameraProjectionMode.cs`.
- Create `engine/helengine.core/model/interfaces/ICameraProjectionSettings.cs`.
- Modify `engine/helengine.core/utils/CameraProjectionUtils.cs`.
- Create `engine/helengine.editor/components/EditorViewportCameraComponent.cs`.
- Modify `engine/helengine.directx11/DirectX11Renderer3D.cs` and `engine/helengine.vulkan/VulkanRenderer3D.cs`.
- Extend `engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs`.

**Interfaces:** `CameraProjectionMode` has `Perspective = 0` and `Orthographic = 1`. `ICameraProjectionSettings` exposes get/set `ProjectionMode` and float `OrthographicVerticalSpan`. `EditorViewportCameraComponent : CameraComponent, ICameraProjectionSettings` explicitly opts into editor execution. Add `CameraProjectionUtils.CreateProjection(ICamera camera, float aspectRatio)` and `GetWorldUnitsPerPixel(ICamera camera, double distance, double viewportHeight)`; keep both existing perspective overloads.

- [ ] Add tests `CreateProjection_OrdinaryCamera_MatchesExistingPerspective`, `CreateProjection_Orthographic_MapsVisibleExtentsAndClipPlanes`, and `WorldUnitsPerPixel_Orthographic_IsIndependentOfDistance`. Assert matrix equivalence for ordinary cameras, visible extent ±span/2, correct near/far depth, and a span of 20 over 1000 pixels yielding 0.02 world units/pixel.
- [ ] Add invalid-dimension/span cases and actual-FOV cases; run filter `FullyQualifiedName~CameraProjectionUtilsTests` and confirm red.
- [ ] Implement the optional contract and helpers using existing depth conventions. New editor camera defaults to perspective; opt-in properties are editor-only and ignored by scene persistence. Reject non-finite/invalid direct API input; workspace migration handles absent legacy values explicitly.
- [ ] Route desktop scene and ID-picking projection creation through `CreateProjection`. Leave shadow projection code unchanged. Run tests green and build both desktop backend projects using the same artifacts root.
- [ ] Review and commit only this task's files: `feat(editor): support optional orthographic viewport projection`.

