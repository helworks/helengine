namespace helengine.editor {
    /// <summary>
    /// Represents one generated texture payload emitted by build-time text-to-sprite conversion.
    /// </summary>
    public sealed class TextComponentSpriteBakeResult {
        /// <summary>
        /// Initializes one generated text-sprite bake result.
        /// </summary>
        /// <param name="textureAsset">Generated texture asset that should back the replacement sprite.</param>
        /// <param name="processorSettings">Processor settings that should be applied when cooking the generated texture.</param>
        /// <param name="stableKey">Stable identifier used to derive deterministic generated output paths.</param>
        public TextComponentSpriteBakeResult(TextureAsset textureAsset, TextureAssetProcessorSettings processorSettings, string stableKey) {
            TextureAsset = textureAsset ?? throw new ArgumentNullException(nameof(textureAsset));
            ProcessorSettings = processorSettings ?? throw new ArgumentNullException(nameof(processorSettings));
            StableKey = string.IsNullOrWhiteSpace(stableKey)
                ? throw new ArgumentException("Stable key must be provided.", nameof(stableKey))
                : stableKey;
        }

        /// <summary>
        /// Gets the generated texture asset that should back the replacement sprite.
        /// </summary>
        public TextureAsset TextureAsset { get; }

        /// <summary>
        /// Gets the texture processor settings that should be applied when cooking the generated texture.
        /// </summary>
        public TextureAssetProcessorSettings ProcessorSettings { get; }

        /// <summary>
        /// Gets the stable identifier used to derive deterministic generated output paths.
        /// </summary>
        public string StableKey { get; }
    }
}
