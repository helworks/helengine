using helengine.vfx;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.tests.effects {
    /// <summary>
    /// Covers RainbowExpand's parameter parsing: the slot layout its shader depends on, the documented
    /// defaults, and the rejection of values the shader could not interpret.
    /// </summary>
    public class RainbowExpandEffectTests {
        /// <summary>
        /// With no parameters supplied, the resolved slots must match the defaults advertised in the
        /// effect's parameter descriptors and in CLI help output.
        /// </summary>
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

        /// <summary>
        /// Confirms every supplied parameter lands in the slot index its shader reads.
        /// </summary>
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

        /// <summary>
        /// An easing name that does not exist must fail rather than fall back to Linear.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_InvalidEasing_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["Easing"] = "NotARealEasing" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// A numeric easing value outside the declared enum range must fail; Enum.TryParse alone would
        /// happily accept it and hand the shader an undefined branch selector.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_OutOfRangeEasingNumeric_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["Easing"] = "42" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        /// <summary>
        /// A background color that is not three parseable numbers must fail rather than silently
        /// compositing against black.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_InvalidBackgroundColor_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["BackgroundColor"] = "not,a,color" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }
    }
}
