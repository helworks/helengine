namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies new generic format identifiers survive typed import settings and
    /// remain distinct from platform-owned or undefined format identifiers.
    /// </summary>
    public sealed class TextureAssetProcessorSettingsTests {
        /// <summary>Native P8 exposes the existing quantizer selector without being treated as a generic payload format.</summary>
        [Fact]
        public void NativeXboxPaletteUsesIndexingControls() {
            TextureAssetProcessorSettings settings = new TextureAssetProcessorSettings {
                ColorFormatId = "Xbox.Swizzled.I8_A8R8G8B8"
            };
            Assert.False(settings.UsesGenericColorFormat());
            Assert.True(settings.UsesIndexedColorFormat());
            Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed, settings.ResolveIndexingMethod());
            settings.IndexingMethodId = "unsupported";
            Assert.Throws<InvalidOperationException>(() => settings.ResolveIndexingMethod());
            settings.ColorFormatId = "Xbox.DXT1";
            Assert.False(settings.UsesIndexedColorFormat());
        }
        /// <summary>
        /// Round-trips the selected format and its alpha policy through the
        /// existing texture import settings serializer without schema changes.
        /// </summary>
        /// <param name="format">Generic format added to the processor.</param>
        /// <param name="alpha">Stored alpha policy appropriate to the format.</param>
        [Theory]
        [InlineData(TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Binary)]
        [InlineData(TextureAssetColorFormat.Ia4, TextureAssetAlphaPrecision.Binary)]
        [InlineData(TextureAssetColorFormat.Ia8, TextureAssetAlphaPrecision.A4)]
        [InlineData(TextureAssetColorFormat.Ia16, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.I4, TextureAssetAlphaPrecision.A4)]
        [InlineData(TextureAssetColorFormat.I8, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Opaque)]
        public void NewGenericFormatsRoundTripTypedSettings(TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha) {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Processor.Platforms.Add("n64", new TextureAssetProcessorSettings {
                ColorFormat = format,
                AlphaPrecision = alpha,
                MaxResolution = 256
            });
            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            TextureAssetProcessorSettings restored = TextureAssetImportSettingsBinarySerializer.Deserialize(stream).Processor.Platforms["n64"];

            Assert.True(restored.UsesGenericColorFormat());
            Assert.False(restored.UsesIndexedColorFormat());
            Assert.Equal(format, restored.ColorFormat);
            Assert.Equal(alpha, restored.AlphaPrecision);
            Assert.Equal(256, restored.MaxResolution);
        }

        /// <summary>
        /// Only declared generic names resolve; GX and numeric enum strings
        /// remain outside the generic processor's supported identifier contract.
        /// </summary>
        /// <param name="id">Identifier that must not resolve generically.</param>
        [Theory]
        [InlineData("GxRgb5A3")]
        [InlineData("unknown-format")]
        [InlineData("255")]
        public void UnsupportedIdentifiersRemainNonGeneric(string id) {
            Assert.False(new TextureAssetProcessorSettings { ColorFormatId = id }.UsesGenericColorFormat());
            Assert.False(TextureAssetProcessorSettings.TryResolveGenericColorFormat(id, out _));
        }
    }
}
