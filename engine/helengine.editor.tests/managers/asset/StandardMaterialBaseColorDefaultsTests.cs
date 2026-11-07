using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the colour helpers of the standard-material base-colour buffer contract.
    /// </summary>
    public class StandardMaterialBaseColorDefaultsTests {
        /// <summary>
        /// Ensures the linear to sRGB conversion hits the known anchor values and leaves alpha untouched.
        /// </summary>
        [Fact]
        public void LinearToSrgb_encodesColourChannelsAndKeepsAlpha() {
            float4 result = StandardMaterialBaseColorDefaults.LinearToSrgb(new float4(0f, 0.5f, 1f, 0.5f));

            Assert.Equal(0f, result.X.ToSingle(), 0.0001f);
            Assert.Equal(0.7354f, result.Y.ToSingle(), 0.0005f);
            Assert.Equal(1f, result.Z.ToSingle(), 0.0001f);
            Assert.Equal(0.5f, result.W.ToSingle(), 0.0001f);
        }

        /// <summary>
        /// Ensures the linear segment near black uses the 12.92 slope.
        /// </summary>
        [Fact]
        public void LinearToSrgb_usesTheLinearSegmentNearBlack() {
            float4 result = StandardMaterialBaseColorDefaults.LinearToSrgb(new float4(0.002f, 0.002f, 0.002f, 1f));

            Assert.Equal(0.002f * 12.92f, result.X.ToSingle(), 0.0001f);
        }

        /// <summary>
        /// Ensures a buffer payload reads back as the colour it was written from.
        /// </summary>
        [Fact]
        public void ReadConstantBufferData_roundTripsTheColour() {
            float4 result = StandardMaterialBaseColorDefaults.ReadConstantBufferData(
                StandardMaterialBaseColorDefaults.CreateConstantBufferData(new float4(0.1f, 0.2f, 0.3f, 0.4f)));

            Assert.Equal(0.1f, result.X.ToSingle(), 0.0001f);
            Assert.Equal(0.2f, result.Y.ToSingle(), 0.0001f);
            Assert.Equal(0.3f, result.Z.ToSingle(), 0.0001f);
            Assert.Equal(0.4f, result.W.ToSingle(), 0.0001f);
        }
    }
}
