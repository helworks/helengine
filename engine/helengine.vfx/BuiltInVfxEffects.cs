namespace helengine.vfx {
    /// <summary>
    /// Defines the effects that ship with the engine. Each is an ordinary <see cref="EffectAsset"/>, exactly like a
    /// project-authored <c>.heffect</c>; their shaders live under <c>shaders/effects</c> in the application directory.
    /// </summary>
    public static class BuiltInVfxEffects {
        /// <summary>
        /// Shared easing names, in the order the shaders' <c>ApplyEasing</c> expects as indices.
        /// </summary>
        static readonly string[] EasingNames = Enum.GetNames<VfxEasingKind>();

        /// <summary>
        /// Returns every built-in effect in a stable order.
        /// </summary>
        /// <returns>Fresh built-in effect definitions.</returns>
        public static EffectAsset[] All() {
            return new[] { RainbowExpand(), RainbowAura(), DepthComposite() };
        }

        /// <summary>
        /// Scales a mask-keyed subject over the clip while cycling its hue, over a solid background.
        /// </summary>
        /// <returns>The rainbow-expand effect definition.</returns>
        public static EffectAsset RainbowExpand() {
            return new EffectAsset {
                EffectId = "rainbow-expand",
                DisplayName = "Rainbow Expand",
                Inputs = new[] { new EffectInputAsset("Source", false), new EffectInputAsset("Mask", true) },
                Passes = new[] { Pass("shaders/effects/RainbowExpand.hlsl", "RainbowExpandPS", "Source", "Mask") },
                Parameters = new[] {
                    Number("HueCyclesPerClip", "Number of full 360-degree hue rotations across the whole clip.", 1, 0),
                    Scale("StartScale", "Uniform scale factor at the start of the clip.", 1, 1),
                    Scale("EndScale", "Uniform scale factor at the end of the clip.", 2, 2),
                    Easing(3),
                    new EffectParameterAsset { Name = "BackgroundColor", Description = "Solid background color as R,G,B in [0,1].", Type = EffectParameterType.Color, DefaultValue = new float4(0, 0, 0, 1), Minimum = 0, Maximum = 1, Slot = 4 }
                }
            };
        }

        /// <summary>
        /// Repeats a mask-keyed subject outward from the frame center, each copy hue-shifted from the last.
        /// </summary>
        /// <returns>The rainbow-aura effect definition.</returns>
        public static EffectAsset RainbowAura() {
            return new EffectAsset {
                EffectId = "rainbow-aura",
                DisplayName = "Rainbow Aura",
                Inputs = new[] { new EffectInputAsset("Source", false), new EffectInputAsset("Mask", true) },
                Passes = new[] { Pass("shaders/effects/RainbowAura.hlsl", "RainbowAuraPS", "Source", "Mask") },
                Parameters = new[] {
                    new EffectParameterAsset { Name = "RepetitionCount", Description = "Number of repeated copies, from 1 to 64.", Type = EffectParameterType.Integer, DefaultValue = new float4(10, 0, 0, 0), Minimum = 1, Maximum = 64, Slot = 0 },
                    Scale("StartScale", "Uniform scale of the innermost (first) repetition.", 1, 1),
                    Scale("ScaleStep", "Additional uniform scale each successive repetition grows to, relative to StartScale.", 0.15f, 2),
                    Number("HueSpreadDegrees", "Total hue rotation spread across all repetitions; 360 gives a full rainbow gradient outward.", 360, 3),
                    new EffectParameterAsset { Name = "GrowWindow", Description = "Fraction of the clip's normalized time each repetition takes to grow and fade in once born.", Type = EffectParameterType.Float, DefaultValue = new float4(0.2f, 0, 0, 0), Minimum = 0.0001f, Maximum = 1, Slot = 4 },
                    Number("HueCyclesPerClip", "Extra full 360-degree hue rotations applied to every repetition together across the whole clip.", 0, 5),
                    Easing(6),
                    new EffectParameterAsset { Name = "SaturationBoost", Description = "Saturation multiplier applied to each repetition's hue-rotated color; 1 leaves it unchanged, 0 is grayscale.", Type = EffectParameterType.Float, DefaultValue = new float4(5, 0, 0, 0), Minimum = 0, Maximum = 1000000, Slot = 7 }
                }
            };
        }

        /// <summary>
        /// Composites a keyed subject into a rendered scene using the scene's depth.
        /// </summary>
        /// <returns>The depth-composite effect definition.</returns>
        public static EffectAsset DepthComposite() {
            return new EffectAsset {
                EffectId = "depth-composite",
                DisplayName = "Depth Composite",
                Inputs = new[] { new EffectInputAsset("Subject", true), new EffectInputAsset("RenderColor", false), new EffectInputAsset("RenderDepth", false) },
                Passes = new[] { Pass("shaders/effects/DepthComposite.hlsl", "DepthCompositePS", "Subject", "RenderColor", "RenderDepth") },
                Parameters = new[] {
                    Number("DepthThreshold", "Depth value, in the same units as the RenderDepth sequence, that the subject sits at. Render pixels with a depth greater than this are drawn behind the subject; render pixels with a depth less than or equal to this are drawn in front, fully occluding it.", 0, 0)
                }
            };
        }

        /// <summary>
        /// Builds the single output pass every built-in effect uses.
        /// </summary>
        /// <param name="shaderPath">Shader path relative to the application directory.</param>
        /// <param name="entryPoint">Pixel shader entry point.</param>
        /// <param name="reads">Input names bound to t0, t1, ...</param>
        /// <returns>Pass writing the effect output.</returns>
        static EffectPassAsset Pass(string shaderPath, string entryPoint, params string[] reads) {
            return new EffectPassAsset { ShaderPath = shaderPath, PixelEntryPoint = entryPoint, Reads = reads, Writes = EffectAsset.OutputTargetName };
        }

        /// <summary>
        /// Builds an unbounded float parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <param name="slot">Constant slot.</param>
        /// <returns>Float parameter declaration.</returns>
        static EffectParameterAsset Number(string name, string description, float defaultValue, int slot) {
            return new EffectParameterAsset { Name = name, Description = description, Type = EffectParameterType.Float, DefaultValue = new float4(defaultValue, 0, 0, 0), Slot = slot };
        }

        /// <summary>
        /// Builds a uniform-scale parameter bounded to the range the compositor has always accepted.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <param name="slot">Constant slot.</param>
        /// <returns>Bounded float parameter declaration.</returns>
        static EffectParameterAsset Scale(string name, string description, float defaultValue, int slot) {
            return new EffectParameterAsset { Name = name, Description = description, Type = EffectParameterType.Float, DefaultValue = new float4(defaultValue, 0, 0, 0), Minimum = 0.01f, Maximum = 16, Slot = slot };
        }

        /// <summary>
        /// Builds the shared easing enum parameter.
        /// </summary>
        /// <param name="slot">Constant slot.</param>
        /// <returns>Easing enum declaration defaulting to Linear.</returns>
        static EffectParameterAsset Easing(int slot) {
            return new EffectParameterAsset { Name = "Easing", Description = "One of Linear, EaseIn, EaseOut, EaseInOut.", Type = EffectParameterType.Enum, AllowedValues = EasingNames, Slot = slot };
        }
    }
}
