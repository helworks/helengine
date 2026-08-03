using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class VfxEasingTests {
        [Theory]
        [InlineData(VfxEasingKind.Linear, 0f, 0f)]
        [InlineData(VfxEasingKind.Linear, 0.5f, 0.5f)]
        [InlineData(VfxEasingKind.Linear, 1f, 1f)]
        [InlineData(VfxEasingKind.EaseIn, 0.5f, 0.25f)]
        [InlineData(VfxEasingKind.EaseOut, 0.5f, 0.75f)]
        public void Apply_KnownValues_MatchesExpected(VfxEasingKind kind, float t, float expected) {
            float result = VfxEasing.Apply(kind, t);

            Assert.Equal(expected, result, 3);
        }

        [Fact]
        public void Apply_ValuesOutsideZeroToOne_AreClamped() {
            Assert.Equal(0f, VfxEasing.Apply(VfxEasingKind.Linear, -1f));
            Assert.Equal(1f, VfxEasing.Apply(VfxEasingKind.Linear, 2f));
        }

        [Theory]
        [InlineData(VfxEasingKind.EaseInOut, 0f)]
        [InlineData(VfxEasingKind.EaseInOut, 1f)]
        public void Apply_EaseInOut_HitsEndpoints(VfxEasingKind kind, float t) {
            Assert.Equal(t, VfxEasing.Apply(kind, t), 3);
        }
    }
}
