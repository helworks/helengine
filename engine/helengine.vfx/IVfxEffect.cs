namespace helengine.vfx {
    /// <summary>
    /// A VFX effect backed by an HLSL shader compiled through the engine's shader compiler.
    /// </summary>
    public interface IVfxEffect {
        string Id { get; }
        string DisplayName { get; }
        IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; }
        string ShaderResourcePath { get; }
        string VertexEntryPoint { get; }
        string PixelEntryPoint { get; }

        /// <summary>
        /// Resolves named parameter values (as raw CLI strings) into the fixed
        /// VfxFrameConstants.ParamSlotCount-length float array this effect's shader expects.
        /// </summary>
        float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues);
    }
}
