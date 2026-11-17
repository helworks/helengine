using helengine;

namespace helengine.core.tests.imaging {
    /// <summary>Verifies unconfigured hosts and allocation bounds before any codec is allowed to run.</summary>
    public sealed class ImageDecodeServiceTests {
        /// <summary>Unconfigured hosts expose absence explicitly, without decoding on the caller thread.</summary>
        [Fact]
        public void MissingBackendFailsExplicitly() {
            ImageDecodeService service = new ImageDecodeService(null);
            Assert.False(service.IsAvailable);
            Assert.Throws<NotSupportedException>(() => service.BeginDecode(new byte[] { 1 }, 512, 512 * 512));
        }
        /// <summary>Independent dimension and pixel limits reject requests before allocating their output.</summary>
        [Theory]
        [InlineData(0, 1)]
        [InlineData(4097, 1)]
        [InlineData(1, 0)]
        [InlineData(1, 16777217)]
        public void InvalidOutputBoundsFailBeforeCodec(int dimension, int pixels) {
            Assert.Throws<ArgumentOutOfRangeException>(() => ImageDecodeService.ValidateRequest(new byte[] { 1 }, dimension, pixels));
        }
        /// <summary>Encoded payload bounds are independent from the requested tiny output image.</summary>
        [Fact]
        public void MissingEmptyAndOversizedInputsFailBeforeCodec() {
            Assert.Throws<ArgumentNullException>(() => ImageDecodeService.ValidateRequest(null, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImageDecodeService.ValidateRequest(Array.Empty<byte>(), 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImageDecodeService.ValidateRequest(new byte[ImageDecodeService.MaximumEncodedBytes + 1], 1, 1));
            ImageDecodeService.ValidateRequest(new byte[] { 1 }, 512, 512 * 512);
        }
    }
}
