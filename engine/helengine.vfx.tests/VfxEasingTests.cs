using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Pins the C# easing curve values. These formulas are duplicated in VfxCommon.hlsli's
    /// ApplyEasing, so these expectations double as the reference the HLSL side must match.
    /// </summary>
    public class VfxEasingTests {
        /// <summary>
        /// Checks each curve against hand-computed values at its characteristic points.
        /// </summary>
        /// <param name="kind">Easing curve under test.</param>
        /// <param name="t">Normalized progress input.</param>
        /// <param name="expected">Expected eased output.</param>
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

        /// <summary>
        /// Progress outside [0, 1] must clamp rather than extrapolate, matching the shader's saturate.
        /// </summary>
        [Fact]
        public void Apply_ValuesOutsideZeroToOne_AreClamped() {
            Assert.Equal(0f, VfxEasing.Apply(VfxEasingKind.Linear, -1f));
            Assert.Equal(1f, VfxEasing.Apply(VfxEasingKind.Linear, 2f));
        }

        /// <summary>
        /// The two-piece ease-in-out curve must still start at 0 and end at 1 with no discontinuity.
        /// </summary>
        /// <param name="kind">Easing curve under test.</param>
        /// <param name="t">Endpoint progress value that must map to itself.</param>
        [Theory]
        [InlineData(VfxEasingKind.EaseInOut, 0f)]
        [InlineData(VfxEasingKind.EaseInOut, 1f)]
        public void Apply_EaseInOut_HitsEndpoints(VfxEasingKind kind, float t) {
            Assert.Equal(t, VfxEasing.Apply(kind, t), 3);
        }
    }
}
