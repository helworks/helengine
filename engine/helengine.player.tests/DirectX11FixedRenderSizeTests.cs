using helengine.directx11;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the fixed-render-size surface only accepts exact integer magnification, which is what makes point
    /// sampling reproduce every source pixel as an identical block.
    /// </summary>
    public sealed class DirectX11FixedRenderSizeTests {
        /// <summary>Equal integer multiples on both axes are accepted and reported.</summary>
        [Theory]
        [InlineData(640, 448, 640, 448, 1)]
        [InlineData(1280, 896, 640, 448, 2)]
        [InlineData(1280, 896, 320, 224, 4)]
        public void ValidateMagnification_AcceptsIntegerMultiples(int clientWidth, int clientHeight, int renderWidth, int renderHeight, int expected) {
            Assert.Equal(expected, DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight));
        }

        /// <summary>Fractional or mismatched magnification is rejected.</summary>
        [Theory]
        [InlineData(1000, 700, 640, 448)]
        [InlineData(1280, 448, 640, 448)]
        [InlineData(320, 224, 640, 448)]
        public void ValidateMagnification_RejectsNonIntegerOrUnequalMultiples(int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            Assert.Throws<ArgumentException>(() => DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight));
        }
    }
}
