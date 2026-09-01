namespace helengine.core.tests.content {
    /// <summary>
    /// Verifies that runtime font registration preserves the renderer required by deferred content loads.
    /// </summary>
    public sealed class RuntimeContentManagerConfigurationTests {
        /// <summary>
        /// Ensures a renderer supplied during configuration remains available to a processor invoked later by the content manager.
        /// </summary>
        [Fact]
        public void ConfigureSharedAssetContentManager_FontProcessorRetainsRendererAfterConfigurationReturns() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            using ContentManager contentManager = new ContentManager(new MemoryContentStreamSource(CreateMinimalFontPayload()));

            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager, renderer);

            FontAsset font = contentManager.Load<FontAsset>("ui/menu.hefont", RuntimeContentProcessorIds.FontAsset);

            Assert.NotNull(font);
            Assert.Same(renderer, renderer.LastBuildRenderer);
            Assert.NotNull(renderer.LastBuiltTexture);
            font.Dispose();
        }

        /// <summary>
        /// Ensures font registration creates a processor object whose renderer reference outlives configuration.
        /// </summary>
        [Fact]
        public void ConfigureSharedAssetContentManager_UsesOwningFontProcessor() {
            string sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "content",
                "RuntimeContentManagerConfiguration.cs"));
            string source = File.ReadAllText(sourcePath);

            Assert.Contains("new FontAssetBinaryContentProcessor(renderManager2D)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("stream => FontAssetBinarySerializer.Deserialize(stream, renderManager2D)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the dedicated font processor stores and uses its renderer as instance state rather than a deferred lambda capture.
        /// </summary>
        [Fact]
        public void FontAssetBinaryContentProcessor_StoresRendererForDeferredRead() {
            string sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "content",
                "FontAssetBinaryContentProcessor.cs"));
            string source = File.ReadAllText(sourcePath);

            Assert.Contains("readonly RenderManager2D RenderManager2DValue;", source, StringComparison.Ordinal);
            Assert.Contains("RenderManager2DValue = renderManager2D", source, StringComparison.Ordinal);
            Assert.Contains("FontAssetBinarySerializer.Deserialize(stream, RenderManager2DValue)", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates a minimal valid packaged font whose non-empty atlas requires the supplied renderer.
        /// </summary>
        /// <returns>Complete serialized font payload.</returns>
        static byte[] CreateMinimalFontPayload() {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                FontAssetBinarySerializer.CurrentVersion,
                FontAssetBinarySerializer.FormatId,
                (ushort)FontAssetBinarySerializer.RecordKind,
                FontAssetBinarySerializer.ValueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteString("menu-atlas");
                writer.WriteInt64(1);
                writer.WriteUInt16(1);
                writer.WriteUInt16(1);
                writer.WriteByte((byte)TextureAssetColorFormat.Rgba32);
                writer.WriteByte((byte)TextureAssetAlphaPrecision.Opaque);
                writer.WriteByteArray(null);
                writer.WriteByteArray(new byte[] { 255, 255, 255, 255 });
                writer.WriteString("Test");
                writer.WriteInt32(1);
                writer.WriteSingle(1f);
                writer.WriteSingle(1f);
                writer.WriteInt32(1);
                writer.WriteInt32(1);
                writer.WriteInt32(0);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Provides the packaged payload to a content manager without filesystem access.
        /// </summary>
        sealed class MemoryContentStreamSource : IContentStreamSource {
            /// <summary>
            /// Bytes returned for each requested asset path.
            /// </summary>
            readonly byte[] Payload;

            /// <summary>
            /// Initializes an in-memory content source with one packaged payload.
            /// </summary>
            /// <param name="payload">Bytes returned by subsequent reads.</param>
            public MemoryContentStreamSource(byte[] payload) {
                Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            }

            /// <summary>
            /// Opens a fresh read stream over the configured payload.
            /// </summary>
            /// <param name="assetPath">Requested asset path.</param>
            /// <returns>Readable stream positioned at the payload beginning.</returns>
            public Stream OpenRead(string assetPath) {
                return new MemoryStream(Payload, writable: false);
            }
        }

        /// <summary>
        /// Tracks whether the deferred font processor invokes the originally supplied renderer.
        /// </summary>
        sealed class RecordingRenderManager2D : RenderManager2D {
            /// <summary>
            /// Renderer observed while building the most recent raw texture.
            /// </summary>
            public RenderManager2D LastBuildRenderer { get; private set; }

            /// <summary>
            /// Texture returned for the most recent raw texture build.
            /// </summary>
            public RuntimeTexture LastBuiltTexture { get; private set; }

            /// <summary>
            /// Builds a managed texture while recording the renderer invocation.
            /// </summary>
            /// <param name="data">Raw texture data supplied by font deserialization.</param>
            /// <returns>Managed texture carrying the source dimensions.</returns>
            public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
                LastBuildRenderer = this;
                LastBuiltTexture = new ManagedRuntimeTexture {
                    Width = data.Width,
                    Height = data.Height
                };
                return LastBuiltTexture;
            }

            /// <summary>
            /// Leaves texture-region updates unused by this deserialization test.
            /// </summary>
            protected override void UpdateTextureRegionCore(
                RuntimeTexture texture,
                int x,
                int y,
                int width,
                int height,
                [NativeNoEscape] byte[] rgba8,
                int sourceRowPitch) {
            }

            /// <summary>
            /// Leaves sprite drawing unused by this deserialization test.
            /// </summary>
            public override void DrawSprite(ISpriteDrawable2D sprite) {
            }

            /// <summary>
            /// Leaves text drawing unused by this deserialization test.
            /// </summary>
            public override void DrawText(ITextDrawable2D text) {
            }

            /// <summary>
            /// Leaves rounded-rectangle drawing unused by this deserialization test.
            /// </summary>
            public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            }
        }
    }
}
