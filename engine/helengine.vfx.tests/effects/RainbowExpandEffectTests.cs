using helengine.vfx;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.tests.effects {
    public class RainbowExpandEffectTests {
        [Fact]
        public void ResolveParameterSlots_Defaults_MatchDocumentedDefaults() {
            var effect = new RainbowExpandEffect();

            float[] slots = effect.ResolveParameterSlots(new Dictionary<string, string>());

            Assert.Equal(VfxFrameConstants.ParamSlotCount, slots.Length);
            Assert.Equal(1f, slots[0]);
            Assert.Equal(1f, slots[1]);
            Assert.Equal(2f, slots[2]);
            Assert.Equal((float)VfxEasingKind.Linear, slots[3]);
            Assert.Equal(0f, slots[4]);
            Assert.Equal(0f, slots[5]);
            Assert.Equal(0f, slots[6]);
        }

        [Fact]
        public void ResolveParameterSlots_ExplicitValues_AreParsed() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> {
                ["HueCyclesPerClip"] = "3",
                ["StartScale"] = "0.5",
                ["EndScale"] = "4",
                ["Easing"] = "EaseInOut",
                ["BackgroundColor"] = "0.1,0.2,0.3"
            };

            float[] slots = effect.ResolveParameterSlots(values);

            Assert.Equal(3f, slots[0]);
            Assert.Equal(0.5f, slots[1]);
            Assert.Equal(4f, slots[2]);
            Assert.Equal((float)VfxEasingKind.EaseInOut, slots[3]);
            Assert.Equal(0.1f, slots[4], 3);
            Assert.Equal(0.2f, slots[5], 3);
            Assert.Equal(0.3f, slots[6], 3);
        }

        [Fact]
        public void ResolveParameterSlots_InvalidEasing_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["Easing"] = "NotARealEasing" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        [Fact]
        public void ResolveParameterSlots_InvalidBackgroundColor_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["BackgroundColor"] = "not,a,color" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }
    }
}
