using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport picker behavior when multiple editor viewports coexist.
    /// </summary>
    public sealed class EditorViewportPickerTests {
        /// <summary>
        /// Shared no-op additional gizmo entity resolver used by picker unit tests.
        /// </summary>
        /// <returns>Empty list of additional gizmo entities.</returns>
        static IReadOnlyList<EditorEntity> ResolveNoAdditionalOwnedEntities() {
            return Array.Empty<EditorEntity>();
        }

        /// <summary>
        /// Ensures one inactive picker does not clear gizmo hover that belongs to another viewport.
        /// </summary>
        [Fact]
        public void Update_WhenPointerIsOutsideViewport_DoesNotClearHoverOwnedByAnotherViewport() {
            TestInputBackend inputBackend = new TestInputBackend();
            Core core = new Core();

            try {
                core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), inputBackend, new PlatformInfo("test", "test-version"));
                EditorInputCaptureService.Reset();
                EditorGizmoHoverService.ClearHoveredHandle();

                EditorEntity sceneCameraEntity = new EditorEntity();
                CameraComponent sceneCamera = new CameraComponent {
                    Viewport = new float4(0f, 0f, 100f, 100f)
                };
                sceneCameraEntity.AddComponent(sceneCamera);

                CameraComponent gizmoCamera = new CameraComponent {
                    Viewport = new float4(0f, 0f, 100f, 100f)
                };
                sceneCameraEntity.AddComponent(gizmoCamera);

                EditorEntity pickerCameraEntity = new EditorEntity();
                CameraComponent pickerCamera = new CameraComponent();
                pickerCameraEntity.AddComponent(pickerCamera);
                EditorEntity translationGizmoRoot = new EditorEntity();
                EditorEntity rotationGizmoRoot = new EditorEntity();
                EditorEntity scaleGizmoRoot = new EditorEntity();
                EditorViewportGizmoDrawableCollector gizmoDrawableCollector = new EditorViewportGizmoDrawableCollector(
                    ResolveNoAdditionalOwnedEntities,
                    translationGizmoRoot,
                    rotationGizmoRoot,
                    scaleGizmoRoot);

                EditorViewportPicker picker = new EditorViewportPicker(
                    sceneCamera,
                    gizmoCamera,
                    gizmoDrawableCollector,
                    pickerCameraEntity,
                    pickerCamera,
                    TestDirectX11RenderManager3D.Create());
                sceneCameraEntity.AddComponent(picker);

                EditorEntity hoveredHandle = new EditorEntity();
                EditorGizmoHoverService.SetHoveredHandle(hoveredHandle);
                inputBackend.SetMouseState(new MouseState(
                    180,
                    180,
                    0,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released));
                inputBackend.EarlyUpdate();

                picker.Update();

                Assert.Same(hoveredHandle, EditorGizmoHoverService.HoveredHandleEntity);
            } finally {
                EditorGizmoHoverService.ClearHoveredHandle();
                EditorInputCaptureService.Reset();
                core.Dispose();
            }
        }
    }
}
