namespace helengine.vfx {
    /// <summary>
    /// A VFX effect backed by an HLSL shader compiled through the engine's shader compiler.
    /// </summary>
    public interface IVfxEffect {
        /// <summary>
        /// Stable machine-readable identifier used to select this effect from the CLI and registry.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Human-readable effect name shown in CLI help output.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Parameters this effect accepts, used for CLI help text and for rejecting unknown parameter names.
        /// </summary>
        IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; }

        /// <summary>
        /// Path of the effect's HLSL source relative to the application base directory.
        /// </summary>
        string ShaderResourcePath { get; }

        /// <summary>
        /// Name of the vertex shader entry point to compile out of <see cref="ShaderResourcePath"/>.
        /// </summary>
        string VertexEntryPoint { get; }

        /// <summary>
        /// Name of the pixel shader entry point to compile out of <see cref="ShaderResourcePath"/>.
        /// </summary>
        string PixelEntryPoint { get; }

        /// <summary>
        /// Resolves named parameter values (as raw CLI strings) into the fixed
        /// VfxFrameConstants.ParamSlotCount-length float array this effect's shader expects.
        /// </summary>
        /// <param name="parameterValues">Raw name/value pairs supplied by the caller; missing entries fall back to each parameter's documented default.</param>
        /// <returns>Parameter slot values laid out exactly as the effect's HLSL cbuffer expects them.</returns>
        float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues);
    }
}
