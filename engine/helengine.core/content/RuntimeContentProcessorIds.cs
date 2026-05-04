namespace helengine {
    /// <summary>
    /// Defines stable processor identifiers used by runtime scene-loading paths.
    /// </summary>
    public static class RuntimeContentProcessorIds {
        /// <summary>
        /// Processor id used for serialized material assets.
        /// </summary>
        public const string MaterialAsset = "runtime.material-asset";

        /// <summary>
        /// Processor id used for serialized model assets.
        /// </summary>
        public const string ModelAsset = "runtime.model-asset";

        /// <summary>
        /// Processor id used for serialized texture assets.
        /// </summary>
        public const string TextureAsset = "runtime.texture-asset";

        /// <summary>
        /// Processor id used for serialized text assets.
        /// </summary>
        public const string TextAsset = "runtime.text-asset";

        /// <summary>
        /// Processor id used for serialized shader assets.
        /// </summary>
        public const string ShaderAsset = "runtime.shader-asset";

        /// <summary>
        /// Processor id used for serialized scene assets.
        /// </summary>
        public const string SceneAsset = "runtime.scene-asset";

        /// <summary>
        /// Processor id used for packaged font assets.
        /// </summary>
        public const string FontAsset = "runtime.font-asset";
    }
}
