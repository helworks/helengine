namespace helengine.editor {
    /// <summary>
    /// Represents one authored or built-in shader source resolved from the shader asset identifier stored by materials.
    /// </summary>
    public sealed class EditorProjectShaderSource {
        /// <summary>
        /// Stores the stable shader asset identifier.
        /// </summary>
        readonly string ShaderAssetIdValue;

        /// <summary>
        /// Stores the absolute path to the resolved HLSL source file.
        /// </summary>
        readonly string SourcePathValue;

        /// <summary>
        /// Stores the authored HLSL source text.
        /// </summary>
        readonly string SourceTextValue;

        /// <summary>
        /// Stores the SHA-256 identity of the authored source bytes.
        /// </summary>
        readonly string SourceHashValue;

        /// <summary>
        /// Initializes one resolved shader source entry.
        /// </summary>
        /// <param name="shaderAssetId">Stable shader asset identifier persisted by materials.</param>
        /// <param name="sourcePath">Absolute HLSL source path.</param>
        /// <param name="sourceText">Complete HLSL source text.</param>
        /// <param name="sourceHash">Uppercase SHA-256 hash of the source bytes.</param>
        public EditorProjectShaderSource(string shaderAssetId, string sourcePath, string sourceText, string sourceHash) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id is required.", nameof(shaderAssetId));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Shader source path is required.", nameof(sourcePath));
            } else if (sourceText == null) {
                throw new ArgumentNullException(nameof(sourceText));
            } else if (string.IsNullOrWhiteSpace(sourceHash)) {
                throw new ArgumentException("Shader source hash is required.", nameof(sourceHash));
            }

            ShaderAssetIdValue = shaderAssetId;
            SourcePathValue = Path.GetFullPath(sourcePath);
            SourceTextValue = sourceText;
            SourceHashValue = sourceHash;
        }

        /// <summary>
        /// Gets the stable shader asset identifier.
        /// </summary>
        public string ShaderAssetId => ShaderAssetIdValue;

        /// <summary>
        /// Gets the absolute HLSL source path.
        /// </summary>
        public string SourcePath => SourcePathValue;

        /// <summary>
        /// Gets the authored HLSL source text.
        /// </summary>
        public string SourceText => SourceTextValue;

        /// <summary>
        /// Gets the uppercase SHA-256 hash of the source bytes.
        /// </summary>
        public string SourceHash => SourceHashValue;
    }
}
