using helengine;

namespace helengine.editor.tests.managers.asset {
    /// <summary>Checks native Vita indexing settings and GPU memory accounting without charging descriptor bytes.</summary>
    public sealed class VitaNativeTextureSettingsTests {
        /// <summary>Checks native pitch, complete block storage, minimum PVRTC words and full palette capacity.</summary>
        [Theory]
        [InlineData("Vita.U8U8U8U8_ABGR", 160)]
        [InlineData("Vita.P4_ABGR", 84)]
        [InlineData("Vita.P8_ABGR", 1064)]
        [InlineData("Vita.UBC1_ABGR", 32)]
        [InlineData("Vita.UBC2_ABGR", 64)]
        [InlineData("Vita.PVRT2BPP_ABGR", 32)]
        [InlineData("Vita.PVRTII4BPP_ABGR", 32)]
        public void MemoryEstimator_CountsCanonicalNativeGpuStorage(string id, long expected) {
            TextureAssetProcessorSettings settings = new() { ColorFormatId = id, MaxResolution = 0 };
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(3, 5, settings, out long bytes)); Assert.Equal(expected, bytes);
        }
        /// <summary>Exposes quantization choices only for native P4/P8 and keeps their ids outside the generic processor.</summary>
        [Theory]
        [InlineData("Vita.P4_ABGR", true)]
        [InlineData("Vita.P8_ARGB", true)]
        [InlineData("Vita.U8U8U8U8_ABGR", false)]
        public void IndexingSettings_RecognizeNativePaletteIds(string id, bool expected) {
            TextureAssetProcessorSettings settings = new() { ColorFormatId = id };
            Assert.Equal(expected, settings.UsesIndexedColorFormat()); Assert.False(settings.UsesGenericColorFormat());
            if (expected) Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed, settings.ResolveIndexingMethod());
            else Assert.Throws<InvalidOperationException>(() => settings.ResolveIndexingMethod());
        }
        /// <summary>Resizes before checking the hardware extent and rejects dimensions that still exceed the native limit.</summary>
        [Fact]
        public void MemoryEstimator_ResizesBeforeCheckingGxmExtent() {
            TextureAssetProcessorSettings settings = new() { ColorFormatId = "Vita.U8U8U8U8_ABGR" };
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(8192, 4096, settings, out _)); settings.MaxResolution = 256;
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(8192, 4096, settings, out long bytes)); Assert.Equal(256 * 128 * 4, bytes);
        }
    }
}
