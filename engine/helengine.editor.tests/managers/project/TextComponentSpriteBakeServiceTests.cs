using helengine.directx11;
using helengine.vulkan;
using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies build-time text-to-sprite baking using the exact 2D preview capture path and one render-target readback seam.
    /// </summary>
    public sealed class TextComponentSpriteBakeServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by bake-service tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes the core services and temporary workspace required by bake-service tests.
        /// </summary>
        public TextComponentSpriteBakeServiceTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-text-sprite-bake-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(ProjectRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance and temporary workspace after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();

            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the bake service preserves the authored text box size when allocating the preview render target.
        /// </summary>
        [Fact]
        public void Bake_WhenRequested_UsesTheAuthoredTextBoxSizeForTheGeneratedTexture() {
            FakeRenderTargetTextureAssetReader reader = new FakeRenderTargetTextureAssetReader(CreateTextureAsset(128, 32));
            TextComponentSpriteBakeService service = CreateService(reader);

            TextComponentSpriteBakeResult result = service.Bake(CreateRequest("ds", new int2(128, 32)));

            Assert.NotNull(reader.LastRenderTarget);
            Assert.Equal(128, reader.LastRenderTarget.Width);
            Assert.Equal(32, reader.LastRenderTarget.Height);
            Assert.Equal((ushort)128, result.TextureAsset.Width);
            Assert.Equal((ushort)32, result.TextureAsset.Height);
        }

        /// <summary>
        /// Ensures bake requests use the shared generic processor settings instead of platform-specific texture defaults.
        /// </summary>
        [Fact]
        public void Bake_WhenRequested_UsesSharedGenericDefaultProcessorSettings() {
            TextComponentSpriteBakeService service = CreateService(new FakeRenderTargetTextureAssetReader(CreateTextureAsset(96, 24)));

            TextComponentSpriteBakeResult result = service.Bake(CreateRequest("custom-handheld", new int2(96, 24)));

            Assert.Equal(TextureAssetColorFormat.Rgba32.ToString(), result.ProcessorSettings.ColorFormatId);
            Assert.Equal(TextureAssetAlphaPrecision.A8, result.ProcessorSettings.AlphaPrecision);
            Assert.Equal(string.Empty, result.ProcessorSettings.IndexingMethodId);
            Assert.False(string.IsNullOrWhiteSpace(result.StableKey));
        }

        /// <summary>
        /// Ensures DS-targeted bake requests also use the shared generic processor settings after the DS-specific branch is removed.
        /// </summary>
        [Fact]
        public void Bake_WhenRequestedForNintendoDs_UsesSharedGenericDefaultProcessorSettings() {
            TextComponentSpriteBakeService service = CreateService(new FakeRenderTargetTextureAssetReader(CreateTextureAsset(96, 24)));

            TextComponentSpriteBakeResult result = service.Bake(CreateRequest("ds", new int2(96, 24)));

            Assert.Equal(TextureAssetColorFormat.Rgba32.ToString(), result.ProcessorSettings.ColorFormatId);
            Assert.Equal(TextureAssetAlphaPrecision.A8, result.ProcessorSettings.AlphaPrecision);
            Assert.Equal(string.Empty, result.ProcessorSettings.IndexingMethodId);
            Assert.False(string.IsNullOrWhiteSpace(result.StableKey));
        }

        /// <summary>
        /// Ensures bake requests reject the removed generated Nintendo DS debug-font reference.
        /// </summary>
        [Fact]
        public void Bake_WhenRequestedWithRemovedNintendoDsGeneratedFont_ThrowsUnsupportedGeneratedFontAssetId() {
            TextComponentSpriteBakeService service = CreateService(new FakeRenderTargetTextureAssetReader(CreateTextureAsset(96, 24)));
            TextComponentSpriteBakeRequest request = new TextComponentSpriteBakeRequest(
                0,
                "ds",
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                    SceneAssetReferenceSourceKind.Generated,
                    "generated/editor/fonts/ds-debug.hefont",
                    "editor",
                    "ds-debug-font"),
                "BACK",
                new int2(96, 24),
                new byte4(255, 255, 255, 255),
                false,
                1f,
                TextAlignment.Center,
                0f,
                12,
                1);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Bake(request));
            Assert.Contains("Unsupported generated font asset id", exception.Message);
        }

        /// <summary>
        /// Creates one real bake service backed by test render managers and one fake render-target reader.
        /// </summary>
        /// <param name="reader">Render-target reader that should receive the captured preview target.</param>
        /// <returns>Configured bake service.</returns>
        TextComponentSpriteBakeService CreateService(IRenderTargetTextureAssetReader reader) {
            string assetsRootPath = Path.Combine(ProjectRootPath, "assets");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath)));

            return new TextComponentSpriteBakeService(
                Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D),
                reader,
                assetsRootPath,
                contentManager,
                assetImportManager,
                CreateDefaultFontAsset());
        }

        /// <summary>
        /// Creates one bake request that uses the generated editor font and preserved authored text layout values.
        /// </summary>
        /// <param name="targetPlatformId">Target platform identifier for the bake request.</param>
        /// <param name="size">Authored text box size that should be preserved by the bake.</param>
        /// <returns>Configured bake request.</returns>
        TextComponentSpriteBakeRequest CreateRequest(string targetPlatformId, int2 size) {
            return new TextComponentSpriteBakeRequest(
                0,
                targetPlatformId,
                CreateEditorFontReference(),
                "BACK",
                size,
                new byte4(255, 255, 255, 255),
                false,
                1f,
                TextAlignment.Center,
                0f,
                12,
                1);
        }

        /// <summary>
        /// Creates the generated editor-font reference used by authored text scene payloads.
        /// </summary>
        /// <returns>Generated editor-font reference.</returns>
        static SceneAssetReference CreateEditorFontReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEditorUiFont();
        }

        /// <summary>
        /// Creates one minimal default font asset used to resolve generated editor-font references during tests.
        /// </summary>
        /// <returns>Minimal default editor font asset.</returns>
        static FontAsset CreateDefaultFontAsset() {
            return new FontAsset(
                new FontInfo("Editor Default", 16, 8f),
                null,
                new Dictionary<char, FontChar>(),
                16f,
                64,
                64) {
                    SourceTextureAsset = CreateTextureAsset(64, 64)
                };
        }

        /// <summary>
        /// Creates one raw texture asset with the supplied dimensions.
        /// </summary>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <returns>Texture asset with the requested dimensions.</returns>
        static TextureAsset CreateTextureAsset(int width, int height) {
            return new TextureAsset {
                Id = string.Concat("generated:text:", width, "x", height),
                Width = (ushort)width,
                Height = (ushort)height,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[width * height * 4]
            };
        }

        /// <summary>
        /// Fake render-target reader that returns one deterministic texture asset and records the captured render target.
        /// </summary>
        sealed class FakeRenderTargetTextureAssetReader : IRenderTargetTextureAssetReader {
            /// <summary>
            /// Texture asset that should be returned for each read request.
            /// </summary>
            readonly TextureAsset TextureAsset;

            /// <summary>
            /// Initializes one fake reader with a predetermined texture asset result.
            /// </summary>
            /// <param name="textureAsset">Texture asset that the fake reader should return.</param>
            public FakeRenderTargetTextureAssetReader(TextureAsset textureAsset) {
                TextureAsset = textureAsset ?? throw new ArgumentNullException(nameof(textureAsset));
            }

            /// <summary>
            /// Gets the last render target received by the fake reader.
            /// </summary>
            public RenderTarget LastRenderTarget { get; private set; }

            /// <summary>
            /// Gets the last asset id requested by the bake service.
            /// </summary>
            public string LastAssetId { get; private set; }

            /// <summary>
            /// Records the supplied render target and returns the configured texture asset.
            /// </summary>
            /// <param name="renderTarget">Captured preview render target.</param>
            /// <param name="assetId">Generated asset id requested by the bake service.</param>
            /// <returns>Configured texture asset.</returns>
            public TextureAsset ReadTextureAsset(RenderTarget renderTarget, string assetId) {
                LastRenderTarget = renderTarget;
                LastAssetId = assetId;
                TextureAsset.Id = assetId;
                return TextureAsset;
            }
        }
    }
}
