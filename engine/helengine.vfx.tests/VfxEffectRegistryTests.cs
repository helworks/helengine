using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Covers effect id lookup, including the error message an unknown id produces, which is what the
    /// CLI surfaces to the user when they mistype <c>--effect</c>.
    /// </summary>
    public class VfxEffectRegistryTests {
        /// <summary>
        /// A registered effect must be retrievable by the id it declared.
        /// </summary>
        [Fact]
        public void Resolve_RegisteredId_ReturnsEffect() {
            VfxEffectRegistry.Register(new FakeEffect());

            IVfxEffect resolved = VfxEffectRegistry.Resolve("fake-effect");

            Assert.Equal("fake-effect", resolved.Id);
        }

        /// <summary>
        /// An unknown id must fail with a message that names the id the caller asked for.
        /// </summary>
        [Fact]
        public void Resolve_UnknownId_ThrowsWithMessage() {
            var exception = Assert.Throws<InvalidOperationException>(() => VfxEffectRegistry.Resolve("does-not-exist"));

            Assert.Contains("does-not-exist", exception.Message);
        }
    }
}
