using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies packaged font binary serialization behavior.
    /// </summary>
    public sealed class FontAssetBinarySerializerTests : IDisposable {
        /// <summary>
        /// Temporary content root used while initializing the core render seam.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the temporary content root for the test fixture.
        /// </summary>
        public FontAssetBinarySerializerTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-font-serializer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Removes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures packaged font atlas runtime ids survive serialize/deserialize.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_whenFontAtlasIsCooked_preservesSourceTextureRuntimeAssetId() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "version"));

            FontAsset asset = new FontAsset(
                new FontInfo("DemoDiscBody", 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1) {
                SourceTextureAsset = new TextureAsset {
                    Id = "fonts/demodiscbody.hefont#atlas",
                    RuntimeAssetId = 42ul,
                    Width = 1,
                    Height = 1,
                    Colors = new byte[] { 255, 255, 255, 255 }
                }
            };

            using MemoryStream stream = new MemoryStream();
            FilesFontAssetBinarySerializer.Serialize(stream, asset);
            stream.Position = 0;

            FontAsset roundTripped = FilesFontAssetBinarySerializer.Deserialize(stream);

            Assert.Equal(42ul, roundTripped.SourceTextureAsset.RuntimeAssetId);
        }
    }
}
