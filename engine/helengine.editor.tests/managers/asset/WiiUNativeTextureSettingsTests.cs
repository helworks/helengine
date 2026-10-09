using helengine;

namespace helengine.editor.tests.managers.asset {
    /// <summary>Checks native Wii U memory accounting follows actual aligned GX2 allocation rather than compact transport bytes.</summary>
    public sealed class WiiUNativeTextureSettingsTests {
        /// <summary>Checks scalar and block pitch alignment and even NV12 GPU planes against independent allocation fixtures.</summary>
        [Theory]
        [InlineData("WiiU.UNORM_R8", 1280, 15)]
        [InlineData("WiiU.UNORM_R8_G8_B8_A8", 1280, 60)]
        [InlineData("WiiU.FLOAT_R16_G16_B16_A16", 2560, 120)]
        [InlineData("WiiU.FLOAT_R32_G32_B32_A32", 5120, 240)]
        [InlineData("WiiU.UNORM_BC1", 1024, 16)]
        [InlineData("WiiU.UNORM_BC2", 2048, 32)]
        [InlineData("WiiU.SNORM_BC5", 2048, 32)]
        [InlineData("WiiU.UNORM_NV12", 2304, 36)]
        public void MemoryEstimator_CountsAlignedGpuAllocation(string id, long gpuBytes, int transportBytes) {
            Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat(id, out var format)); Assert.Equal(transportBytes, format.GetTransportByteLength(3, 5)); Assert.Equal(gpuBytes, format.GetVramByteLength(3, 5));
            TextureAssetProcessorSettings settings = new() { ColorFormatId = id }; Assert.True(TextureVramUsageCalculator.TryCalculateBytes(3, 5, settings, out long bytes)); Assert.Equal(gpuBytes, bytes); Assert.False(settings.UsesGenericColorFormat()); Assert.False(settings.UsesIndexedColorFormat());
        }
        /// <summary>Checks the full 128-bit native format extent does not overflow an intermediate bit count.</summary>
        [Fact]
        public void MemoryEstimator_MaximumFloat128UsesByteArithmetic() { Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat("WiiU.FLOAT_R32_G32_B32_A32", out var format)); Assert.Equal(1073741824, format.GetVramByteLength(8192, 8192)); }
        /// <summary>Applies the shared resize cap before validating the native two-dimensional extent.</summary>
        [Fact]
        public void MemoryEstimator_ResizesBeforeHardwareExtentCheck() {
            TextureAssetProcessorSettings settings = new() { ColorFormatId = "WiiU.UNORM_R8_G8_B8_A8" }; Assert.False(TextureVramUsageCalculator.TryCalculateBytes(16384, 8192, settings, out _)); settings.MaxResolution = 256;
            Assert.True(TextureVramUsageCalculator.TryCalculateBytes(16384, 8192, settings, out long bytes)); Assert.Equal(256 * 128 * 4, bytes);
        }
    }
}
