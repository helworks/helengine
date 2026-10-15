using helengine.directx11;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the fixed-render-size rule: the client height is an integer multiple of the render height, and the client
    /// width is the render width stretched by the display pixel aspect at that multiple. With square pixels this is the
    /// equal integer multiple that makes point sampling reproduce every pixel as an identical block.
    /// </summary>
    public sealed class DirectX11FixedRenderSizeTests {
        /// <summary>Equal integer multiples on both axes are accepted with square pixels.</summary>
        [Theory]
        [InlineData(640, 448, 640, 448, 1)]
        [InlineData(1280, 896, 640, 448, 2)]
        [InlineData(1280, 896, 320, 224, 4)]
        public void ValidateMagnification_WithSquarePixels_AcceptsIntegerMultiples(int clientWidth, int clientHeight, int renderWidth, int renderHeight, int expected) {
            Assert.Equal(expected, DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight, 1d));
        }

        /// <summary>Fractional or mismatched magnification is rejected with square pixels.</summary>
        [Theory]
        [InlineData(1000, 700, 640, 448)]
        [InlineData(1280, 448, 640, 448)]
        [InlineData(320, 224, 640, 448)]
        public void ValidateMagnification_WithSquarePixels_RejectsNonIntegerOrUnequalMultiples(int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            Assert.Throws<ArgumentException>(() => DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight, 1d));
        }

        /// <summary>A 4:3 client of round(k*H*4/3) x k*H is accepted for framebuffers shown 4:3 (the PS2 640- and 512-wide modes).</summary>
        [Theory]
        [InlineData(1195, 896, 640, 448, 2)]
        [InlineData(896, 672, 640, 224, 3)]
        [InlineData(597, 448, 512, 224, 2)]
        [InlineData(683, 512, 512, 512, 1)]
        [InlineData(683, 512, 640, 256, 2)]
        public void ValidateMagnification_ForAFourByThreeDisplay_AcceptsTheStretchedWidth(int clientWidth, int clientHeight, int renderWidth, int renderHeight, int expected) {
            double pixelAspect = 4d * renderHeight / (3d * renderWidth);

            Assert.Equal(expected, DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight, pixelAspect));
        }

        /// <summary>A 4:3 client whose width or height is wrong is rejected.</summary>
        [Theory]
        [InlineData(1194, 896, 640, 448)]
        [InlineData(1196, 896, 640, 448)]
        [InlineData(1195, 895, 640, 448)]
        [InlineData(1280, 896, 640, 448)]
        public void ValidateMagnification_ForAFourByThreeDisplay_RejectsOtherClients(int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            double pixelAspect = 4d * renderHeight / (3d * renderWidth);

            Assert.Throws<ArgumentException>(() => DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight, pixelAspect));
        }

        /// <summary>
        /// The 320- and 256-wide PS2 modes are shown 3:2 and 22:15 (gsKit's integer magnification), so their client is
        /// round(k*H*DAW/DAH) x k*H, and the 4:3 width for the same height is rejected.
        /// </summary>
        [Theory]
        [InlineData(320, 224, 3, 2, 336, 224, 299)]
        [InlineData(320, 256, 3, 2, 768, 512, 683)]
        [InlineData(256, 224, 22, 15, 329, 224, 299)]
        [InlineData(256, 256, 22, 15, 751, 512, 683)]
        public void ValidateMagnification_ForAWiderThanFourByThreeDisplay_AcceptsOnlyItsOwnWidth(int renderWidth, int renderHeight, int displayAspectWidth, int displayAspectHeight, int clientWidth, int clientHeight, int fourByThreeClientWidth) {
            double pixelAspect = (double)displayAspectWidth * renderHeight / ((double)displayAspectHeight * renderWidth);

            Assert.Equal(clientHeight / renderHeight, DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight, pixelAspect));
            Assert.Throws<ArgumentException>(() => DirectX11FixedRenderSize.ValidateMagnification(fourByThreeClientWidth, clientHeight, renderWidth, renderHeight, pixelAspect));
        }

        /// <summary>At 1x the client width is the 4:3 width of the render height.</summary>
        [Theory]
        [InlineData(640, 448, 597)]
        [InlineData(640, 224, 299)]
        [InlineData(640, 256, 341)]
        [InlineData(640, 512, 683)]
        public void ResolveClientWidth_AtOneTimes_IsTheFourByThreeWidthOfTheHeight(int renderWidth, int renderHeight, int expected) {
            Assert.Equal(expected, DirectX11FixedRenderSize.ResolveClientWidth(renderWidth, 1, 4d * renderHeight / (3d * renderWidth)));
        }

        /// <summary>A non-finite or non-positive pixel aspect is rejected.</summary>
        [Fact]
        public void ValidateMagnification_RejectsInvalidPixelAspect() {
            Assert.Throws<ArgumentOutOfRangeException>(() => DirectX11FixedRenderSize.ValidateMagnification(640, 448, 640, 448, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => DirectX11FixedRenderSize.ValidateMagnification(640, 448, 640, 448, double.NaN));
        }
    }
}
