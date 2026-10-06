using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor-only exact 2D preview capture service allocates and owns render-target-backed preview resources correctly.
    /// </summary>
    public sealed class EditorExact2DPreviewCaptureServiceTests : IDisposable {
        /// <summary>Scaled menu title glyphs and their outline exceed a 28-pixel layout box and must survive capture and world framing.</summary>
        [Fact]
        public void CaptureScaledTitle_UsesFullGlyphBoundsAndPreservesAuthoredLayout() {
            RuntimeTexture atlas = new TestRuntimeTexture { Width = 64, Height = 64 };
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            foreach (char character in "HELENGINE") {
                characters[character] = new FontChar(new float4(0, 0, 10f / 64, 24f / 64), 8, 12, 0, 0);
            }
            FontAsset font = new FontAsset(new FontInfo("Title", 32, 8), atlas, characters, 32, 64, 64);
            EditorEntity viewport = new EditorEntity(Core.Instance, GeneratedAssetGraph.InteractionServices);
            viewport.AddComponent(new ViewportComponent { BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(1280, 720) });
            EditorEntity source = new EditorEntity(Core.Instance, GeneratedAssetGraph.InteractionServices) { LocalPosition = new float3(100, 100, 0) };
            viewport.AddChild(source);
            TextComponent text = new TextComponent { Size = new int2(280, 28), Text = "HELENGINE", Font = font, FontScale = 2, OutlineScale = 2 };
            source.AddComponent(text);
            float4 bounds = EditorTextPreviewBoundsService.ResolveBounds(text);
            Assert.Equal(new float4(-2, 0, 282, 66), bounds);
            EditorEntity proxy = new EditorEntity(Core.Instance, GeneratedAssetGraph.InteractionServices);
            EditorTextWorldPreviewComponent preview = new EditorTextWorldPreviewComponent(source, text, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            proxy.AddComponent(preview);
            Assert.Equal(new float3(98, -100, 0), proxy.Position);
            Assert.Equal(new float3(282, 66, 1), proxy.Scale);
            using EditorExact2DPreviewCaptureService capture = new EditorExact2DPreviewCaptureService(Core.Instance.RenderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            capture.CaptureTextPreview(source, text, new int2((int)bounds.Z, (int)bounds.W));
            Assert.Equal(66, capture.PreviewRenderTarget.Height);
            Assert.Equal(new float3(2, 0, 0), capture.PreviewTextComponent.Parent.LocalPosition);
            Assert.Equal(new int2(280, 28), capture.PreviewTextComponent.Size);
            Assert.Equal(2f, capture.PreviewTextComponent.FontScale);
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent { Viewport = new float4(0, 0, 1280, 720) };
            EditorEntity cameraEntity = new EditorEntity(Core.Instance, GeneratedAssetGraph.InteractionServices);
            cameraEntity.AddComponent(camera);
            EditorViewportCameraController controller = new EditorViewportCameraController(camera, Core.Instance.Input);
            cameraEntity.AddComponent(controller);
            new EditorViewportSelectionFramingService().FocusSelection(camera, controller, source);
            Assert.Equal(new float3(239, -133, 0), controller.GetOrbitTarget());
        }

        /// <summary>Multiline glyphs, negative shadow offsets, and outlines retain both ends of the texture without changing layout or wrapping.</summary>
        [Fact]
        public void CaptureMultilineText_WithNegativeShadow_ExtendsCaptureOriginAndHeight() {
            FontAsset font = new FontAsset(new FontInfo("Title", 32, 8), new TestRuntimeTexture { Width = 64, Height = 64 },
                new Dictionary<char, FontChar> { ['H'] = new FontChar(new float4(0, 0, 10f / 64, 24f / 64), 8, 12, 0, 0) }, 32, 64, 64);
            EditorEntity source = new EditorEntity(Core.Instance, GeneratedAssetGraph.InteractionServices);
            TextComponent text = new TextComponent {
                Size = new int2(280, 28), Text = "H\nH", Font = font, FontScale = 2,
                OutlineScale = 2, ShadowOffset = new float2(-5, -20)
            };
            source.AddComponent(text);
            float4 bounds = EditorTextPreviewBoundsService.ResolveBounds(text);
            Assert.Equal(new float4(-5, -4, 285, 134), bounds);
            using EditorExact2DPreviewCaptureService capture = new EditorExact2DPreviewCaptureService(Core.Instance.RenderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            capture.CaptureTextPreview(source, text, new int2((int)bounds.Z, (int)bounds.W));
            Assert.Equal(new float3(5, 4, 0), capture.PreviewTextComponent.Parent.LocalPosition);
            Assert.Equal(134, capture.PreviewRenderTarget.Height);
            Assert.Equal(new int2(280, 28), capture.PreviewTextComponent.Size);
        }

        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Initializes the core services required by exact 2D preview capture tests.
        /// </summary>
        public EditorExact2DPreviewCaptureServiceTests() {
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new helengine.directx11.DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new helengine.vulkan.VulkanShaderBackend());

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures capturing a text preview allocates or resizes the render target to the requested size.
        /// </summary>
        [Fact]
        public void CaptureTextPreview_WhenRequested_CreatesOrResizesRenderTargetToRequestedSize() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity(Core.Instance);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview"
            };
            sourceEntity.AddComponent(sourceComponent);

            using EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            service.CaptureTextPreview(sourceEntity, sourceComponent, new int2(256, 128));

            Assert.NotNull(service.PreviewRenderTarget);
            Assert.Equal(256, service.PreviewRenderTarget.Width);
            Assert.Equal(128, service.PreviewRenderTarget.Height);
        }

        /// <summary>
        /// Ensures text effect values are copied into the hidden component used by exact preview capture.
        /// </summary>
        [Fact]
        public void CaptureTextPreview_WhenTextEffectsAreConfigured_CopiesEffectsToPreviewComponent() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity(Core.Instance);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview",
                OutlineScale = 2f,
                OutlineColor = new byte4(1, 2, 3, 255),
                ShadowOffset = new float2(4f, 5f),
                ShadowColor = new byte4(6, 7, 8, 200)
            };
            sourceEntity.AddComponent(sourceComponent);

            using EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            service.CaptureTextPreview(sourceEntity, sourceComponent, new int2(256, 128));

            Assert.Equal(sourceComponent.OutlineScale, service.PreviewTextComponent.OutlineScale);
            Assert.Equal(sourceComponent.OutlineColor, service.PreviewTextComponent.OutlineColor);
            Assert.Equal(sourceComponent.ShadowOffset, service.PreviewTextComponent.ShadowOffset);
            Assert.Equal(sourceComponent.ShadowColor, service.PreviewTextComponent.ShadowColor);
        }

        /// <summary>
        /// Ensures capturing a rounded-rectangle preview binds the preview render target to the returned runtime material.
        /// </summary>
        [Fact]
        public void CaptureRoundedRectPreview_WhenRequested_BindsPreviewTextureOnReturnedMaterial() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity(Core.Instance);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            RoundedRectComponent sourceComponent = new RoundedRectComponent {
                Size = new int2(64, 32)
            };
            sourceEntity.AddComponent(sourceComponent);

            using EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            ShaderRuntimeMaterial material = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(service.CaptureRoundedRectPreview(sourceEntity, sourceComponent, new int2(128, 64)));

            int bindingIndex = material.Layout.FindTextureBindingIndex("PreviewTexture");
            Assert.True(bindingIndex >= 0);
            Assert.Same(service.PreviewRenderTarget, material.Properties.GetTexture(bindingIndex));
        }

        /// <summary>
        /// Ensures each exact preview camera receives only the private 2D clone owned by its capture service.
        /// </summary>
        [Fact]
        public void CaptureServices_WhenSecondCameraIsCreatedAfterFirstCapture_KeepDrawablesOnTheirOwnQueues() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity textSourceEntity = new Entity(Core.Instance);
            textSourceEntity.InitComponents();
            textSourceEntity.InitChildren();
            TextComponent textSource = new TextComponent {
                Text = "Text preview",
                Size = new int2(100, 28)
            };
            textSourceEntity.AddComponent(textSource);

            Entity roundedRectSourceEntity = new Entity(Core.Instance);
            roundedRectSourceEntity.InitComponents();
            roundedRectSourceEntity.InitChildren();
            RoundedRectComponent roundedRectSource = new RoundedRectComponent {
                Size = new int2(80, 40)
            };
            roundedRectSourceEntity.AddComponent(roundedRectSource);

            using EditorExact2DPreviewCaptureService textService = new EditorExact2DPreviewCaptureService(renderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            textService.CaptureTextPreview(textSourceEntity, textSource, new int2(160, 48));

            using EditorExact2DPreviewCaptureService roundedRectService = new EditorExact2DPreviewCaptureService(renderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            roundedRectService.CaptureRoundedRectPreview(roundedRectSourceEntity, roundedRectSource, new int2(128, 64));

            RenderList2D textQueue = Assert.IsType<RenderList2D>(textService.PreviewCamera.RenderQueue2D);
            RenderList2D roundedRectQueue = Assert.IsType<RenderList2D>(roundedRectService.PreviewCamera.RenderQueue2D);
            Assert.Equal(1, textQueue.Count);
            Assert.Same(textService.PreviewTextComponent, textQueue[0]);
            Assert.Equal(1, roundedRectQueue.Count);
            Assert.Same(roundedRectService.PreviewRoundedRectComponent, roundedRectQueue[0]);

            textService.CaptureTextPreview(textSourceEntity, textSource, new int2(320, 96));
            roundedRectService.CaptureRoundedRectPreview(roundedRectSourceEntity, roundedRectSource, new int2(256, 128));
            Assert.Equal(1, textQueue.Count);
            Assert.Same(textService.PreviewTextComponent, textQueue[0]);
            Assert.Equal(1, roundedRectQueue.Count);
            Assert.Same(roundedRectService.PreviewRoundedRectComponent, roundedRectQueue[0]);

            textService.CaptureRoundedRectPreview(roundedRectSourceEntity, roundedRectSource, new int2(256, 128));
            Assert.Null(textService.PreviewTextComponent);
            Assert.NotNull(textService.PreviewRoundedRectComponent);
            Assert.Equal(1, textQueue.Count);
            Assert.Same(textService.PreviewRoundedRectComponent, textQueue[0]);
            Assert.Equal(1, roundedRectQueue.Count);
            Assert.Same(roundedRectService.PreviewRoundedRectComponent, roundedRectQueue[0]);

            roundedRectSourceEntity.Enabled = false;
            roundedRectService.CaptureRoundedRectPreview(roundedRectSourceEntity, roundedRectSource, new int2(256, 128));
            Assert.False(roundedRectService.PreviewRoundedRectComponent.Parent.Enabled);
            Assert.Equal(0, roundedRectQueue.Count);

            roundedRectSourceEntity.Enabled = true;
            roundedRectService.CaptureRoundedRectPreview(roundedRectSourceEntity, roundedRectSource, new int2(256, 128));
            Assert.True(roundedRectService.PreviewRoundedRectComponent.Parent.Enabled);
            Assert.Equal(1, roundedRectQueue.Count);
            Assert.Same(roundedRectService.PreviewRoundedRectComponent, roundedRectQueue[0]);
        }

        /// <summary>
        /// Ensures disposing the capture service releases its owned render-target resources.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalled_ReleasesOwnedRenderTargetResources() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity(Core.Instance);
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview"
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D, Core.Instance.RenderManager2D, Core.Instance.ObjectManager, GeneratedAssetGraph.ShaderLibrary);
            service.CaptureTextPreview(sourceEntity, sourceComponent, new int2(256, 128));
            TestRenderTarget previewRenderTarget = Assert.IsType<TestRenderTarget>(service.PreviewRenderTarget);

            service.Dispose();

            Assert.True(previewRenderTarget.WasDisposed);
        }
    }
}
