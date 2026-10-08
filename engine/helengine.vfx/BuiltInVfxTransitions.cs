namespace helengine.vfx {
    /// <summary>
    /// Defines the scene transitions that ship with the engine. Each is a <see cref="EffectCategory.Transition"/>
    /// <see cref="EffectAsset"/> reading the outgoing scene (<c>From</c>) and the incoming scene (<c>To</c>); the
    /// executor's normalized time is the transition progress. Shaders live in <c>shaders/transitions/Transitions.hlsl</c>
    /// and share one parameter layout: Easing (slot 0), Direction or Shape (1), Amount (2), Hold (3), Color (4-7).
    /// </summary>
    public static class BuiltInVfxTransitions {
        /// <summary>
        /// Shader file holding every built-in transition entry point, relative to the application directory.
        /// </summary>
        const string Shader = "shaders/transitions/Transitions.hlsl";

        /// <summary>
        /// Allowed directions, in the index order the shaders expect.
        /// </summary>
        static readonly string[] Directions = { "Left", "Right", "Up", "Down" };

        /// <summary>
        /// Allowed reveal shapes, in the index order the shaders expect.
        /// </summary>
        static readonly string[] Shapes = { "Circle", "Diamond", "Square" };

        /// <summary>
        /// Returns every built-in transition in a stable order.
        /// </summary>
        /// <returns>Fresh transition definitions.</returns>
        public static EffectAsset[] All() {
            return new[] { Dissolve(), DipToColor(), BlurDissolve(), Push(), Wipe(), Zoom(), Whip(), ShapeReveal(), Flash(), Glitch() };
        }

        /// <summary>
        /// Dissolve with a selectable easing curve.
        /// </summary>
        /// <returns>The dissolve transition.</returns>
        public static EffectAsset Dissolve() {
            return SinglePass("dissolve", "Dissolve", "DissolvePS", Easing());
        }

        /// <summary>
        /// Fades through a solid color, optionally holding it.
        /// </summary>
        /// <returns>The dip-to-color transition.</returns>
        public static EffectAsset DipToColor() {
            return SinglePass("dip-to-color", "Dip to Color", "DipToColorPS", Easing(),
                Number("Hold", "Fraction of the transition spent fully on the color.", 0.1f, 0, 0.8f, 3),
                Color("Color", "Linear RGBA color the scenes pass through.", new float4(0, 0, 0, 1)));
        }

        /// <summary>
        /// Dissolves while blurring both scenes, sharpest at the ends.
        /// </summary>
        /// <returns>The blur-dissolve transition.</returns>
        public static EffectAsset BlurDissolve() {
            EffectAsset effect = Base("blur-dissolve", "Blur Dissolve",
                Easing(),
                Number("Radius", "Strongest blur radius in output pixels, reached mid-transition.", 32, 0, 96, 2));
            effect.Targets = new[] { new EffectTargetAsset("Mix", 1f, EffectTargetFormat.Rgba16Float), new EffectTargetAsset("BlurX", 0.5f, EffectTargetFormat.Rgba16Float) };
            effect.Passes = new[] {
                Pass("BlurMixPS", "Mix", "From", "To"),
                Pass("BlurHorizontalPS", "BlurX", "Mix"),
                Pass("BlurFinalPS", EffectAsset.OutputTargetName, "BlurX", "Mix")
            };
            return effect;
        }

        /// <summary>
        /// Pushes the outgoing scene out while the incoming scene slides in behind it.
        /// </summary>
        /// <returns>The push transition.</returns>
        public static EffectAsset Push() {
            return SinglePass("push", "Push", "PushPS", Easing(), Choice("Direction", "Direction the scenes travel.", Directions, 0, 1));
        }

        /// <summary>
        /// Sweeps a soft-edged line across the frame.
        /// </summary>
        /// <returns>The wipe transition.</returns>
        public static EffectAsset Wipe() {
            return SinglePass("wipe", "Wipe", "WipePS", Easing(),
                Choice("Direction", "Direction the wipe edge travels.", Directions, 1, 1),
                Number("Softness", "Width of the soft edge as a fraction of the frame.", 0.08f, 0, 0.5f, 2));
        }

        /// <summary>
        /// Zooms through the cut.
        /// </summary>
        /// <returns>The zoom transition.</returns>
        public static EffectAsset Zoom() {
            return SinglePass("zoom", "Zoom", "ZoomPS", Easing(), Number("Strength", "Extra zoom applied at the cut.", 0.6f, 0, 3, 2));
        }

        /// <summary>
        /// Fast push with motion blur, like a whip pan.
        /// </summary>
        /// <returns>The whip transition.</returns>
        public static EffectAsset Whip() {
            EffectAsset effect = Base("whip", "Whip Pan",
                Choice("Direction", "Direction of the swing.", Directions, 0, 1),
                Number("Radius", "Strongest motion blur in output pixels, reached mid-swing.", 64, 0, 96, 2));
            effect.Targets = new[] { new EffectTargetAsset("Mix", 1f, EffectTargetFormat.Rgba16Float) };
            effect.Passes = new[] {
                Pass("WhipPushPS", "Mix", "From", "To"),
                Pass("WhipBlurPS", EffectAsset.OutputTargetName, "Mix")
            };
            return effect;
        }

        /// <summary>
        /// Reveals the incoming scene inside a growing shape.
        /// </summary>
        /// <returns>The shape-reveal transition.</returns>
        public static EffectAsset ShapeReveal() {
            return SinglePass("shape-reveal", "Shape Reveal", "ShapeRevealPS", Easing(),
                Choice("Shape", "Shape that grows from the center.", Shapes, 0, 1),
                Number("Softness", "Edge softness as a fraction of the shape size.", 0.05f, 0, 0.5f, 2));
        }

        /// <summary>
        /// Cuts at the midpoint under a burst of light.
        /// </summary>
        /// <returns>The flash transition.</returns>
        public static EffectAsset Flash() {
            return SinglePass("flash", "Flash", "FlashPS", Easing(),
                Number("Intensity", "Brightness of the burst.", 1.5f, 0, 4, 2),
                Color("Color", "Linear RGBA burst color.", new float4(1, 1, 1, 1)));
        }

        /// <summary>
        /// Digital glitch with block displacement and RGB split.
        /// </summary>
        /// <returns>The glitch transition.</returns>
        public static EffectAsset Glitch() {
            return SinglePass("glitch", "Glitch", "GlitchPS", Number("Intensity", "Strength of the displacement and color split.", 0.6f, 0, 1, 2));
        }

        /// <summary>
        /// Builds a transition with its inputs and parameters but no passes yet.
        /// </summary>
        /// <param name="id">Effect id.</param>
        /// <param name="name">Display name.</param>
        /// <param name="parameters">Parameter declarations.</param>
        /// <returns>Transition definition awaiting passes.</returns>
        static EffectAsset Base(string id, string name, params EffectParameterAsset[] parameters) {
            return new EffectAsset {
                EffectId = id,
                DisplayName = name,
                Category = EffectCategory.Transition,
                DowngradeMode = RendererFeatureDowngradeMode.Degrade,
                Inputs = new[] { new EffectInputAsset("From", false), new EffectInputAsset("To", false) },
                Parameters = parameters
            };
        }

        /// <summary>
        /// Builds a transition drawn by one pass reading both scenes.
        /// </summary>
        /// <param name="id">Effect id.</param>
        /// <param name="name">Display name.</param>
        /// <param name="entryPoint">Pixel shader entry point.</param>
        /// <param name="parameters">Parameter declarations.</param>
        /// <returns>Single-pass transition definition.</returns>
        static EffectAsset SinglePass(string id, string name, string entryPoint, params EffectParameterAsset[] parameters) {
            EffectAsset effect = Base(id, name, parameters);
            effect.Passes = new[] { Pass(entryPoint, EffectAsset.OutputTargetName, "From", "To") };
            return effect;
        }

        /// <summary>
        /// Builds one pass of the shared transition shader.
        /// </summary>
        /// <param name="entryPoint">Pixel shader entry point.</param>
        /// <param name="writes">Target written by the pass.</param>
        /// <param name="reads">Inputs or targets bound to t0, t1, ...</param>
        /// <returns>Pass definition.</returns>
        static EffectPassAsset Pass(string entryPoint, string writes, params string[] reads) {
            return new EffectPassAsset { ShaderPath = Shader, PixelEntryPoint = entryPoint, Reads = reads, Writes = writes };
        }

        /// <summary>
        /// Builds the shared easing parameter, defaulting to ease-in-out.
        /// </summary>
        /// <returns>Easing enum declaration in slot 0.</returns>
        static EffectParameterAsset Easing() {
            return Choice("Easing", "Progress curve: Linear, EaseIn, EaseOut or EaseInOut.", Enum.GetNames<VfxEasingKind>(), (int)VfxEasingKind.EaseInOut, 0);
        }

        /// <summary>
        /// Builds an enum parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="values">Allowed names, indexed from zero.</param>
        /// <param name="defaultIndex">Default value index.</param>
        /// <param name="slot">Constant slot.</param>
        /// <returns>Enum declaration.</returns>
        static EffectParameterAsset Choice(string name, string description, string[] values, int defaultIndex, int slot) {
            return new EffectParameterAsset { Name = name, Description = description, Type = EffectParameterType.Enum, AllowedValues = values, DefaultValue = new float4(defaultIndex, 0, 0, 0), Slot = slot };
        }

        /// <summary>
        /// Builds a bounded float parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <param name="minimum">Inclusive minimum.</param>
        /// <param name="maximum">Inclusive maximum.</param>
        /// <param name="slot">Constant slot.</param>
        /// <returns>Float declaration.</returns>
        static EffectParameterAsset Number(string name, string description, float defaultValue, float minimum, float maximum, int slot) {
            return new EffectParameterAsset { Name = name, Description = description, Type = EffectParameterType.Float, DefaultValue = new float4(defaultValue, 0, 0, 0), Minimum = minimum, Maximum = maximum, Slot = slot };
        }

        /// <summary>
        /// Builds a color parameter in slots 4 to 7.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="defaultValue">Default linear RGBA color.</param>
        /// <returns>Color declaration.</returns>
        static EffectParameterAsset Color(string name, string description, float4 defaultValue) {
            return new EffectParameterAsset { Name = name, Description = description, Type = EffectParameterType.Color, DefaultValue = defaultValue, Minimum = 0, Maximum = 1, Slot = 4 };
        }
    }
}
