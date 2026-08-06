using helengine.vfx;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.tests.effects {
    /// <summary>
    /// Covers RainbowAura's parameter parsing: the slot layout its shader depends on, the documented
    /// defaults, and the rejection of values the shader could not interpret.
    /// </summary>
    public class RainbowAuraEffectTests {
        /// <summary>
        /// With no parameters supplied, the resolved slots must match the defaults advertised in the
        /// effect's parameter descriptors and in CLI help output.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_Defaults_MatchDocumentedDefaults() {
            var effect = new RainbowAuraEffect();

            float[] slots = effect.ResolveParameterSlots(new Dictionary<string, string>());

            Assert.Equal(VfxFrameConstants.ParamSlotCount, slots.Length);
            Assert.Equal(10f, slots[0]);
            Assert.Equal(1f, slots[1]);
            Assert.Equal(0.15f, slots[2], 3);
            Assert.Equal(360f, slots[3]);
            Assert.Equal(0.2f, slots[4], 3);
            Assert.Equal(0f, slots[5]);
            Assert.Equal((float)VfxEasingKind.Linear, slots[6]);
            Assert.Equal(5f, slots[7]);
        }

        /// <summary>
        /// Confirms every supplied parameter lands in the slot index its shader reads.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_ExplicitValues_AreParsed() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> {
                ["RepetitionCount"] = "6",
                ["StartScale"] = "0.9",
                ["ScaleStep"] = "0.25",
                ["HueSpreadDegrees"] = "180",
                ["GrowWindow"] = "0.35",
                ["HueCyclesPerClip"] = "2",
                ["Easing"] = "EaseOut",
                ["SaturationBoost"] = "8"
            };

            float[] slots = effect.ResolveParameterSlots(values);

            Assert.Equal(6f, slots[0]);
            Assert.Equal(0.9f, slots[1], 3);
            Assert.Equal(0.25f, slots[2], 3);
            Assert.Equal(180f, slots[3]);
            Assert.Equal(0.35f, slots[4], 3);
            Assert.Equal(2f, slots[5]);
            Assert.Equal((float)VfxEasingKind.EaseOut, slots[6]);
            Assert.Equal(8f, slots[7]);
        }

        /// <summary>
        /// A repetition count that is not a whole number would leave the shader's loop bound ambiguous.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_FractionalRepetitionCount_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["RepetitionCount"] = "3.5" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// Zero or negative repetitions would make the effect a no-op or an invalid loop bound.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_RepetitionCountBelowMinimum_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["RepetitionCount"] = "0" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// An excessive repetition count would make per-frame export pathologically slow, so it is
        /// rejected up front rather than left to run.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_RepetitionCountAboveMaximum_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["RepetitionCount"] = "65" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// A zero or negative grow window would divide by zero (or never finish growing) in the shader.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_NonPositiveGrowWindow_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["GrowWindow"] = "0" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// A grow window greater than 1 clip-length is meaningless since normalized time never reaches it.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_GrowWindowAboveOne_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["GrowWindow"] = "1.5" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// A negative saturation boost would invert the color around its luma rather than desaturate it.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_NegativeSaturationBoost_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["SaturationBoost"] = "-1" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// An easing name that does not exist must fail rather than fall back to Linear.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_InvalidEasing_Throws() {
            var effect = new RainbowAuraEffect();
            var values = new Dictionary<string, string> { ["Easing"] = "NotARealEasing" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }
    }
}
