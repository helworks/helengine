namespace helengine.vfx.effects {
    /// <summary>
    /// Additively repeats a mask-keyed subject outward from frame center, each repetition growing in
    /// over the clip and offset in hue from the last, over a transparent background.
    /// </summary>
    public class RainbowAuraEffect : IVfxEffect {
        /// <summary>
        /// Smallest number of repetitions the shader's dynamic loop is allowed to run.
        /// </summary>
        const int MinRepetitionCount = 1;

        /// <summary>
        /// Largest number of repetitions the shader's dynamic loop is allowed to run, chosen to keep
        /// per-frame export time reasonable; nothing about the shader itself requires this cap.
        /// </summary>
        const int MaxRepetitionCount = 64;

        /// <summary>
        /// Identifier callers pass to <c>--effect</c> to select this effect.
        /// </summary>
        public string Id => "rainbow-aura";

        /// <summary>
        /// Human-readable name shown in CLI help output.
        /// </summary>
        public string DisplayName => "Rainbow Aura";

        /// <summary>
        /// Location of this effect's HLSL source relative to the application base directory.
        /// </summary>
        public string ShaderResourcePath => "shaders/effects/RainbowAura.hlsl";

        /// <summary>
        /// Vertex entry point; the shared fullscreen-triangle vertex shader pulled in from VfxCommon.hlsli.
        /// </summary>
        public string VertexEntryPoint => "FullscreenVS";

        /// <summary>
        /// Pixel entry point that accumulates the repeated, hue-shifted copies.
        /// </summary>
        public string PixelEntryPoint => "RainbowAuraPS";

        /// <summary>
        /// Requires the subject's color plate and its matte, bound to t0 and t1 respectively.
        /// </summary>
        public IReadOnlyList<string> InputRoles { get; } = new List<string> { "Source", "Mask" };

        /// <summary>
        /// The matte must carry real alpha; the color plate does not need one since it is only ever
        /// sampled for RGB.
        /// </summary>
        public IReadOnlyList<string> AlphaRequiredInputRoles { get; } = new List<string> { "Mask" };

        /// <summary>
        /// Parameters this effect accepts, in the order they are documented to users.
        /// </summary>
        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor> {
            new VfxEffectParameterDescriptor("RepetitionCount", VfxParameterType.Int, "10", "Number of repeated copies, from 1 to 64."),
            new VfxEffectParameterDescriptor("StartScale", VfxParameterType.Float, "1", "Uniform scale of the innermost (first) repetition."),
            new VfxEffectParameterDescriptor("ScaleStep", VfxParameterType.Float, "0.15", "Additional uniform scale each successive repetition grows to, relative to StartScale."),
            new VfxEffectParameterDescriptor("HueSpreadDegrees", VfxParameterType.Float, "360", "Total hue rotation spread across all repetitions; 360 gives a full rainbow gradient outward."),
            new VfxEffectParameterDescriptor("GrowWindow", VfxParameterType.Float, "0.2", "Fraction of the clip's normalized time each repetition takes to grow and fade in once born."),
            new VfxEffectParameterDescriptor("HueCyclesPerClip", VfxParameterType.Float, "0", "Extra full 360-degree hue rotations applied to every repetition together across the whole clip."),
            new VfxEffectParameterDescriptor("Easing", VfxParameterType.Int, "Linear", "One of Linear, EaseIn, EaseOut, EaseInOut."),
            new VfxEffectParameterDescriptor("SaturationBoost", VfxParameterType.Float, "5", "Saturation multiplier applied to each repetition's hue-rotated color; 1 leaves it unchanged, 0 is grayscale.")
        };

        /// <summary>
        /// Parses the caller's raw parameter strings into the parameter slot layout RainbowAura.hlsl reads:
        /// slot 0 repetition count, slot 1 start scale, slot 2 scale step, slot 3 hue spread degrees,
        /// slot 4 grow window, slot 5 hue cycles per clip, slot 6 easing kind, slot 7 saturation boost.
        /// </summary>
        /// <param name="parameterValues">Raw name/value pairs; missing entries fall back to the documented defaults.</param>
        /// <returns>Parameter slots sized to <see cref="VfxFrameConstants.ParamSlotCount"/>.</returns>
        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) {
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            float[] slots = new float[VfxFrameConstants.ParamSlotCount];
            slots[0] = ResolveRepetitionCount(parameterValues);
            slots[1] = VfxEffectParameterParsing.ResolveFloat(parameterValues, "StartScale", "1");
            slots[2] = VfxEffectParameterParsing.ResolveFloat(parameterValues, "ScaleStep", "0.15");
            slots[3] = VfxEffectParameterParsing.ResolveFloat(parameterValues, "HueSpreadDegrees", "360");
            slots[4] = ResolveGrowWindow(parameterValues);
            slots[5] = VfxEffectParameterParsing.ResolveFloat(parameterValues, "HueCyclesPerClip", "0");
            slots[6] = (float)VfxEffectParameterParsing.ResolveEasing(parameterValues, "Easing", "Linear");
            slots[7] = ResolveSaturationBoost(parameterValues);

            return slots;
        }

        /// <summary>
        /// Parses RepetitionCount, rejecting non-whole numbers and values outside the shader's supported
        /// loop range instead of letting the shader run a nonsensical or pathologically long loop.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <returns>The validated repetition count.</returns>
        static float ResolveRepetitionCount(IReadOnlyDictionary<string, string> values) {
            float raw = VfxEffectParameterParsing.ResolveFloat(values, "RepetitionCount", "10");
            if (raw != Math.Floor(raw) || raw < MinRepetitionCount || raw > MaxRepetitionCount) {
                throw new ArgumentException(
                    $"Parameter 'RepetitionCount' must be a whole number from {MinRepetitionCount} to {MaxRepetitionCount}, got '{raw}'.");
            }
            return raw;
        }

        /// <summary>
        /// Parses GrowWindow, rejecting values outside (0, 1] since the shader divides by it and a
        /// non-positive window would divide by zero or never finish growing.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <returns>The validated grow window.</returns>
        static float ResolveGrowWindow(IReadOnlyDictionary<string, string> values) {
            float raw = VfxEffectParameterParsing.ResolveFloat(values, "GrowWindow", "0.2");
            if (raw <= 0f || raw > 1f) {
                throw new ArgumentException($"Parameter 'GrowWindow' must be greater than 0 and at most 1, got '{raw}'.");
            }
            return raw;
        }

        /// <summary>
        /// Parses SaturationBoost, rejecting negative values since a negative multiplier would invert
        /// the color around its luma rather than desaturate it.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <returns>The validated saturation boost.</returns>
        static float ResolveSaturationBoost(IReadOnlyDictionary<string, string> values) {
            float raw = VfxEffectParameterParsing.ResolveFloat(values, "SaturationBoost", "5");
            if (raw < 0f) {
                throw new ArgumentException($"Parameter 'SaturationBoost' must be zero or greater, got '{raw}'.");
            }
            return raw;
        }
    }
}
