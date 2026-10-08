using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Verifies how textual parameter values become constant slots for each parameter type.
    /// </summary>
    public class VfxParameterSlotResolverTests {
        /// <summary>
        /// Omitted parameters fall back to their declared defaults.
        /// </summary>
        [Fact]
        public void Resolve_NoValues_UsesDefaults() {
            float[] slots = VfxParameterSlotResolver.Resolve(VfxEffectValidatorTests.TwoPass(), new Dictionary<string, string>());
            Assert.Equal(4f, slots[0]);
        }

        /// <summary>
        /// An RGB color is accepted for compatibility with existing compositions and receives an opaque alpha.
        /// </summary>
        [Fact]
        public void Resolve_RgbColor_FillsOpaqueAlpha() {
            EffectAsset effect = WithParameter(new EffectParameterAsset { Name = "Tint", Type = EffectParameterType.Color, Minimum = 0, Maximum = 1, Slot = 4 });
            float[] slots = VfxParameterSlotResolver.Resolve(effect, new Dictionary<string, string> { ["Tint"] = "0.5,0.25,0" });
            Assert.Equal(new[] { 0.5f, 0.25f, 0f, 1f }, slots.Skip(4).Take(4));
        }

        /// <summary>
        /// An RGBA color keeps its explicit alpha, which soft shadows depend on.
        /// </summary>
        [Fact]
        public void Resolve_RgbaColor_KeepsAlpha() {
            EffectAsset effect = WithParameter(new EffectParameterAsset { Name = "Tint", Type = EffectParameterType.Color, Minimum = 0, Maximum = 1, Slot = 4 });
            float[] slots = VfxParameterSlotResolver.Resolve(effect, new Dictionary<string, string> { ["Tint"] = "0,0,0,0.6" });
            Assert.Equal(0.6f, slots[7], 3);
        }

        /// <summary>
        /// A vector parameter fills consecutive slots and a switch becomes zero or one.
        /// </summary>
        [Fact]
        public void Resolve_Float2AndBool_FillTheirSlots() {
            EffectAsset effect = WithParameter(
                new EffectParameterAsset { Name = "Offset", Type = EffectParameterType.Float2, Slot = 4 },
                new EffectParameterAsset { Name = "Enabled", Type = EffectParameterType.Bool, Slot = 6 });
            float[] slots = VfxParameterSlotResolver.Resolve(effect, new Dictionary<string, string> { ["Offset"] = "3,-2", ["Enabled"] = "true" });
            Assert.Equal(new[] { 3f, -2f, 1f }, slots.Skip(4).Take(3));
        }

        /// <summary>
        /// Values outside the declared range and names the effect does not declare are rejected.
        /// </summary>
        [Fact]
        public void Resolve_OutOfRangeOrUnknown_Throws() {
            EffectAsset effect = VfxEffectValidatorTests.TwoPass();
            Assert.Throws<ArgumentException>(() => VfxParameterSlotResolver.Resolve(effect, new Dictionary<string, string> { ["Radius"] = "65" }));
            Assert.Throws<ArgumentException>(() => VfxParameterSlotResolver.Resolve(effect, new Dictionary<string, string> { ["Sigma"] = "1" }));
        }

        /// <summary>
        /// Builds the baseline effect with extra parameters appended.
        /// </summary>
        /// <param name="extra">Parameters to add after the baseline radius.</param>
        /// <returns>Effect with the extra parameters.</returns>
        static EffectAsset WithParameter(params EffectParameterAsset[] extra) {
            EffectAsset effect = VfxEffectValidatorTests.TwoPass();
            effect.Parameters = effect.Parameters.Concat(extra).ToArray();
            VfxEffectValidator.Validate(effect);
            return effect;
        }
    }
}
