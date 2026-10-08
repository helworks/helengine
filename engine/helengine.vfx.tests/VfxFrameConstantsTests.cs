using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Pins the constant-buffer float layout shared with every effect shader's cbuffer declaration in
    /// VfxCommon.hlsli. A change here without a matching HLSL change silently corrupts every effect.
    /// </summary>
    public class VfxFrameConstantsTests {
        /// <summary>
        /// A parameter slot array of the wrong length would shift every value in the buffer, so it is
        /// rejected instead of being padded or truncated.
        /// </summary>
        [Fact]
        public void Build_WrongParamSlotLength_Throws() {
            Assert.Throws<ArgumentException>(() => VfxFrameConstants.Build(0f, 4, 4, new float[4]));
        }

        /// <summary>
        /// Confirms the header floats come first in time/width/height order and the effect's parameter
        /// slots start immediately after them.
        /// </summary>
        [Fact]
        public void Build_ValidInput_LaysOutHeaderThenParams() {
            float[] paramSlots = new float[VfxFrameConstants.ParamSlotCount];
            paramSlots[0] = 7f;

            float[] result = VfxFrameConstants.Build(0.5f, 100, 200, paramSlots);

            Assert.Equal(VfxFrameConstants.TotalFloatCount, result.Length);
            Assert.Equal(0.5f, result[0]);
            Assert.Equal(100f, result[1]);
            Assert.Equal(200f, result[2]);
            Assert.Equal(7f, result[VfxFrameConstants.HeaderFloatCount]);
        }

        /// <summary>
        /// Confirms the pass block follows the parameter slots: pass constants, then the pass target and main input
        /// texel sizes, matching the PassConstants/TexelSize/MainTexelSize order in VfxCommon.hlsli.
        /// </summary>
        [Fact]
        public void Build_PassValues_FollowParameterSlots() {
            float[] result = VfxFrameConstants.Build(0f, 50, 25, new float[VfxFrameConstants.ParamSlotCount], new float4(1, 2, 3, 4), 100, 50);
            int pass = VfxFrameConstants.HeaderFloatCount + VfxFrameConstants.ParamSlotCount;

            Assert.Equal(28, VfxFrameConstants.TotalFloatCount);
            Assert.Equal(new[] { 1f, 2f, 3f, 4f }, result.Skip(pass).Take(4));
            Assert.Equal(1f / 50, result[pass + 4]);
            Assert.Equal(1f / 25, result[pass + 5]);
            Assert.Equal(1f / 100, result[pass + 6]);
            Assert.Equal(1f / 50, result[pass + 7]);
        }
    }
}
