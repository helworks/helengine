using System.Reflection;
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

        /// <summary>
        /// Ensures picker projection synchronization mirrors the gizmo camera clip range so large scene-view gizmos remain hoverable when the visible overlay camera frames far-away content.
        /// </summary>
        [Fact]
        public void SynchronizePickerCameraProjection_WhenGizmoCameraUsesExtendedClipRange_MirrorsClipPlanesOntoPickerCamera() {
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
                    Viewport = new float4(0f, 0f, 100f, 100f),
                    NearPlaneDistance = 2f,
                    FarPlaneDistance = 8000f
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

                InvokeSynchronizePickerCameraProjection(picker, gizmoCamera);

                Assert.Equal(gizmoCamera.NearPlaneDistance, pickerCamera.NearPlaneDistance);
                Assert.Equal(gizmoCamera.FarPlaneDistance, pickerCamera.FarPlaneDistance);
            } finally {
                EditorGizmoHoverService.ClearHoveredHandle();
                EditorInputCaptureService.Reset();
                core.Dispose();
            }
        }

        /// <summary>
        /// Invokes the private picker projection-synchronization seam for one picker instance.
        /// </summary>
        /// <param name="picker">Picker instance whose hidden camera projection should be synchronized.</param>
        /// <param name="sourceCamera">Source camera whose clip range should be mirrored.</param>
        static void InvokeSynchronizePickerCameraProjection(EditorViewportPicker picker, CameraComponent sourceCamera) {
            if (picker == null) {
                throw new ArgumentNullException(nameof(picker));
            }
            if (sourceCamera == null) {
                throw new ArgumentNullException(nameof(sourceCamera));
            }

            MethodInfo method = typeof(EditorViewportPicker).GetMethod("SynchronizePickerCameraProjection", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private SynchronizePickerCameraProjection method was not found.");
            }

            method.Invoke(picker, new object[] { sourceCamera });
        }
    }
}
