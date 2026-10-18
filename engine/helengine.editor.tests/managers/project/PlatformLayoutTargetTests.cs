using helengine.baseplatform.Builders;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>Validates that cook targets distinguish known geometry from unknown physical density.</summary>
    public sealed class PlatformLayoutTargetTests {
        /// <summary>Unknown density remains explicitly unknown instead of acquiring a desktop default.</summary>
        [Fact]
        public void UnknownDensity_IsNotInvented() {
            PlatformLayoutTarget target = new PlatformLayoutTarget(320, 240, 1, false, 0, 1);
            Assert.False(target.HasDpi);
            Assert.Equal(0, target.Dpi);
            Assert.Equal(320, target.Width);
        }

        /// <summary>Known target data must not contain dimensions or aspect ratios that cannot be presented.</summary>
        [Theory]
        [InlineData(0, 240, 1)]
        [InlineData(320, 0, 1)]
        [InlineData(320, 240, 0)]
        [InlineData(320, 240, double.NaN)]
        public void InvalidTarget_FailsExplicitly(int width, int height, double aspect) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformLayoutTarget(width, height, aspect, false, 0, 1));
        }
    }
}
