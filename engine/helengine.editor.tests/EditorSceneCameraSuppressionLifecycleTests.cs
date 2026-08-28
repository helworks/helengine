using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-authored scene-camera suppression survives later camera lifecycle changes that would otherwise re-register the camera for runtime rendering.
    /// </summary>
    public sealed class EditorSceneCameraSuppressionLifecycleTests : IDisposable {
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Initializes the core runtime used by the suppression lifecycle tests.
        /// </summary>
        public EditorSceneCameraSuppressionLifecycleTests() {
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
        }

        /// <summary>
        /// Disposes the active core runtime after each test so later tests start from an empty object manager.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures one suppressed scene camera stays out of the runtime camera list after later draw-order changes, layer-mask edits, and enabled-state transitions.
        /// </summary>
        [Fact]
        public void Suppressed_scene_camera_does_not_reregister_after_property_and_enabled_state_changes() {
            EditorEntity entity = new EditorEntity();

            CameraComponent camera = new CameraComponent {
                CameraDrawOrder = 4,
                LayerMask = 0x1234
            };
            entity.AddComponent(camera);

            Assert.Single(CoreValue.ObjectManager.Cameras);
            Assert.True(EditorSceneCameraSuppressionService.AttachAndSuppress(entity, GeneratedAssetGraph.ObjectManager));
            Assert.Empty(CoreValue.ObjectManager.Cameras);

            camera.CameraDrawOrder = 8;
            camera.LayerMask = 0x4321;
            Assert.Empty(CoreValue.ObjectManager.Cameras);

            entity.Enabled = false;
            Assert.Empty(CoreValue.ObjectManager.Cameras);

            entity.Enabled = true;
            Assert.Empty(CoreValue.ObjectManager.Cameras);
        }
    }
}
