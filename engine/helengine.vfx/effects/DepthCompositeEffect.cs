namespace helengine.vfx.effects {
    /// <summary>
    /// Composites a mask-keyed subject against a 3D render using the render's per-pixel depth: where
    /// the render is nearer than the subject's chosen depth plane it fully occludes the subject, and
    /// where it is farther the subject is composited over it using its own (possibly feathered) alpha.
    /// </summary>
    public class DepthCompositeEffect : IVfxEffect {
        /// <summary>
        /// Identifier callers pass to <c>--effect</c> to select this effect.
        /// </summary>
        public string Id => "depth-composite";

        /// <summary>
        /// Human-readable name shown in CLI help output.
        /// </summary>
        public string DisplayName => "Depth Composite";

        /// <summary>
        /// Location of this effect's HLSL source relative to the application base directory.
        /// </summary>
        public string ShaderResourcePath => "shaders/effects/DepthComposite.hlsl";

        /// <summary>
        /// Vertex entry point; the shared fullscreen-triangle vertex shader pulled in from VfxCommon.hlsli.
        /// </summary>
        public string VertexEntryPoint => "FullscreenVS";

        /// <summary>
        /// Pixel entry point that performs the depth-ordered composite.
        /// </summary>
        public string PixelEntryPoint => "DepthCompositePS";

        /// <summary>
        /// Requires the subject's own RGBA plate, the 3D render's color, and the 3D render's per-pixel
        /// depth, bound to t0, t1, and t2 respectively.
        /// </summary>
        public IReadOnlyList<string> InputRoles { get; } = new List<string> { "Subject", "RenderColor", "RenderDepth" };

        /// <summary>
        /// Only the subject must carry real alpha; the render color and depth are always sampled as
        /// fully opaque scene content.
        /// </summary>
        public IReadOnlyList<string> AlphaRequiredInputRoles { get; } = new List<string> { "Subject" };

        /// <summary>
        /// Parameters this effect accepts, in the order they are documented to users.
        /// </summary>
        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor> {
            new VfxEffectParameterDescriptor(
                "DepthThreshold",
                VfxParameterType.Float,
                "0",
                "Depth value, in the same units as the RenderDepth sequence, that the subject sits at. Render pixels with a depth greater than this are drawn behind the subject; render pixels with a depth less than or equal to this are drawn in front, fully occluding it.")
        };

        /// <summary>
        /// Parses the caller's raw parameter strings into the parameter slot layout DepthComposite.hlsl
        /// reads: slot 0 depth threshold.
        /// </summary>
        /// <param name="parameterValues">Raw name/value pairs; missing entries fall back to the documented defaults.</param>
        /// <returns>Parameter slots sized to <see cref="VfxFrameConstants.ParamSlotCount"/>.</returns>
        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) {
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            float[] slots = new float[VfxFrameConstants.ParamSlotCount];
            slots[0] = VfxEffectParameterParsing.ResolveFloat(parameterValues, "DepthThreshold", "0");
            return slots;
        }
    }
}
