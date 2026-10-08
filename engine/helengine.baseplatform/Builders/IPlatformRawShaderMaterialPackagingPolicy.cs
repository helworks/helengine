namespace helengine.baseplatform.Builders {
    /// <summary>
    /// Explicitly opts a mixed-format platform into preserving authored shader material
    /// payloads while its material cooker continues to translate classic materials.
    /// </summary>
    public interface IPlatformRawShaderMaterialPackagingPolicy {
        /// <summary>
        /// Gets whether shader materials with an authored shader identifier bypass the
        /// platform cooker when its runtime contract resolves raw shader-backed materials.
        /// Platforms without this policy retain their existing material cooking behavior.
        /// </summary>
        bool PreserveAuthoredShaderMaterialPayloads { get; }
    }
}
