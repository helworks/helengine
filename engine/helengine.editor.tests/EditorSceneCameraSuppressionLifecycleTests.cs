using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-authored scene-camera suppression survives later camera lifecycle changes that would otherwise re-register the camera for runtime rendering.
    /// </summary>
    public sealed class EditorSceneCameraSuppressionLifecycleTests : IDisposable {
        /// <summary>
        /// Initializes the core runtime used by the suppression lifecycle tests.
        /// </summary>
        public EditorSceneCameraSuppressionLifecycleTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core runtime after each test so later tests start from an empty object manager.
        /// </summary>
        public void Dispose() {
            if (Core.Instance != null) {
                Core.Instance.Dispose();
            }
        }

        /// <summary>
        /// Ensures one suppressed scene camera stays out of the runtime camera list after later draw-order changes, layer-mask edits, and enabled-state transitions.
        /// </summary>
        [Fact]
        public void Suppressed_scene_camera_does_not_reregister_after_property_and_enabled_state_changes() {
            EditorEntity entity = new EditorEntity();
            entity.InitComponents();

            CameraComponent camera = new CameraComponent {
                CameraDrawOrder = 4,
                LayerMask = 0x1234
            };
            entity.AddComponent(camera);

            Assert.Single(Core.Instance.ObjectManager.Cameras);
            Assert.True(EditorSceneCameraSuppressionService.AttachAndSuppress(entity));
            Assert.Empty(Core.Instance.ObjectManager.Cameras);

            camera.CameraDrawOrder = 8;
            camera.LayerMask = 0x4321;
            Assert.Empty(Core.Instance.ObjectManager.Cameras);

            entity.Enabled = false;
            Assert.Empty(Core.Instance.ObjectManager.Cameras);

            entity.Enabled = true;
            Assert.Empty(Core.Instance.ObjectManager.Cameras);
        }
    }
}
