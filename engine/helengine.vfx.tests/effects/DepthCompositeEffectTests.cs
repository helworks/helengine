using helengine.vfx;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.tests.effects {
    /// <summary>
    /// Covers DepthComposite's parameter parsing and its declared input roles, since this effect is
    /// the first to require more than the usual Source/Mask pair.
    /// </summary>
    public class DepthCompositeEffectTests {
        /// <summary>
        /// Confirms the effect declares exactly the three roles its shader binds to t0, t1, and t2, in
        /// that order, and that only Subject is required to carry real alpha.
        /// </summary>
        [Fact]
        public void InputRoles_DeclaresSubjectRenderColorRenderDepthInOrder() {
            var effect = new DepthCompositeEffect();

            Assert.Equal(new[] { "Subject", "RenderColor", "RenderDepth" }, effect.InputRoles);
            Assert.Equal(new[] { "Subject" }, effect.AlphaRequiredInputRoles);
        }

        /// <summary>
        /// With no parameters supplied, the resolved slot must match the documented default.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_Default_MatchesDocumentedDefault() {
            var effect = new DepthCompositeEffect();

            float[] slots = effect.ResolveParameterSlots(new Dictionary<string, string>());

            Assert.Equal(VfxFrameConstants.ParamSlotCount, slots.Length);
            Assert.Equal(0f, slots[0]);
        }

        /// <summary>
        /// An explicit DepthThreshold must land in slot 0, including negative values since depth units
        /// are caller-defined and may legitimately be negative.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_ExplicitDepthThreshold_IsParsed() {
            var effect = new DepthCompositeEffect();
            var values = new Dictionary<string, string> { ["DepthThreshold"] = "-12.5" };

            float[] slots = effect.ResolveParameterSlots(values);

            Assert.Equal(-12.5f, slots[0], 3);
        }

        /// <summary>
        /// A non-numeric DepthThreshold must fail rather than silently default to 0.
        /// </summary>
        [Fact]
        public void ResolveParameterSlots_InvalidDepthThreshold_Throws() {
            var effect = new DepthCompositeEffect();
            var values = new Dictionary<string, string> { ["DepthThreshold"] = "not-a-number" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }
    }
}
