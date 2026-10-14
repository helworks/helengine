using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests.effects {
    /// <summary>
    /// Covers the gaussian-blur layer effect definition: its registration, two-pass shape with a half-resolution
    /// intermediate target and its bounded radius.
    /// </summary>
    public class GaussianBlurEffectTests {
        /// <summary>
        /// The effect is a layer effect with one input and the two passes the shader implements.
        /// </summary>
        [Fact]
        public void Definition_IsATwoPassLayerEffectWithHalfResolutionTarget() {
            EffectAsset effect = BuiltInVfxEffects.GaussianBlur();

            Assert.Equal("gaussian-blur", effect.EffectId);
            Assert.Equal("Gaussian Blur", effect.DisplayName);
            Assert.Equal(EffectCategory.Layer, effect.Category);
            Assert.Equal(new[] { "Source" }, effect.Inputs.Select(input => input.Name));
            Assert.False(effect.Inputs[0].RequiresAlpha);
            EffectTargetAsset target = Assert.Single(effect.Targets);
            Assert.Equal(0.5f, target.Scale);
            Assert.Equal(new[] { "GaussianBlurHorizontalPS", "GaussianBlurVerticalPS" }, effect.Passes.Select(pass => pass.PixelEntryPoint));
            Assert.Equal(target.Name, effect.Passes[0].Writes);
            Assert.Equal(EffectAsset.OutputTargetName, effect.Passes[1].Writes);
            Assert.Equal(new[] { target.Name, "Source" }, effect.Passes[1].Reads);
        }

        /// <summary>
        /// The radius parameter is bounded 0..96 with a 24 pixel default in slot 0.
        /// </summary>
        [Fact]
        public void Radius_IsBoundedWithDefaultOf24() {
            EffectParameterAsset radius = Assert.Single(BuiltInVfxEffects.GaussianBlur().Parameters);

            Assert.Equal("Radius", radius.Name);
            Assert.Equal(0, radius.Slot);
            Assert.Equal(0f, radius.Minimum);
            Assert.Equal(96f, radius.Maximum);
            float[] slots = VfxParameterSlotResolver.Resolve(BuiltInVfxEffects.GaussianBlur(), new Dictionary<string, string>());
            Assert.Equal(24f, slots[0]);
        }

        /// <summary>
        /// The effect follows the three existing layer effects in the built-in order.
        /// </summary>
        [Fact]
        public void BuiltInCatalog_ListsItAfterDepthComposite() {
            VfxEffectCatalog catalog = VfxEffectCatalog.CreateBuiltIn();

            Assert.Equal(new[] { "rainbow-expand", "rainbow-aura", "depth-composite", "gaussian-blur" }, catalog.KnownIds.Take(4));
        }
    }
}
