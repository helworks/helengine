using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the texture VRAM estimator mirrors the texture asset processor's resize and packing rules.
    /// </summary>
    public sealed class TextureVramUsageCalculatorTests {
        [Fact]
        public void TryCalculateBytes_ForRgba32_UsesFourBytesPerTexel() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(256, 128, CreateSettings("Rgba32", 0), out long bytes);

            Assert.True(resolved);
            Assert.Equal(256L * 128L * 4L, bytes);
        }

        [Fact]
        public void TryCalculateBytes_ForRgba4444_UsesTwoBytesPerTexel() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(64, 64, CreateSettings("Rgba4444", 0), out long bytes);

            Assert.True(resolved);
            Assert.Equal(64L * 64L * 2L, bytes);
        }

        [Fact]
        public void TryCalculateBytes_ForIndexed8_AddsPaletteBytes() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(64, 64, CreateSettings("Indexed8", 0), out long bytes);

            Assert.True(resolved);
            Assert.Equal((64L * 64L) + (256L * 4L), bytes);
        }

        [Fact]
        public void TryCalculateBytes_ForIndexed4_PacksTwoTexelsPerByteAndAddsPalette() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(33, 1, CreateSettings("Indexed4", 0), out long bytes);

            Assert.True(resolved);
            Assert.Equal(((33L + 1L) / 2L) + (16L * 4L), bytes);
        }

        [Fact]
        public void TryCalculateBytes_WhenMaxResolutionClamps_ScalesBothAxes() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(512, 256, CreateSettings("Rgba32", 128), out long bytes);

            Assert.True(resolved);
            Assert.Equal(128L * 64L * 4L, bytes);
        }

        [Fact]
        public void TryCalculateBytes_WhenSourceFitsMaxResolution_KeepsSourceDimensions() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(100, 40, CreateSettings("Rgba32", 128), out long bytes);

            Assert.True(resolved);
            Assert.Equal(100L * 40L * 4L, bytes);
        }

        [Fact]
        public void TryCalculateBytes_ForPlatformOwnedFormatId_ReturnsFalse() {
            bool resolved = TextureVramUsageCalculator.TryCalculateBytes(64, 64, CreateSettings("ps2-ct32", 0), out _);

            Assert.False(resolved);
        }

        [Fact]
        public void FormatBytes_FormatsCompactUnits() {
            Assert.Equal("512 B", TextureVramUsageCalculator.FormatBytes(512));
            Assert.Equal("16 KB", TextureVramUsageCalculator.FormatBytes(16 * 1024));
            Assert.Equal("4 MB", TextureVramUsageCalculator.FormatBytes(4L * 1024L * 1024L));
            Assert.Equal("1.5 MB", TextureVramUsageCalculator.FormatBytes((1024L + 512L) * 1024L));
        }

        static TextureAssetProcessorSettings CreateSettings(string colorFormatId, int maxResolution) {
            return new TextureAssetProcessorSettings {
                ColorFormatId = colorFormatId,
                MaxResolution = maxResolution
            };
        }
    }
}
