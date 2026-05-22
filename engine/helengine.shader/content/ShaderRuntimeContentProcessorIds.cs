namespace helengine {
    /// <summary>
    /// Defines stable processor identifiers used by shader-runtime content loading paths.
    /// </summary>
    public static class ShaderRuntimeContentProcessorIds {
        /// <summary>
        /// Processor id used for serialized shader assets.
        /// </summary>
        public const string ShaderAsset = "runtime.shader-asset";

        /// <summary>
        /// Processor id used for serialized shader-owned raw material assets.
        /// </summary>
        public const string ShaderMaterialAsset = "runtime.shader-material-asset";
    }
}
