using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor-only exact 2D preview capture service allocates and owns render-target-backed preview resources correctly.
    /// </summary>
    public sealed class EditorExact2DPreviewCaptureServiceTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by exact 2D preview capture tests.
        /// </summary>
        public EditorExact2DPreviewCaptureServiceTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures capturing a text preview allocates or resizes the render target to the requested size.
        /// </summary>
        [Fact]
        public void CaptureTextPreview_WhenRequested_CreatesOrResizesRenderTargetToRequestedSize() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview"
            };
            sourceEntity.AddComponent(sourceComponent);

            using EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D);
            service.CaptureTextPreview(sourceEntity, sourceComponent, new int2(256, 128));

            Assert.NotNull(service.PreviewRenderTarget);
            Assert.Equal(256, service.PreviewRenderTarget.Width);
            Assert.Equal(128, service.PreviewRenderTarget.Height);
        }

        /// <summary>
        /// Ensures capturing a rounded-rectangle preview binds the preview render target to the returned runtime material.
        /// </summary>
        [Fact]
        public void CaptureRoundedRectPreview_WhenRequested_BindsPreviewTextureOnReturnedMaterial() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            RoundedRectComponent sourceComponent = new RoundedRectComponent {
                Size = new int2(64, 32)
            };
            sourceEntity.AddComponent(sourceComponent);

            using EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D);
            RuntimeMaterial material = service.CaptureRoundedRectPreview(sourceEntity, sourceComponent, new int2(128, 64));

            int bindingIndex = material.Layout.FindTextureBindingIndex("PreviewTexture");
            Assert.True(bindingIndex >= 0);
            Assert.Same(service.PreviewRenderTarget, material.Properties.GetTexture(bindingIndex));
        }

        /// <summary>
        /// Ensures disposing the capture service releases its owned render-target resources.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalled_ReleasesOwnedRenderTargetResources() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Size = new int2(120, 32),
                Text = "Preview"
            };
            sourceEntity.AddComponent(sourceComponent);

            EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderManager3D);
            service.CaptureTextPreview(sourceEntity, sourceComponent, new int2(256, 128));
            TestRenderTarget previewRenderTarget = Assert.IsType<TestRenderTarget>(service.PreviewRenderTarget);

            service.Dispose();

            Assert.True(previewRenderTarget.WasDisposed);
        }
    }
}
