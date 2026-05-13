using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that camera layer-mask changes keep render queues aligned with the camera's visible layers.
    /// </summary>
    public class CameraComponentLayerMaskTests {
        /// <summary>
        /// Ensures removing the scene-grid layer from an enabled camera also removes matching drawables from that camera queue.
        /// </summary>
        [Fact]
        public void LayerMask_WhenSceneGridLayerIsRemoved_StopsRenderingSceneGridDrawables() {
            InitializeCore();
            Entity cameraEntity = CreateCameraEntity((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid));
            CameraComponent camera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components));
            Entity gridEntity = CreateMeshEntity(EditorLayerMasks.SceneGrid);

            Assert.Equal(1, camera.RenderQueue3D.Count);

            camera.LayerMask = EditorLayerMasks.SceneObjects;

            Assert.Equal(0, camera.RenderQueue3D.Count);
        }

        /// <summary>
        /// Initializes a core instance with test render managers so entities can register cameras and drawables.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates an enabled camera entity registered with the object manager using the supplied layer mask.
        /// </summary>
        /// <param name="layerMask">Layer mask exposed by the created camera.</param>
        /// <returns>Camera entity containing one registered camera component.</returns>
        Entity CreateCameraEntity(ushort layerMask) {
            var entity = new Entity();
            entity.InitComponents();

            var camera = new CameraComponent {
                LayerMask = layerMask
            };
            entity.AddComponent(camera);

            return entity;
        }

        /// <summary>
        /// Creates an enabled mesh entity registered on the supplied layer mask.
        /// </summary>
        /// <param name="layerMask">Layer mask assigned to the mesh entity.</param>
        /// <returns>Mesh entity containing one registered mesh component.</returns>
        Entity CreateMeshEntity(ushort layerMask) {
            var entity = new Entity {
                LayerMask = layerMask
            };
            entity.InitComponents();

            var mesh = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial()
            };
            entity.AddComponent(mesh);

            return entity;
        }
    }
}
