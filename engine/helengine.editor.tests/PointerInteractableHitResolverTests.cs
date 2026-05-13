using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shared 2D interactable hit resolution independent of the live pointer-update loop.
    /// </summary>
    public class PointerInteractableHitResolverTests {
        /// <summary>
        /// Ensures overlapping interactables resolve to the visually top-most render order.
        /// </summary>
        [Fact]
        public void ResolveTopInteractableAt_WhenTwoInteractablesOverlap_PrefersHigherRenderOrder() {
            InitializeCore();
            CameraComponent camera = CreateCamera(new float4(0f, 0f, 320f, 180f), EditorLayerMasks.EditorUi);
            InteractableComponent backInteractable = CreateInteractableEntity(new float3(10f, 20f, 0f), new int2(100, 60), 2);
            InteractableComponent frontInteractable = CreateInteractableEntity(new float3(10f, 20f, 0f), new int2(100, 60), 7);

            IInteractable2D hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                camera,
                40,
                50);

            Assert.Same(frontInteractable, hit);
        }

        /// <summary>
        /// Ensures relative pointer coordinates subtract only the interactable's window-space origin.
        /// </summary>
        [Fact]
        public void GetRelativePointerForInteractable_WhenCameraViewportOffsetsPointer_UsesWindowSpaceEntityPosition() {
            InitializeCore();
            CameraComponent camera = CreateCamera(new float4(100f, 50f, 320f, 180f), EditorLayerMasks.EditorUi);
            InteractableComponent interactable = CreateInteractableEntity(new float3(132f, 68f, 0f), new int2(40, 30), 3);

            PointerInteractableHitResolver.GetRelativePointerForInteractable(interactable, 180, 120, camera, out int relativeX, out int relativeY);

            Assert.Equal(48, relativeX);
            Assert.Equal(52, relativeY);
        }

        /// <summary>
        /// Ensures hit resolution still finds a window-space interactable that renders inside an offset viewport.
        /// </summary>
        [Fact]
        public void ResolveTopInteractableAt_WhenCameraViewportOffsetsPointer_UsesWindowSpaceEntityPosition() {
            InitializeCore();
            CameraComponent camera = CreateCamera(new float4(100f, 50f, 320f, 180f), EditorLayerMasks.EditorUi);
            InteractableComponent interactable = CreateInteractableEntity(new float3(132f, 68f, 0f), new int2(80, 80), 3);

            IInteractable2D hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                camera,
                180,
                120);

            Assert.Same(interactable, hit);
        }

        /// <summary>
        /// Ensures clipped descendants do not block hit testing for controls above the viewport.
        /// </summary>
        [Fact]
        public void ResolveTopInteractableAt_WhenAClippedRowOverlapsTheToolbar_IgnoresTheRowOutsideItsViewport() {
            InitializeCore();
            CameraComponent camera = CreateCamera(new float4(0f, 0f, 320f, 180f), EditorLayerMasks.EditorUi);

            InteractableComponent toolbarInteractable = CreateInteractableEntity(new float3(0f, 0f, 0f), new int2(120, 24), 2);

            EditorEntity clipHost = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                Position = new float3(0f, 24f, 0f)
            };

            ScrollComponent scrollComponent = new ScrollComponent {
                Size = new int2(120, 24)
            };
            clipHost.AddComponent(scrollComponent);

            EditorEntity contentRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                LocalPosition = new float3(0f, -24f, 0f)
            };
            clipHost.AddChild(contentRoot);
            scrollComponent.ContentRoot = contentRoot;

            EditorEntity rowEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                LocalPosition = float3.Zero
            };
            contentRoot.AddChild(rowEntity);

            SpriteComponent rowSprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = new int2(120, 24),
                RenderOrder2D = 7
            };
            rowEntity.AddComponent(rowSprite);

            InteractableComponent rowInteractable = new InteractableComponent {
                Size = new int2(120, 24)
            };
            rowEntity.AddComponent(rowInteractable);

            IInteractable2D hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                camera,
                12,
                12);

            Assert.Same(toolbarInteractable, hit);
        }

        /// <summary>
        /// Ensures reference-canvas UI scaled into one matching-aspect `853x480` window keeps pointer hit regions aligned with the visible button.
        /// </summary>
        [Fact]
        public void ResolveTopInteractableAt_WhenReferenceCanvasFitsTo853x480_HitsTheScaledButton() {
            TestRenderManager3D renderManager = InitializeCoreWithWindow(853, 480);
            CameraComponent camera = CreateCamera(new float4(0f, 0f, 1f, 1f), EditorLayerMasks.EditorUi);

            EditorEntity root = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };
            root.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });
            root.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            EditorEntity buttonEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                LocalPosition = new float3(160f, 240f, 0f)
            };
            root.AddChild(buttonEntity);

            SpriteComponent sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = new int2(400, 120),
                RenderOrder2D = 5
            };
            buttonEntity.AddComponent(sprite);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(400, 120)
            };
            buttonEntity.AddComponent(interactable);

            renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
            Core.Instance.Update();

            IInteractable2D hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                camera,
                180,
                180);

            Assert.Same(interactable, hit);
        }

        /// <summary>
        /// Initializes the lightweight core services required by pointer hit-resolution tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Initializes the lightweight core services required by pointer hit-resolution tests using one real 3D render manager window size.
        /// </summary>
        /// <param name="windowWidth">Window width exposed to viewport resolution.</param>
        /// <param name="windowHeight">Window height exposed to viewport resolution.</param>
        /// <returns>Render manager used to drive future window-resize notifications.</returns>
        TestRenderManager3D InitializeCoreWithWindow(int windowWidth, int windowHeight) {
            Core core = new Core();
            TestRenderManager3D renderManager = new TestRenderManager3D();
            core.Initialize(renderManager, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            renderManager.OnWindowResize(IntPtr.Zero, windowWidth, windowHeight);
            return renderManager;
        }

        /// <summary>
        /// Creates one active camera with the supplied viewport and layer mask.
        /// </summary>
        /// <param name="viewport">Viewport rectangle used by the hit resolver.</param>
        /// <param name="layerMask">Layer mask rendered by the camera.</param>
        /// <returns>Configured camera component.</returns>
        CameraComponent CreateCamera(float4 viewport, ushort layerMask) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = layerMask,
                CameraDrawOrder = 255,
                Viewport = viewport
            };
            cameraEntity.AddComponent(camera);
            return camera;
        }

        /// <summary>
        /// Creates one interactable entity with a visible sprite so render-order comparisons remain deterministic.
        /// </summary>
        /// <param name="position">Top-left entity position in window-space coordinates.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="renderOrder">2D render order assigned to the visible sprite.</param>
        /// <returns>Interactable component registered for hit resolution.</returns>
        InteractableComponent CreateInteractableEntity(float3 position, int2 size, byte renderOrder) {
            EditorEntity entity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                Position = position
            };

            SpriteComponent sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = size,
                RenderOrder2D = renderOrder
            };
            entity.AddComponent(sprite);

            InteractableComponent interactable = new InteractableComponent {
                Size = size
            };
            entity.AddComponent(interactable);
            return interactable;
        }
    }
}
