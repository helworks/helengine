using System.Globalization;

namespace helengine.vfx.effects {
    /// <summary>
    /// Hue-cycles a mask-keyed subject while scaling it from frame center, composited over a solid background.
    /// </summary>
    public class RainbowExpandEffect : IVfxEffect {
        public string Id => "rainbow-expand";
        public string DisplayName => "Rainbow Expand";
        public string ShaderResourcePath => "shaders/effects/RainbowExpand.hlsl";
        public string VertexEntryPoint => "FullscreenVS";
        public string PixelEntryPoint => "RainbowExpandPS";

        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor> {
            new VfxEffectParameterDescriptor("HueCyclesPerClip", VfxParameterType.Float, "1", "Number of full 360-degree hue rotations across the whole clip."),
            new VfxEffectParameterDescriptor("StartScale", VfxParameterType.Float, "1", "Uniform scale factor at the start of the clip."),
            new VfxEffectParameterDescriptor("EndScale", VfxParameterType.Float, "2", "Uniform scale factor at the end of the clip."),
            new VfxEffectParameterDescriptor("Easing", VfxParameterType.Int, "Linear", "One of Linear, EaseIn, EaseOut, EaseInOut."),
            new VfxEffectParameterDescriptor("BackgroundColor", VfxParameterType.Color, "0,0,0", "Solid background color as R,G,B in [0,1].")
        };

        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) {
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            float[] slots = new float[VfxFrameConstants.ParamSlotCount];
            slots[0] = ResolveFloat(parameterValues, "HueCyclesPerClip", "1");
            slots[1] = ResolveFloat(parameterValues, "StartScale", "1");
            slots[2] = ResolveFloat(parameterValues, "EndScale", "2");
            slots[3] = (float)ResolveEasing(parameterValues);

            (float r, float g, float b) = ResolveColor(parameterValues, "BackgroundColor", "0,0,0");
            slots[4] = r;
            slots[5] = g;
            slots[6] = b;

            return slots;
        }

        static float ResolveFloat(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) {
                throw new ArgumentException($"Parameter '{name}' must be a number, got '{text}'.");
            }
            return parsed;
        }

        static VfxEasingKind ResolveEasing(IReadOnlyDictionary<string, string> values) {
            string text = values.TryGetValue("Easing", out string raw) ? raw : "Linear";
            if (!Enum.TryParse(text, ignoreCase: true, out VfxEasingKind kind) || !Enum.IsDefined(typeof(VfxEasingKind), kind)) {
                throw new ArgumentException($"Parameter 'Easing' must be one of Linear, EaseIn, EaseOut, EaseInOut, got '{text}'.");
            }
            return kind;
        }

        static (float, float, float) ResolveColor(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            string[] parts = text.Split(',');
            if (parts.Length != 3
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b)) {
                throw new ArgumentException($"Parameter '{name}' must be three comma-separated numbers R,G,B, got '{text}'.");
            }
            return (r, g, b);
        }
    }
}
