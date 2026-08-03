using helengine.vfx;

namespace helengine.vfx.tests {
    /// <summary>
    /// Minimal IVfxEffect stand-in used to exercise registry lookup without pulling in a real shader
    /// or a GPU device.
    /// </summary>
    class FakeEffect : IVfxEffect {
        /// <summary>
        /// Registry id this fake registers itself under.
        /// </summary>
        public string Id => "fake-effect";

        /// <summary>
        /// Display name reported for help output.
        /// </summary>
        public string DisplayName => "Fake Effect";

        /// <summary>
        /// No parameters; the fake exists only to be resolved by id.
        /// </summary>
        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor>();

        /// <summary>
        /// Placeholder shader path; never compiled because no test runs this effect on a device.
        /// </summary>
        public string ShaderResourcePath => "shaders/fake.hlsl";

        /// <summary>
        /// Placeholder vertex entry point name.
        /// </summary>
        public string VertexEntryPoint => "FullscreenVS";

        /// <summary>
        /// Placeholder pixel entry point name.
        /// </summary>
        public string PixelEntryPoint => "FakePS";

        /// <summary>
        /// Returns an all-zero slot bank of the required length.
        /// </summary>
        /// <param name="parameterValues">Ignored; the fake declares no parameters.</param>
        /// <returns>Zeroed parameter slots.</returns>
        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) => new float[VfxFrameConstants.ParamSlotCount];
    }
}
