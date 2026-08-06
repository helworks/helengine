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
        /// Names of the image sequences this effect requires as input, in the order they are bound to
        /// the shader's texture registers (index 0 to register t0, index 1 to t1, and so on). The
        /// caller supplies one folder per role; <see cref="VfxClip"/> groups the resulting sequences by
        /// these same names.
        /// </summary>
        IReadOnlyList<string> InputRoles { get; }

        /// <summary>
        /// Subset of <see cref="InputRoles"/> whose frames must carry a real alpha channel. A role
        /// listed here that turns out to have no stored alpha is rejected before the GPU run starts,
        /// rather than silently treated as fully opaque.
        /// </summary>
        IReadOnlyList<string> AlphaRequiredInputRoles { get; }

        /// <summary>
        /// Resolves named parameter values (as raw CLI strings) into the fixed
        /// VfxFrameConstants.ParamSlotCount-length float array this effect's shader expects.
        /// </summary>
        /// <param name="parameterValues">Raw name/value pairs supplied by the caller; missing entries fall back to each parameter's documented default.</param>
        /// <returns>Parameter slot values laid out exactly as the effect's HLSL cbuffer expects them.</returns>
        float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues);
    }
}
