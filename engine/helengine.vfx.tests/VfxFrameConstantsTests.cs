using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class VfxFrameConstantsTests {
        [Fact]
        public void Build_WrongParamSlotLength_Throws() {
            Assert.Throws<ArgumentException>(() => VfxFrameConstants.Build(0f, 4, 4, new float[4]));
        }

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
    }
}
