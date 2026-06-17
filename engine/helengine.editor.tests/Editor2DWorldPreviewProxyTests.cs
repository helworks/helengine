using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-owned 2D world-preview proxy mappings between authored scene entities and internal preview entities.
    /// </summary>
    public sealed class Editor2DWorldPreviewProxyTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by preview-proxy registry tests.
        /// </summary>
        public Editor2DWorldPreviewProxyTests() {
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
        /// Ensures one registered preview proxy resolves back to the authored source entity.
        /// </summary>
        [Fact]
        public void RegisterPreviewProxy_WhenSourceEntityIsMapped_ResolvesPreviewBackToSourceEntity() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };

            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            Assert.Same(sourceEntity, EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity));
            Assert.Same(previewEntity, EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
        }

        /// <summary>
        /// Ensures removing one source entity clears both the source-to-preview and preview-to-source mappings.
        /// </summary>
        [Fact]
        public void RemovePreviewProxy_WhenSourceEntityIsRemoved_ClearsBothDirections() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            EditorEntity previewEntity = new EditorEntity {
                InternalEntity = true
            };
            EditorWorldSpace2DPreviewRegistry.Register(sourceEntity, previewEntity);

            EditorWorldSpace2DPreviewRegistry.RemoveBySourceEntity(sourceEntity);

            Assert.Null(EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
            Assert.Null(EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity));
        }

        /// <summary>
        /// Ensures one sprite preview proxy mirrors the authored source transform after synchronization.
        /// </summary>
        [Fact]
        public void Update_WhenSpriteSourceMoves_PreviewProxyMirrorsWorldTransform() {
            Entity sourceEntity = new Entity {
                LocalPosition = new float3(42f, 24f, 12f),
                LocalScale = new float3(1f, 1f, 1f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(128, 64),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            previewComponent.SynchronizeFromSource();

            Assert.True(previewEntity.InternalEntity);
            Assert.Equal(sourceEntity.Position, previewEntity.Position);
            Assert.Equal(sourceEntity.Orientation, previewEntity.Orientation);
            Assert.Equal(new float3(128f, 64f, 1f), previewEntity.Scale);
        }

        /// <summary>
        /// Ensures one preview proxy remains editor-internal and never resolves as a selectable authored scene entity.
        /// </summary>
        [Fact]
        public void Update_WhenPreviewProxyExists_ProxyEntityIsInternalAndNonSelectable() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(32, 32),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            previewComponent.SynchronizeFromSource();

            Assert.True(previewEntity.InternalEntity);
            Assert.False(EditorViewportSceneSelectionFilter.ShouldSelectEntity(previewEntity));
        }

        /// <summary>
        /// Ensures sprite world-preview proxies build one dedicated textured preview material instead of the normal lit mesh material path.
        /// </summary>
        [Fact]
        public void Update_WhenSpritePreviewIsCreated_BuildsDedicatedTexturedPreviewMaterial() {
            TestRenderManager3D renderManager3D = (TestRenderManager3D)Core.Instance.RenderManager3D;
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(32, 16),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            ShaderMaterialAsset builtMaterial = Assert.Single(renderManager3D.BuiltMaterialAssets);
            Assert.Equal("EditorWorldSpaceSpritePreview.ps", builtMaterial.PixelProgram);
            Assert.Equal(MaterialBlendMode.AlphaBlend, builtMaterial.RenderState.BlendMode);
        }

        /// <summary>
        /// Ensures sprite world-preview proxies build a corner-origin XY-plane mesh so sprite content expands into positive X/Y from the authored entity origin.
        /// </summary>
        [Fact]
        public void Update_WhenSpritePreviewIsCreated_BuildsCornerOriginXyPlaneMesh() {
            TestRenderManager3D renderManager3D = (TestRenderManager3D)Core.Instance.RenderManager3D;
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(32, 16),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            ModelAsset builtModelAsset = Assert.Single(renderManager3D.BuiltModelAssets);
            Assert.Equal(new float3(0f, 0f, 0f), builtModelAsset.Positions[0]);
            Assert.Equal(new float3(1f, 0f, 0f), builtModelAsset.Positions[1]);
            Assert.Equal(new float3(1f, 1f, 0f), builtModelAsset.Positions[2]);
            Assert.Equal(new float3(0f, 1f, 0f), builtModelAsset.Positions[3]);
        }

        /// <summary>
        /// Ensures exact text preview proxies use the same positive-XY plane contract and source orientation as sprite previews.
        /// </summary>
        [Fact]
        public void Update_WhenTextPreviewSynchronizes_UsesPositiveXyPlaneAndSourceOrientation() {
            Entity sourceEntity = new Entity {
                LocalPosition = new float3(12f, 24f, 36f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            TextComponent sourceComponent = new TextComponent {
                Size = new int2(90, 30),
                Text = "Exact Preview"
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorTextWorldPreviewComponent previewComponent = new EditorTextWorldPreviewComponent(sourceEntity, sourceComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, sourceComponent));
            previewEntity.AddComponent(previewComponent);

            previewComponent.SynchronizeFromSource();

            Assert.Equal(sourceEntity.Position, previewEntity.Position);
            Assert.Equal(sourceEntity.Orientation, previewEntity.Orientation);
            Assert.Equal(new float3(90f, 30f, 1f), previewEntity.Scale);
        }

        /// <summary>
        /// Ensures viewport-owned sprite previews reverse live reference-canvas fit scaling and present in right/down viewport space inside the 3D scene view.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteUsesReferenceCanvasFit_ReversesFitScaleAndPresentsNegativeYWorldSpace() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager3D.AddWindow(IntPtr.Zero, 1600, 1200);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });
            viewportEntity.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            Entity sourceEntity = new Entity {
                LocalPosition = new float3(100f, 200f, 0f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(220, 110),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            Core.Instance.Update();

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            previewComponent.SynchronizeFromSource();

            Assert.Equal(new float3(100f, -200f, 0f), previewEntity.Position);
            Assert.Equal(new float3(220f, 110f, 1f), previewEntity.Scale);
        }

        /// <summary>
        /// Ensures viewport-owned sprite previews use one downward plane whose top edge samples the top of the authored sprite texture.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpritePreviewIsCreated_BuildsViewportPlaneWithRegularTextureUvs() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(32, 16),
                Texture = TextureUtils.PixelTexture
            };
            sourceEntity.AddComponent(spriteComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            ModelAsset builtModelAsset = Assert.Single(renderManager3D.BuiltModelAssets);
            Assert.Equal(new float3(0f, 0f, 0f), builtModelAsset.Positions[0]);
            Assert.Equal(new float3(1f, 0f, 0f), builtModelAsset.Positions[1]);
            Assert.Equal(new float3(1f, -1f, 0f), builtModelAsset.Positions[2]);
            Assert.Equal(new float3(0f, -1f, 0f), builtModelAsset.Positions[3]);
            Assert.Equal(new float2(0f, 0f), builtModelAsset.TexCoords[0]);
            Assert.Equal(new float2(1f, 0f), builtModelAsset.TexCoords[1]);
            Assert.Equal(new float2(1f, 1f), builtModelAsset.TexCoords[2]);
            Assert.Equal(new float2(0f, 1f), builtModelAsset.TexCoords[3]);
        }

        /// <summary>
        /// Ensures viewport-owned exact previews use one downward plane whose top edge samples the top of the render-target capture.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedTextPreviewIsCreated_BuildsViewportPlaneWithRenderTargetUvs() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(96, 24),
                Text = "Viewport Text"
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorTextWorldPreviewComponent previewComponent = new EditorTextWorldPreviewComponent(sourceEntity, sourceComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, sourceComponent));
            previewEntity.AddComponent(previewComponent);

            ModelAsset builtModelAsset = Assert.Single(renderManager3D.BuiltModelAssets);
            Assert.Equal(new float3(0f, 0f, 0f), builtModelAsset.Positions[0]);
            Assert.Equal(new float3(1f, 0f, 0f), builtModelAsset.Positions[1]);
            Assert.Equal(new float3(1f, -1f, 0f), builtModelAsset.Positions[2]);
            Assert.Equal(new float3(0f, -1f, 0f), builtModelAsset.Positions[3]);
            Assert.Equal(new float2(0f, 1f), builtModelAsset.TexCoords[0]);
            Assert.Equal(new float2(1f, 1f), builtModelAsset.TexCoords[1]);
            Assert.Equal(new float2(1f, 0f), builtModelAsset.TexCoords[2]);
            Assert.Equal(new float2(0f, 0f), builtModelAsset.TexCoords[3]);
        }

        /// <summary>
        /// Ensures viewport-owned anchored sprites are presented at authored bottom-right coordinates after live fit scaling and anchor refresh have been reversed.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteUsesBottomRightAnchor_PresentsAuthoredBottomRightPosition() {
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

            LayoutComponent anchorComponent = new LayoutComponent();
            anchorComponent.SetAnchorDistances(right: 44f, bottom: 36f);
            sourceEntity.AddComponent(anchorComponent);

            Core.Instance.Update();

            EditorEntity previewEntity = new EditorEntity();
            EditorSpriteWorldPreviewComponent previewComponent = new EditorSpriteWorldPreviewComponent(sourceEntity, spriteComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, spriteComponent));
            previewEntity.AddComponent(previewComponent);

            previewComponent.SynchronizeFromSource();

            Assert.Equal(new float3(1016f, -464f, 0f), previewEntity.Position);
            Assert.Equal(new float3(220f, 220f, 1f), previewEntity.Scale);
        }

        /// <summary>
        /// Ensures transform-only text changes do not trigger another exact preview texture recapture.
        /// </summary>
        [Fact]
        public void Update_WhenOnlyTextTransformChanges_DoesNotRebuildPreviewTexture() {
            Entity sourceEntity = new Entity {
                LocalPosition = new float3(10f, 20f, 30f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Exact Preview"
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorTextWorldPreviewComponent previewComponent = new EditorTextWorldPreviewComponent(sourceEntity, sourceComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, sourceComponent));
            previewEntity.AddComponent(previewComponent);

            int initialCaptureCount = previewComponent.CaptureCount;
            sourceEntity.LocalPosition = new float3(300f, 400f, 500f);
            previewComponent.SynchronizeFromSource();

            Assert.Equal(initialCaptureCount, previewComponent.CaptureCount);
        }

        /// <summary>
        /// Ensures visible rounded-rectangle state changes trigger one exact preview texture recapture.
        /// </summary>
        [Fact]
        public void Update_WhenRoundedRectVisibleDataChanges_RebuildsPreviewTexture() {
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            RoundedRectComponent sourceComponent = new RoundedRectComponent {
                Size = new int2(64, 32),
                FillColor = new byte4(10, 20, 30, 255)
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorEntity previewEntity = new EditorEntity();
            EditorRoundedRectWorldPreviewComponent previewComponent = new EditorRoundedRectWorldPreviewComponent(sourceEntity, sourceComponent);
            previewEntity.AddComponent(new Editor2DPreviewSourceTagComponent(sourceEntity, sourceComponent));
            previewEntity.AddComponent(previewComponent);

            int initialCaptureCount = previewComponent.CaptureCount;
            sourceComponent.FillColor = new byte4(200, 10, 10, 255);
            previewComponent.SynchronizeFromSource();

            Assert.True(previewComponent.CaptureCount > initialCaptureCount);
        }
    }
}
