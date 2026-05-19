using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the editor-owned world-space 2D preview synchronizer creates and removes internal preview proxies for supported scene entities.
    /// </summary>
    public sealed class EditorWorldSpace2DPreviewSyncComponentTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the preview-sync tests.
        /// </summary>
        public EditorWorldSpace2DPreviewSyncComponentTests() {
            Core core = new Core();
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
        /// Ensures the synchronizer creates one preview proxy when a supported 2D scene entity appears.
        /// </summary>
        [Fact]
        public void Update_WhenSupported2DSceneEntityAppears_CreatesPreviewProxy() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 32),
                Texture = TextureUtils.PixelTexture
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.True(previewEntity.InternalEntity);
            Assert.Contains(previewEntity.Components, component => component is EditorSpriteWorldPreviewComponent);
        }

        /// <summary>
        /// Ensures the synchronizer removes the preview proxy and clears the registry when the authored source entity disappears.
        /// </summary>
        [Fact]
        public void Update_WhenSourceEntityIsRemoved_RemovesPreviewProxy() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 32),
                Texture = TextureUtils.PixelTexture
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);
            syncComponent.Update();

            sourceEntity.Dispose();
            syncComponent.Update();

            Assert.Null(EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
        }

        /// <summary>
        /// Ensures the synchronizer creates one preview proxy when an authored text component appears.
        /// </summary>
        [Fact]
        public void Update_WhenSourceEntityUsesTextComponent_CreatesPreviewProxy() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new TextComponent {
                Size = new int2(80, 24),
                Text = "Preview"
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.True(previewEntity.InternalEntity);
            Assert.Contains(previewEntity.Components, component => component is EditorTextWorldPreviewComponent);
        }

        /// <summary>
        /// Ensures the synchronizer creates one preview proxy when an authored rounded-rectangle component appears.
        /// </summary>
        [Fact]
        public void Update_WhenSourceEntityUsesRoundedRectComponent_CreatesPreviewProxy() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(64, 32)
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.True(previewEntity.InternalEntity);
            Assert.Contains(previewEntity.Components, component => component is EditorRoundedRectWorldPreviewComponent);
        }

        /// <summary>
        /// Ensures editor-internal UI descendants do not get mistaken for authored scene sprites and mirrored into the 3D world.
        /// </summary>
        [Fact]
        public void Update_WhenSpriteEntityBelongsToInternalEditorHierarchy_DoesNotCreatePreviewProxy() {
            EditorEntity internalRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };

            EditorEntity internalChild = new EditorEntity {
                LayerMask = EditorLayerMasks.EditorUi
            };
            internalChild.AddComponent(new SpriteComponent {
                Size = new int2(24, 24),
                Texture = TextureUtils.PixelTexture
            });
            internalRoot.AddChild(internalChild);

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            Assert.Null(EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(internalChild));
        }

        /// <summary>
        /// Ensures the live synchronizer presents viewport-owned bottom-right anchored sprites at their authored reference-canvas position after layout updates settle.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteUsesBottomRightAnchor_SynchronizerPlacesPreviewAtBottomRight() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager3D.AddWindow(IntPtr.Zero, 1600, 1200);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });
            viewportEntity.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            Entity generatedRootEntity = new Entity();
            generatedRootEntity.InitComponents();
            generatedRootEntity.InitChildren();
            viewportEntity.AddChild(generatedRootEntity);

            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            generatedRootEntity.AddChild(sourceEntity);

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(220, 220),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            AnchorComponent anchorComponent = new AnchorComponent();
            anchorComponent.SetAnchorDistances(right: 44f, bottom: 36f);
            sourceEntity.AddComponent(anchorComponent);

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            Core.Instance.Update();
            syncComponent.Update();

            EditorEntity previewEntity = EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.Equal(new float3(1016f, -464f, 0f), previewEntity.Position);
            Assert.Equal(new float3(220f, 220f, 1f), previewEntity.Scale);
        }
    }
}
