using helengine.baseplatform.Player;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the render size an adapter declares is validated.
    /// </summary>
    public sealed class PlayerRenderSizeTests {
        /// <summary>A positive size is kept.</summary>
        [Fact]
        public void Ctor_KeepsPositiveSize() {
            PlayerRenderSize size = new PlayerRenderSize(320, 224);

            Assert.Equal(320, size.Width);
            Assert.Equal(224, size.Height);
        }

        /// <summary>Zero, negative and absurd sizes are rejected.</summary>
        [Theory]
        [InlineData(0, 224)]
        [InlineData(320, -1)]
        [InlineData(8193, 224)]
        public void Ctor_RejectsInvalidSize(int width, int height) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRenderSize(width, height));
        }
    }
}
