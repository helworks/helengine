using helengine.baseplatform.Player;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the render size an adapter declares is validated.
    /// </summary>
    public sealed class PlayerRenderSizeTests {
        /// <summary>A positive size and pixel aspect are kept.</summary>
        [Fact]
        public void Ctor_KeepsPositiveSizeAndPixelAspect() {
            PlayerRenderSize size = new PlayerRenderSize(320, 224, 14d / 15d);

            Assert.Equal(320, size.Width);
            Assert.Equal(224, size.Height);
            Assert.Equal(14d / 15d, size.PixelAspect);
        }

        /// <summary>Zero, negative and absurd sizes are rejected.</summary>
        [Theory]
        [InlineData(0, 224)]
        [InlineData(320, -1)]
        [InlineData(8193, 224)]
        public void Ctor_RejectsInvalidSize(int width, int height) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRenderSize(width, height, 1d));
        }

        /// <summary>A non-finite or non-positive pixel aspect is rejected under its own parameter name.</summary>
        [Theory]
        [InlineData(0d)]
        [InlineData(-1d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Ctor_RejectsInvalidPixelAspect(double pixelAspect) {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRenderSize(320, 224, pixelAspect));

            Assert.Equal("pixelAspect", exception.ParamName);
        }
    }
}
