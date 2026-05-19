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
            EditorWorldSpace2DPreviewRegistry.Clear();
            EditorWorldSpace2DPreviewMeshResources.ResetForTests();
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
        /// Ensures non-viewport-owned authored text drawables leave the scene camera queue when an exact world-preview path is available.
        /// </summary>
        [Fact]
        public void Update_WhenTextHasWorldPreview_RemovesItFromTheSceneCameraQueue() {
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
            drawableEntity.AddComponent(new TextComponent {
                Size = new int2(120, 24),
                Text = "Preview"
            });

            Core.Instance.Update();

            Assert.Equal(new int2(640, 360), presenter.PresentedWorldSize);
            Assert.Equal(0, sceneCamera.RenderQueue2D.Count);
        }

        /// <summary>
        /// Ensures viewport-owned sprite drawables leave the scene camera queue when a world-space preview proxy is available.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteHasWorldPreview_RemovesItFromTheSceneCameraQueue() {
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

            Entity viewportOwnedRoot = new Entity();
            viewportOwnedRoot.InitComponents();
            viewportOwnedRoot.InitChildren();
            viewportOwnedRoot.LayerMask = EditorLayerMasks.SceneObjects;
            viewportOwnedRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = sceneCamera
            });
            sceneCameraEntity.AddChild(viewportOwnedRoot);

            Entity drawableEntity = new Entity();
            drawableEntity.InitComponents();
            drawableEntity.InitChildren();
            drawableEntity.LayerMask = EditorLayerMasks.SceneObjects;
            drawableEntity.AddComponent(new SpriteComponent {
                Size = new int2(32, 32),
                Texture = TextureUtils.PixelTexture
            });
            viewportOwnedRoot.AddChild(drawableEntity);

            Core.Instance.Update();

            Assert.Equal(new int2(640, 360), presenter.PresentedWorldSize);
            Assert.Equal(0, sceneCamera.RenderQueue2D.Count);
        }

        /// <summary>
        /// Ensures viewport-owned rounded-rectangle drawables leave the scene camera queue when an exact world-preview path is available.
        /// </summary>
        [Fact]
        public void Update_WhenRoundedRectHasWorldPreview_RemovesItFromTheSceneCameraQueue() {
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

            Entity viewportOwnedRoot = new Entity();
            viewportOwnedRoot.InitComponents();
            viewportOwnedRoot.InitChildren();
            viewportOwnedRoot.LayerMask = EditorLayerMasks.SceneObjects;
            viewportOwnedRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = sceneCamera
            });
            sceneCameraEntity.AddChild(viewportOwnedRoot);

            Entity drawableEntity = new Entity();
            drawableEntity.InitComponents();
            drawableEntity.InitChildren();
            drawableEntity.LayerMask = EditorLayerMasks.SceneObjects;
            drawableEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(32, 32)
            });
            viewportOwnedRoot.AddChild(drawableEntity);

            Core.Instance.Update();

            Assert.Equal(new int2(640, 360), presenter.PresentedWorldSize);
            Assert.Equal(0, sceneCamera.RenderQueue2D.Count);
        }
    }
}
