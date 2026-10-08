using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Locks the structural rules every effect definition must satisfy before any GPU resource is created.
    /// </summary>
    public class VfxEffectValidatorTests {
        /// <summary>
        /// Every shipped effect must pass its own validation, or the catalog could not be built.
        /// </summary>
        [Fact]
        public void Validate_BuiltInEffects_AreValid() {
            foreach (EffectAsset effect in BuiltInVfxEffects.All()) {
                VfxEffectValidator.Validate(effect);
            }
        }

        /// <summary>
        /// A transition always blends exactly two scenes, the outgoing one first.
        /// </summary>
        [Fact]
        public void Validate_TransitionWithOneInput_Throws() {
            EffectAsset effect = TwoPass();
            effect.Category = EffectCategory.Transition;
            Assert.Throws<InvalidDataException>(() => VfxEffectValidator.Validate(effect));
        }

        /// <summary>
        /// A two-pass blur reading the source and then its own intermediate target is the canonical multi-pass shape.
        /// </summary>
        [Fact]
        public void Validate_TwoPassEffect_IsValid() {
            VfxEffectValidator.Validate(TwoPass());
        }

        /// <summary>
        /// Reading a target before any pass wrote it would sample uninitialized memory.
        /// </summary>
        [Fact]
        public void Validate_ReadBeforeWrite_Throws() {
            EffectAsset effect = TwoPass();
            effect.Passes[0].Reads = new[] { "Half" };
            Assert.Throws<InvalidDataException>(() => VfxEffectValidator.Validate(effect));
        }

        /// <summary>
        /// Only the last pass may produce the effect output, otherwise later passes would be wasted or overwrite it.
        /// </summary>
        [Fact]
        public void Validate_OutputNotWrittenLast_Throws() {
            EffectAsset effect = TwoPass();
            effect.Passes[1].Writes = "Half";
            effect.Passes[1].Reads = new[] { "Source" };
            Assert.Throws<InvalidDataException>(() => VfxEffectValidator.Validate(effect));
        }

        /// <summary>
        /// A color occupies four slots, so a scalar placed inside that range would silently overwrite a component.
        /// </summary>
        [Fact]
        public void Validate_OverlappingParameterSlots_Throws() {
            EffectAsset effect = TwoPass();
            effect.Parameters = new[] {
                new EffectParameterAsset { Name = "Tint", Type = EffectParameterType.Color, Minimum = 0, Maximum = 1, Slot = 0 },
                new EffectParameterAsset { Name = "Radius", Type = EffectParameterType.Float, Slot = 2 }
            };
            Assert.Throws<InvalidDataException>(() => VfxEffectValidator.Validate(effect));
        }

        /// <summary>
        /// A shader path that climbs out of its root could compile arbitrary files from the host machine.
        /// </summary>
        [Fact]
        public void Validate_ShaderPathEscapingRoot_Throws() {
            EffectAsset effect = TwoPass();
            effect.Passes[0].ShaderPath = "../outside/Blur.hlsl";
            Assert.Throws<InvalidDataException>(() => VfxEffectValidator.Validate(effect));
        }

        /// <summary>
        /// Builds a minimal valid two-pass effect used as the baseline for every negative case.
        /// </summary>
        /// <returns>Valid two-pass effect.</returns>
        internal static EffectAsset TwoPass() {
            return new EffectAsset {
                EffectId = "test-blur",
                Inputs = new[] { new EffectInputAsset("Source", true) },
                Targets = new[] { new EffectTargetAsset("Half", 0.5f, EffectTargetFormat.Rgba16Float) },
                Passes = new[] {
                    new EffectPassAsset { ShaderPath = "shaders/Blur.hlsl", PixelEntryPoint = "BlurPS", Reads = new[] { "Source" }, Writes = "Half", PassConstants = new float4(1, 0, 0, 0) },
                    new EffectPassAsset { ShaderPath = "shaders/Blur.hlsl", PixelEntryPoint = "BlurPS", Reads = new[] { "Half" }, Writes = EffectAsset.OutputTargetName, PassConstants = new float4(0, 1, 0, 0) }
                },
                Parameters = new[] {
                    new EffectParameterAsset { Name = "Radius", Type = EffectParameterType.Float, DefaultValue = new float4(4, 0, 0, 0), Minimum = 0, Maximum = 64, Slot = 0 }
                }
            };
        }
    }
}
