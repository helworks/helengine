using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the texture VRAM estimator mirrors the texture asset processor's resize and packing rules.
    /// </summary>
    public sealed class TextureVramUsageCalculatorTests {
        /// <summary>Counts native block rows with both thirty-two-block and 256-byte pitch alignment, excluding the file header.</summary>
        /// <param name="formatId">Native Xenos texture format chosen in platform settings.</param>
        /// <param name="expectedBytes">Independent storage size for a three by five source image.</param>
        [Theory]
        [InlineData("Xbox360.Linear.8_8_8_8", 1280)]
        [InlineData("Xbox360.Linear.16_16_16_16_FLOAT", 1280)]
        [InlineData("Xbox360.Linear.32_32_32_32_FLOAT", 2560)]
        [InlineData("Xbox360.Linear.DXT1", 512)]
        [InlineData("Xbox360.Linear.DXT2_3", 1024)]
        [InlineData("Xbox360.Linear.DXT4_5", 1024)]
        [InlineData("Xbox360.Linear.DXN", 1024)]
        [InlineData("Xbox360.Linear.CTX1", 512)]
        public void NativeXbox360FormatsIncludeAlignedBlockRows(string formatId, long expectedBytes) {
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(3, 5, CreateSettings(formatId, 0), out long bytes));
            Assert.Equal(expectedBytes, bytes);
        }

        /// <summary>Rejects non-texture or unverified entries instead of estimating a different representation under their names.</summary>
        [Theory]
        [InlineData("Xbox360.Linear.16_16_EDRAM")]
        [InlineData("Xbox360.Linear.1_REVERSE")]
        [InlineData("Xbox360.Linear.16_MPEG")]
        public void NativeXbox360UnsupportedFormatsHaveNoEstimate(string formatId) {
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(3, 5, CreateSettings(formatId, 0), out long bytes));
            Assert.Equal(0, bytes);
        }

        /// <summary>Resizes first and permits the full native 2D extent only when the resulting dimensions fit.</summary>
        [Fact]
        public void NativeXbox360FormatResizesBeforeHardwareExtentValidation() {
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(16384, 8192, CreateSettings("Xbox360.Linear.DXT1", 0), out _));
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(16384, 8192, CreateSettings("Xbox360.Linear.DXT1", 256), out long bytes));
            Assert.Equal(16384, bytes);
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(8192, 8192, CreateSettings("Xbox360.Linear.DXT1", 0), out long largestBytes));
            Assert.Equal(33554432, largestBytes);
        }

        /// <summary>Reports native GPU bytes including pitch or POT/block padding, with the P8 palette budget and no file header.</summary>
        /// <param name="formatId">Native format selected in platform import settings.</param>
        /// <param name="expectedBytes">Independent byte count for a three by five texture.</param>
        [Theory]
        [InlineData("Xbox.Linear.A8R8G8B8", 320)]
        [InlineData("Xbox.Swizzled.A8R8G8B8", 128)]
        [InlineData("Xbox.Swizzled.I8_A8R8G8B8", 1056)]
        [InlineData("Xbox.DXT1", 16)]
        [InlineData("Xbox.DXT3", 32)]
        [InlineData("Xbox.DXT5", 32)]
        public void NativeXboxFormatsIncludeHardwareStoragePadding(string formatId, long expectedBytes) {
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(3, 5, CreateSettings(formatId, 0), out long bytes));
            Assert.Equal(expectedBytes, bytes);
        }

        /// <summary>Applies the shared aspect-preserving cap before calculating native blocks and rejects remaining oversized dimensions.</summary>
        [Fact]
        public void NativeXboxFormatResizesBeforeHardwareExtentValidation() {
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(8192, 4096, CreateSettings("Xbox.DXT1", 0), out _));
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(8192, 4096, CreateSettings("Xbox.DXT1", 256), out long bytes));
            Assert.Equal(256L * 128L / 2L, bytes);
        }

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

        /// <summary>
        /// Estimates the actual packed payload for odd dimensions, including
        /// independent row padding and YUV pairs whose final texel is duplicated.
        /// </summary>
        /// <param name="format">Format whose size is estimated.</param>
        /// <param name="expectedBytes">Expected byte count for a three by three image.</param>
        [Theory]
        [InlineData("Rgba5551", 18)]
        [InlineData("Ia4", 6)]
        [InlineData("Ia8", 9)]
        [InlineData("Ia16", 18)]
        [InlineData("I4", 6)]
        [InlineData("I8", 9)]
        [InlineData("Yuv16", 24)]
        public void TryCalculateBytes_NewFormatsIncludeOddRowPadding(string format, int expectedBytes) {
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(3, 3, CreateSettings(format, 0), out long bytes));
            Assert.Equal(expectedBytes, bytes);
        }

        /// <summary>
        /// Packed-size estimates use the same nearest resize dimensions as the
        /// processor and reject undefined formats without inventing a byte size.
        /// </summary>
        [Fact]
        public void TryCalculateBytes_NewFormatsResizeBeforeEstimating() {
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(6, 6, CreateSettings("Yuv16", 3), out long bytes));
            Assert.Equal(24, bytes);
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(3, 3, CreateSettings("255", 0), out _));
        }

        /// <summary>
        /// New codec layouts too large for an engine pixel array report an
        /// unresolved estimate instead of throwing or wrapping the byte count.
        /// </summary>
        /// <param name="format">New layout whose source dimensions exceed its array limit.</param>
        [Theory]
        [InlineData("Rgba5551")]
        [InlineData("Ia4")]
        [InlineData("Yuv16")]
        public void TryCalculateBytes_NewFormatsRejectUnrepresentablePayload(string format) {
            Assert.False(TextureVramUsageCalculator.TryCalculateBytes(int.MaxValue, 3, CreateSettings(format, 0), out long bytes));
            Assert.Equal(0, bytes);
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(int.MaxValue, 3, CreateSettings("Rgba32", 0), out long legacyBytes));
            Assert.Equal((long)int.MaxValue * 3 * 4, legacyBytes);
        }

        /// <summary>
        /// Creates the minimal format and resize settings needed by size estimates.
        /// </summary>
        /// <param name="colorFormatId">Target format identifier.</param>
        /// <param name="maxResolution">Maximum source dimension, or zero for no resize.</param>
        /// <returns>Settings used by the estimator.</returns>
        static TextureAssetProcessorSettings CreateSettings(string colorFormatId, int maxResolution) {
            return new TextureAssetProcessorSettings {
                ColorFormatId = colorFormatId,
                MaxResolution = maxResolution
            };
        }
    }
}
