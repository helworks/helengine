namespace helengine.editor.tests.managers.asset {
    /// <summary>Checks native GX palette controls and tile-based memory estimates in platform settings.</summary>
    public sealed class GxTextureSettingsTests {
        /// <summary>Each hardware indexed format exposes all three native TLUT encodings and the selected quantizer.</summary>
        /// <param name="format">Indexed GX encoding.</param>
        [Theory]
        [InlineData("CI4")]
        [InlineData("CI8")]
        [InlineData("CI14X2")]
        public void IndexedFormatsExposeEachTlutAndQuantization(string format) {
            foreach (string tlut in new[] { "IA8", "RGB565", "RGB5A3" }) {
                TextureAssetProcessorSettings settings = new() { ColorFormatId = "Gx." + format + ".TLUT." + tlut };
                Assert.True(settings.UsesIndexedColorFormat());
                Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed, settings.ResolveIndexingMethod());
                settings.IndexingMethodId = "invalid";
                Assert.Throws<InvalidOperationException>(() => settings.ResolveIndexingMethod());
            }
        }

        /// <summary>Tile estimates include padded edge blocks while excluding the GXN1 CPU header.</summary>
        /// <param name="id">GX storage identifier or the legacy RGB5A3 identifier.</param>
        /// <param name="expected">Bytes required for a three-by-five image.</param>
        [Theory]
        [InlineData("Gx.I4", 32)]
        [InlineData("Gx.I8", 64)]
        [InlineData("Gx.IA4", 64)]
        [InlineData("Gx.IA8", 64)]
        [InlineData("Gx.RGB565", 64)]
        [InlineData("Gx.RGB5A3", 64)]
        [InlineData("Gx.RGBA8", 128)]
        [InlineData("Gx.CMPR", 32)]
        [InlineData("Gx.Z8", 64)]
        [InlineData("Gx.Z16", 64)]
        [InlineData("Gx.Z24X8", 128)]
        [InlineData("GxRgb5A3", 64)]
        [InlineData("Gx.CI4.TLUT.IA8", 64)]
        [InlineData("Gx.CI8.TLUT.RGB565", 576)]
        [InlineData("Gx.CI14X2.TLUT.RGB5A3", 32832)]
        public void StorageEstimateRoundsTilesAndBudgetsFullTlut(string id, long expected) {
            TextureAssetProcessorSettings settings = new() { ColorFormatId = id };
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(3, 5, settings, out long bytes));
            Assert.Equal(expected, bytes);
        }

        /// <summary>Native textures reject uncapped oversized dimensions and apply authored resizing before the hardware limit.</summary>
        [Fact]
        public void ExtentValidationFollowsResize() {
            TextureAssetProcessorSettings settings = new() { ColorFormatId = "Gx.RGBA8" };
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(2048, 1024, settings, out _));
            settings.MaxResolution = 512;
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(2048, 1024, settings, out long bytes));
            Assert.Equal(512L * 256L * 4L, bytes);
        }
    }
}
