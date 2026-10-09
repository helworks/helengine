using helengine.editor;

namespace helengine.editor.tests.managers.asset {
    /// <summary>Verifies that PS2 import settings expose palette processing and budget native GS storage rather than compact transfer bytes.</summary>
    public sealed class Ps2TextureSettingsTests {
        /// <summary>Indexed GS formats retain the editor's quantization controls regardless of their high-bit storage or palette precision.</summary>
        /// <param name="formatId">Native indexed format selected in per-platform settings.</param>
        [Theory]
        [InlineData("PS2.PSMT8")]
        [InlineData("PS2.PSMT4")]
        [InlineData("PS2.PSMT8H")]
        [InlineData("PS2.PSMT4HL")]
        [InlineData("PS2.PSMT4HH")]
        [InlineData("PS2.PSMT8.CLUT16")]
        [InlineData("PS2.PSMT4.CLUT16")]
        [InlineData("PS2.PSMT8H.CLUT16")]
        [InlineData("PS2.PSMT4HL.CLUT16")]
        [InlineData("PS2.PSMT4HH.CLUT16")]
        [InlineData("PS2.PSMT8.CLUT16S")]
        [InlineData("PS2.PSMT4.CLUT16S")]
        [InlineData("PS2.PSMT8H.CLUT16S")]
        [InlineData("PS2.PSMT4HL.CLUT16S")]
        [InlineData("PS2.PSMT4HH.CLUT16S")]
        public void IndexedNativeFormatsExposeQuantization(string formatId) {
            TextureAssetProcessorSettings settings = new TextureAssetProcessorSettings { ColorFormatId = formatId };
            Assert.True(settings.UsesIndexedColorFormat());
            Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed, settings.ResolveIndexingMethod());
            settings.IndexingMethodId = "unsupported";
            Assert.Throws<InvalidOperationException>(() => settings.ResolveIndexingMethod());
        }

        /// <summary>Direct color and depth storage do not offer a palette method that their payloads cannot represent.</summary>
        /// <param name="formatId">Direct GS format selected in per-platform settings.</param>
        [Theory]
        [InlineData("PS2.PSMCT32")]
        [InlineData("PS2.PSMCT24")]
        [InlineData("PS2.PSMCT16")]
        [InlineData("PS2.PSMCT16S")]
        [InlineData("PS2.PSMZ32")]
        [InlineData("PS2.PSMZ24")]
        [InlineData("PS2.PSMZ16")]
        [InlineData("PS2.PSMZ16S")]
        public void DirectNativeFormatsRejectQuantization(string formatId) {
            TextureAssetProcessorSettings settings = new TextureAssetProcessorSettings { ColorFormatId = formatId };
            Assert.False(settings.UsesIndexedColorFormat());
            Assert.Throws<InvalidOperationException>(() => settings.ResolveIndexingMethod());
        }

        /// <summary>Storage estimates round up native pages, including a full CLUT page even when transfer payloads occupy only a few bytes.</summary>
        /// <param name="formatId">Format whose GS page dimensions determine the allocation.</param>
        /// <param name="width">Authored width before import resizing.</param>
        /// <param name="height">Authored height before import resizing.</param>
        /// <param name="expectedBytes">Texture and palette allocation combined.</param>
        [Theory]
        [InlineData("PS2.PSMCT32", 3, 5, 8192)]
        [InlineData("PS2.PSMCT24", 3, 5, 8192)]
        [InlineData("PS2.PSMCT16", 3, 5, 8192)]
        [InlineData("PS2.PSMCT16S", 3, 5, 8192)]
        [InlineData("PS2.PSMT8", 3, 5, 16384)]
        [InlineData("PS2.PSMT4", 3, 5, 16384)]
        [InlineData("PS2.PSMT8H", 3, 5, 16384)]
        [InlineData("PS2.PSMT4HL", 3, 5, 16384)]
        [InlineData("PS2.PSMT4HH", 3, 5, 16384)]
        [InlineData("PS2.PSMZ32", 3, 5, 8192)]
        [InlineData("PS2.PSMZ24", 3, 5, 8192)]
        [InlineData("PS2.PSMZ16", 3, 5, 8192)]
        [InlineData("PS2.PSMZ16S", 3, 5, 8192)]
        [InlineData("PS2.PSMT4HH.CLUT16", 3, 5, 16384)]
        [InlineData("PS2.PSMT8H.CLUT16S", 3, 5, 16384)]
        [InlineData("PS2.PSMCT32", 65, 33, 32768)]
        [InlineData("PS2.PSMCT16S", 65, 65, 32768)]
        [InlineData("PS2.PSMT8H", 65, 33, 40960)]
        [InlineData("PS2.PSMT8", 129, 65, 40960)]
        [InlineData("PS2.PSMT4", 129, 129, 40960)]
        [InlineData("PS2.PSMCT32", 1024, 1024, 4194304)]
        public void VramEstimateUsesNativePages(string formatId, int width, int height, int expectedBytes) {
            TextureAssetProcessorSettings settings = new TextureAssetProcessorSettings { ColorFormatId = formatId };
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(width, height, settings, out long bytes));
            Assert.Equal(expectedBytes, bytes);
        }

        /// <summary>Resize runs before the native extent check; uncapped dimensions outside the GS texture limit remain rejected.</summary>
        [Fact]
        public void VramEstimateAppliesResolutionCapBeforeNativeLimit() {
            TextureAssetProcessorSettings settings = new TextureAssetProcessorSettings { ColorFormatId = "PS2.PSMCT32" };
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(2048, 64, settings, out _));
            settings.MaxResolution = 512;
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(2048, 64, settings, out long bytes));
            Assert.Equal(65536, bytes);
        }
    }
}
