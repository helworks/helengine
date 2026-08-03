using System.Globalization;

namespace helengine.vfx.effects {
    /// <summary>
    /// Hue-cycles a mask-keyed subject while scaling it from frame center, composited over a solid background.
    /// </summary>
    public class RainbowExpandEffect : IVfxEffect {
        /// <summary>
        /// Identifier callers pass to <c>--effect</c> to select this effect.
        /// </summary>
        public string Id => "rainbow-expand";

        /// <summary>
        /// Human-readable name shown in CLI help output.
        /// </summary>
        public string DisplayName => "Rainbow Expand";

        /// <summary>
        /// Location of this effect's HLSL source relative to the application base directory.
        /// </summary>
        public string ShaderResourcePath => "shaders/effects/RainbowExpand.hlsl";

        /// <summary>
        /// Vertex entry point; the shared fullscreen-triangle vertex shader pulled in from VfxCommon.hlsli.
        /// </summary>
        public string VertexEntryPoint => "FullscreenVS";

        /// <summary>
        /// Pixel entry point that performs the hue rotation, scaling, and background compositing.
        /// </summary>
        public string PixelEntryPoint => "RainbowExpandPS";

        /// <summary>
        /// Parameters this effect accepts, in the order they are documented to users.
        /// </summary>
        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor> {
            new VfxEffectParameterDescriptor("HueCyclesPerClip", VfxParameterType.Float, "1", "Number of full 360-degree hue rotations across the whole clip."),
            new VfxEffectParameterDescriptor("StartScale", VfxParameterType.Float, "1", "Uniform scale factor at the start of the clip."),
            new VfxEffectParameterDescriptor("EndScale", VfxParameterType.Float, "2", "Uniform scale factor at the end of the clip."),
            new VfxEffectParameterDescriptor("Easing", VfxParameterType.Int, "Linear", "One of Linear, EaseIn, EaseOut, EaseInOut."),
            new VfxEffectParameterDescriptor("BackgroundColor", VfxParameterType.Color, "0,0,0", "Solid background color as R,G,B in [0,1].")
        };

        /// <summary>
        /// Parses the caller's raw parameter strings into the parameter slot layout RainbowExpand.hlsl reads:
        /// slot 0 hue cycles, slot 1 start scale, slot 2 end scale, slot 3 easing kind, slots 4-6 background RGB.
        /// </summary>
        /// <param name="parameterValues">Raw name/value pairs; missing entries fall back to the documented defaults.</param>
        /// <returns>Parameter slots sized to <see cref="VfxFrameConstants.ParamSlotCount"/>.</returns>
        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) {
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            float[] slots = new float[VfxFrameConstants.ParamSlotCount];
            slots[0] = ResolveFloat(parameterValues, "HueCyclesPerClip", "1");
            slots[1] = ResolveFloat(parameterValues, "StartScale", "1");
            slots[2] = ResolveFloat(parameterValues, "EndScale", "2");
            slots[3] = (float)ResolveEasing(parameterValues);

            ResolveColor(parameterValues, "BackgroundColor", "0,0,0", out float red, out float green, out float blue);
            slots[4] = red;
            slots[5] = green;
            slots[6] = blue;

            return slots;
        }

        /// <summary>
        /// Parses a scalar parameter, falling back to its documented default when the caller omitted it.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <param name="name">Parameter name to read.</param>
        /// <param name="defaultValueText">Textual default used when the parameter is absent.</param>
        /// <returns>The parsed scalar value.</returns>
        static float ResolveFloat(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) {
                throw new ArgumentException($"Parameter '{name}' must be a number, got '{text}'.");
            }
            return parsed;
        }

        /// <summary>
        /// Parses the easing parameter, accepting either an easing name or its numeric value and
        /// rejecting numbers that do not map to a declared easing kind.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <returns>The selected easing curve.</returns>
        static VfxEasingKind ResolveEasing(IReadOnlyDictionary<string, string> values) {
            string text = values.TryGetValue("Easing", out string raw) ? raw : "Linear";
            if (!Enum.TryParse(text, ignoreCase: true, out VfxEasingKind kind) || !Enum.IsDefined(typeof(VfxEasingKind), kind)) {
                throw new ArgumentException($"Parameter 'Easing' must be one of Linear, EaseIn, EaseOut, EaseInOut, got '{text}'.");
            }
            return kind;
        }

        /// <summary>
        /// Parses a comma-separated R,G,B color parameter, falling back to its documented default
        /// when the caller omitted it.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <param name="name">Parameter name to read.</param>
        /// <param name="defaultValueText">Textual default used when the parameter is absent.</param>
        /// <param name="red">Receives the parsed red component.</param>
        /// <param name="green">Receives the parsed green component.</param>
        /// <param name="blue">Receives the parsed blue component.</param>
        static void ResolveColor(
            IReadOnlyDictionary<string, string> values,
            string name,
            string defaultValueText,
            out float red,
            out float green,
            out float blue) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            string[] parts = text.Split(',');
            if (parts.Length != 3
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out red)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out green)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out blue)) {
                throw new ArgumentException($"Parameter '{name}' must be three comma-separated numbers R,G,B, got '{text}'.");
            }
        }
    }
}
