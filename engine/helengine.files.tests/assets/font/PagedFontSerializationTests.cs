using helengine;
namespace helengine.files.tests.assets.font {
    /// <summary>Protects legacy font bytes and optional page metadata across editor/runtime readers.</summary>
    public sealed class PagedFontSerializationTests {
        /// <summary>Ordinary fonts retain version five; paged fonts round-trip their external paths and glyph indices without opening pages.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RoundTrip_PreservesOptionalPagesAndLegacyVersion(bool paged) {
            FontChar glyph = new FontChar(new float4(0, 0, 1, 1), 0, 20, 0, 0) { PageIndex = paged ? 1 : 0 };
            using FontAsset font = new FontAsset(new FontInfo("test", 24, 6), null,
                new Dictionary<char, FontChar> { ['?'] = glyph }, 24, 1, 1) {
                SourceTextureAsset = new TextureAsset { Id = "base", Width = 1, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 255 } },
                Pages = paged ? new[] { new FontPage("missing-until-requested.hasset", 512, 256) } : null
            };
            using MemoryStream stream = new MemoryStream();
            helengine.files.FontAssetBinarySerializer.Serialize(stream, font);
            stream.Position = 0;
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            Assert.Equal((byte)(paged ? 6 : 5), header.Version);
            stream.Position = 0;
            using FontAsset editor = helengine.files.FontAssetBinarySerializer.Deserialize(stream);
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(glyph.PageIndex, runtime.Characters['?'].PageIndex);
            Assert.Equal(editor.MeasureString("??"), runtime.MeasureString("??"));
            if (paged) {
                Assert.Single(runtime.Pages); Assert.Equal(512, runtime.Pages[0].Width);
                Assert.Equal("missing-until-requested.hasset", runtime.Pages[0].TexturePath);
            } else { Assert.Null(runtime.Pages); }
        }
    }
}
