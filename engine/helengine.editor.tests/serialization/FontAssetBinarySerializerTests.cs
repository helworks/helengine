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
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
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
                    ColorFormat = TextureAssetColorFormat.Rgba4444,
                    Colors = new byte[] { 0xFF, 0x0F }
                }
            };

            using MemoryStream stream = new MemoryStream();
            FilesFontAssetBinarySerializer.Serialize(stream, asset);
            stream.Position = 0;

            FontAsset roundTripped = FilesFontAssetBinarySerializer.Deserialize(stream);

            Assert.Equal(42ul, roundTripped.SourceTextureAsset.RuntimeAssetId);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, roundTripped.SourceTextureAsset.ColorFormat);
            Assert.Equal(new byte[] { 0xFF, 0x0F }, roundTripped.SourceTextureAsset.Colors);
        }

        /// <summary>
        /// Ensures packaged font atlases preserve indexed texture metadata through serialize and deserialize.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_whenFontAtlasIsIndexed8_preservesPaletteAndAlphaPrecision() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "version"));

            FontAsset asset = new FontAsset(
                new FontInfo("DemoDiscBody", 16, 4f),
                new TestRuntimeTexture {
                    Width = 2,
                    Height = 2
                },
                new Dictionary<char, FontChar>(),
                16f,
                2,
                2) {
                SourceTextureAsset = new TextureAsset {
                    Id = "fonts/demodiscbody.hefont#atlas",
                    RuntimeAssetId = 77ul,
                    Width = 2,
                    Height = 2,
                    ColorFormat = TextureAssetColorFormat.Indexed8,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8,
                    PaletteColors = new byte[] {
                        255, 255, 255, 0,
                        255, 255, 255, 255
                    },
                    Colors = new byte[] { 0, 1, 1, 0 }
                }
            };

            using MemoryStream stream = new MemoryStream();
            FilesFontAssetBinarySerializer.Serialize(stream, asset);
            stream.Position = 0;

            FontAsset roundTripped = FilesFontAssetBinarySerializer.Deserialize(stream);

            Assert.Equal(77ul, roundTripped.SourceTextureAsset.RuntimeAssetId);
            Assert.Equal(TextureAssetColorFormat.Indexed8, roundTripped.SourceTextureAsset.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.A8, roundTripped.SourceTextureAsset.AlphaPrecision);
            Assert.Equal(asset.SourceTextureAsset.PaletteColors, roundTripped.SourceTextureAsset.PaletteColors);
            Assert.Equal(asset.SourceTextureAsset.Colors, roundTripped.SourceTextureAsset.Colors);
        }

        /// <summary>
        /// Ensures packaged fonts can reference one external cooked atlas texture without embedding raw atlas bytes.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_whenFontUsesExternalCookedAtlasPath_preservesPathWithoutBuildingRawTexture() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            TestRenderManager2D renderManager2D = new TestRenderManager2D();
            core.Initialize(new TestRenderManager3D(), renderManager2D, new TestInputBackend(), new PlatformInfo("test", "version"));

            FontAsset asset = new FontAsset(
                new FontInfo("DemoDiscBody", 16, 4f),
                null,
                new Dictionary<char, FontChar>(),
                16f,
                128,
                64) {
                CookedAtlasTextureRelativePath = "cooked/fonts/body-atlas.ps2tex"
            };

            using MemoryStream stream = new MemoryStream();
            FilesFontAssetBinarySerializer.Serialize(stream, asset);
            stream.Position = 0;

            FontAsset roundTripped = FilesFontAssetBinarySerializer.Deserialize(stream);

            Assert.Equal("cooked/fonts/body-atlas.ps2tex", roundTripped.CookedAtlasTextureRelativePath);
            Assert.Null(roundTripped.SourceTextureAsset);
            Assert.Null(roundTripped.Texture);
            Assert.Equal(0, renderManager2D.BuildTextureFromRawCallCount);
        }
    }
}

