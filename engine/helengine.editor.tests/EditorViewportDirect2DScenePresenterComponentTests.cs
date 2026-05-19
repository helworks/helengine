using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies direct 2D scene presentation behavior for editor viewports.
    /// </summary>
    public sealed class EditorViewportDirect2DScenePresenterComponentTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the direct scene-presentation tests.
        /// </summary>
        public EditorViewportDirect2DScenePresenterComponentTests() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures direct scene presentation uses the resolved scene viewport size as its world-presented 2D size.
        /// </summary>
        [Fact]
        public void Update_WhenSceneViewportIs1280By720_UsesMatchingWorldPresented2DSize() {
            EditorEntity sceneCameraEntity = new EditorEntity();
            sceneCameraEntity.InitComponents();
            sceneCameraEntity.InitChildren();
            CameraComponent sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f)
            };
            sceneCameraEntity.AddComponent(sceneCamera);
            ViewportComponent sceneViewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = sceneCamera
            };
            sceneCameraEntity.AddComponent(sceneViewportComponent);
            EditorViewportDirect2DScenePresenterComponent presenter = new EditorViewportDirect2DScenePresenterComponent(sceneCamera, sceneViewportComponent);
            sceneCameraEntity.AddComponent(presenter);

            Core.Instance.Update();

            Assert.Equal(new int2(1280, 720), presenter.PresentedWorldSize);
        }

        /// <summary>
        /// Ensures authored scene 2D drawables stay on the scene camera queue when the viewport uses direct 2D presentation.
        /// </summary>
        [Fact]
        public void Update_WhenDirectPresenterIsActive_KeepsScene2DDrawablesOnTheSceneCameraQueue() {
            EditorEntity sceneCameraEntity = new EditorEntity();
            sceneCameraEntity.InitComponents();
            sceneCameraEntity.InitChildren();
            CameraComponent sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 640f, 360f),
                LayerMask = EditorLayerMasks.SceneObjects
            };
            sceneCameraEntity.AddComponent(sceneCamera);
            ViewportComponent sceneViewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = sceneCamera
            };
            sceneCameraEntity.AddComponent(sceneViewportComponent);
            EditorViewportDirect2DScenePresenterComponent presenter = new EditorViewportDirect2DScenePresenterComponent(sceneCamera, sceneViewportComponent);
            sceneCameraEntity.AddComponent(presenter);

            Entity drawableEntity = new Entity();
            drawableEntity.InitComponents();
            drawableEntity.InitChildren();
            drawableEntity.LayerMask = EditorLayerMasks.SceneObjects;
            drawableEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(32, 32)
            });
            sceneCameraEntity.AddChild(drawableEntity);

            Core.Instance.Update();

            Assert.Equal(new int2(640, 360), presenter.PresentedWorldSize);
            Assert.Equal(1, sceneCamera.RenderQueue2D.Count);
        }
    }
}
