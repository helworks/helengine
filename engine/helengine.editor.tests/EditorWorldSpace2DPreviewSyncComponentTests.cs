using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the editor-owned world-space 2D preview synchronizer creates and removes internal preview proxies for supported scene entities.
    /// </summary>
    public sealed class EditorWorldSpace2DPreviewSyncComponentTests : IDisposable {
        EditorSessionInteractionServices InteractionServices => GeneratedAssetGraph.InteractionServices;
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Initializes the core services required by the preview-sync tests.
        /// </summary>
        public EditorWorldSpace2DPreviewSyncComponentTests() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            CoreValue = core;
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures the synchronizer creates one preview proxy when a supported 2D scene entity appears.
        /// </summary>
        [Fact]
        public void Update_WhenSupported2DSceneEntityAppears_CreatesPreviewProxy() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 32),
                Texture = CoreValue.RenderManager2D.PixelTexture
            });

            EditorEntity syncHostEntity = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.True(previewEntity.InternalEntity);
            Assert.Contains(previewEntity.Components, component => component is EditorSpriteWorldPreviewComponent);
        }

        /// <summary>Preview visibility follows disabled ancestors and reparenting, like the authored renderer.</summary>
        [Fact]
        public void Update_PreviewRespectsAncestorVisibility() {
            EditorEntity ancestor = new EditorEntity(CoreValue, InteractionServices) { Enabled = false };
            EditorEntity panel = new EditorEntity(CoreValue, InteractionServices);
            EditorEntity source = new EditorEntity(CoreValue, InteractionServices);
            ancestor.AddChild(panel);
            panel.AddChild(source);
            source.AddComponent(new SpriteComponent { Size = new int2(64, 32), Texture = CoreValue.RenderManager2D.PixelTexture });
            EditorEntity host = new EditorEntity(CoreValue, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent sync = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            host.AddComponent(sync);
            sync.Update();
            EditorEntity preview = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(source);
            Assert.NotNull(preview);
            Assert.False(preview.IsHierarchyEnabled);
            ancestor.Enabled = true;
            sync.Update();
            Assert.True(preview.IsHierarchyEnabled);
            panel.Enabled = false;
            sync.Update();
            Assert.False(preview.IsHierarchyEnabled);
            panel.RemoveChild(source);
            ancestor.AddChild(source);
            sync.Update();
            Assert.True(preview.IsHierarchyEnabled);
            source.Enabled = false;
            sync.Update();
            Assert.False(preview.IsHierarchyEnabled);
        }

        /// <summary>Preview proxies remain enabled but disappear when a source or its ancestor is hidden in the editor.</summary>
        [Fact]
        public void Update_PreviewRespectsEditorHiddenStateWithoutDisablingProxy() {
            EditorEntity ancestor = new EditorEntity(CoreValue, InteractionServices);
            EditorEntity source = new EditorEntity(CoreValue, InteractionServices);
            ancestor.AddChild(source);
            source.AddComponent(new SpriteComponent { Size = new int2(64, 32), Texture = CoreValue.RenderManager2D.PixelTexture });
            EditorEntity host = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent sync = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            host.AddComponent(sync);

            sync.Update();
            EditorEntity preview = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(source);
            Assert.NotNull(preview);
            Assert.False(preview.RenderSuppressed);

            ancestor.RenderSuppressed = true;
            sync.Update();
            Assert.True(preview.RenderSuppressed);
            Assert.True(preview.Enabled);

            ancestor.RenderSuppressed = false;
            source.RenderSuppressed = true;
            sync.Update();
            Assert.True(preview.RenderSuppressed);
            Assert.True(preview.Enabled);

            source.RenderSuppressed = false;
            sync.Update();
            Assert.False(preview.RenderSuppressed);
        }

        /// <summary>Hiding viewport-owned 2D content keeps its world-space proxy on the 3D preview path.</summary>
        [Fact]
        public void Update_HidingViewportOwnedSourceKeepsSuppressedWorldPreview() {
            EditorEntity viewportRoot = new EditorEntity(CoreValue, InteractionServices);
            viewportRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(640, 360)
            });
            EditorEntity source = new EditorEntity(CoreValue, InteractionServices);
            source.AddComponent(new SpriteComponent { Size = new int2(64, 32), Texture = CoreValue.RenderManager2D.PixelTexture });
            viewportRoot.AddChild(source);

            EditorEntity host = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent sync = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            host.AddComponent(sync);
            sync.Update();
            EditorEntity preview = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(source);

            Assert.NotNull(preview);
            Assert.False(preview.RenderSuppressed);

            source.RenderSuppressed = true;
            sync.Update();

            Assert.Same(preview, InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(source));
            Assert.True(preview.RenderSuppressed);
            Assert.True(preview.Enabled);
        }

        /// <summary>
        /// Ensures the synchronizer removes the preview proxy and clears the registry when the authored source entity disappears.
        /// </summary>
        [Fact]
        public void Update_WhenSourceEntityIsRemoved_RemovesPreviewProxy() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 32),
                Texture = CoreValue.RenderManager2D.PixelTexture
            });

            EditorEntity syncHostEntity = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHostEntity.AddComponent(syncComponent);
            syncComponent.Update();

            sourceEntity.Dispose();
            syncComponent.Update();

            Assert.Null(InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
        }

        /// <summary>
        /// Ensures the synchronizer creates one preview proxy when an authored text component appears.
        /// </summary>
        [Fact]
        public void Update_WhenSourceEntityUsesTextComponent_CreatesPreviewProxy() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new TextComponent {
                Size = new int2(80, 24),
                Text = "Preview"
            });

            EditorEntity syncHostEntity = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.True(previewEntity.InternalEntity);
            Assert.Contains(previewEntity.Components, component => component is EditorTextWorldPreviewComponent);
        }

        /// <summary>
        /// Ensures the synchronizer creates one preview proxy when an authored rounded-rectangle component appears.
        /// </summary>
        [Fact]
        public void Update_WhenSourceEntityUsesRoundedRectComponent_CreatesPreviewProxy() {
            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(64, 32)
            });

            EditorEntity syncHostEntity = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.True(previewEntity.InternalEntity);
            Assert.Contains(previewEntity.Components, component => component is EditorRoundedRectWorldPreviewComponent);
        }

        /// <summary>
        /// Ensures editor-internal UI descendants do not get mistaken for authored scene sprites and mirrored into the 3D world.
        /// </summary>
        [Fact]
        public void Update_WhenSpriteEntityBelongsToInternalEditorHierarchy_DoesNotCreatePreviewProxy() {
            EditorEntity internalRoot = new EditorEntity(CoreValue, InteractionServices) {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };

            EditorEntity internalChild = new EditorEntity(CoreValue, InteractionServices) {
                LayerMask = EditorLayerMasks.EditorUi
            };
            internalChild.AddComponent(new SpriteComponent {
                Size = new int2(24, 24),
                Texture = CoreValue.RenderManager2D.PixelTexture
            });
            internalRoot.AddChild(internalChild);

            EditorEntity syncHostEntity = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            Assert.Null(InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(internalChild));
        }

        /// <summary>
        /// Ensures the live synchronizer presents viewport-owned bottom-right anchored sprites at their authored reference-canvas position after layout updates settle.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteUsesBottomRightAnchor_SynchronizerPlacesPreviewAtBottomRight() {

            Entity viewportEntity = new Entity(CoreValue);
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

            Entity generatedRootEntity = new Entity(CoreValue);
            generatedRootEntity.InitComponents();
            generatedRootEntity.InitChildren();
            viewportEntity.AddChild(generatedRootEntity);

            Entity sourceEntity = new Entity(CoreValue);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            generatedRootEntity.AddChild(sourceEntity);

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(220, 220),
                Texture = CoreValue.RenderManager2D.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            LayoutComponent anchorComponent = new LayoutComponent();
            anchorComponent.SetAnchorDistances(right: 44f, bottom: 36f);
            sourceEntity.AddComponent(anchorComponent);

            EditorEntity syncHostEntity = new EditorEntity(GeneratedAssetGraph.ObjectManager.OwnerCore, InteractionServices);
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHostEntity.AddComponent(syncComponent);

            CoreValue.Update();
            syncComponent.Update();

            EditorEntity previewEntity = InteractionServices.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.Equal(new float3(1016f, -464f, 0f), previewEntity.Position);
            Assert.Equal(new float3(220f, 220f, 1f), previewEntity.Scale);
        }
    }
}
