using System.IO;

namespace helengine.baseplatform.Requests {

    /// <summary>
    /// Supplies the resolved authored source and stable identity for one shader asset requested by a platform shader cook operation.
    /// </summary>
    public sealed class PlatformShaderArtifactCookSource {
        /// <summary>
        /// Stores the stable shader asset identifier.
        /// </summary>
        readonly string ShaderAssetIdValue;

        /// <summary>
        /// Stores the SHA-256 identity of the authored source bytes.
        /// </summary>
        readonly string SourceHashValue;

        /// <summary>
        /// Stores the complete authored HLSL source text.
        /// </summary>
        readonly string SourceTextValue;

        /// <summary>
        /// Stores the fully qualified host-side path for the authored HLSL source, or null for legacy source-only callers.
        /// </summary>
        readonly string SourcePathValue;

        /// <summary>
        /// Initializes one resolved shader source for a platform shader cook operation.
        /// </summary>
        /// <param name="shaderAssetId">Stable shader asset identifier referenced by material dependencies.</param>
        /// <param name="sourceHash">SHA-256 identity of the authored source bytes.</param>
        /// <param name="sourceText">Complete authored HLSL source text.</param>
        public PlatformShaderArtifactCookSource(string shaderAssetId, string sourceHash, string sourceText) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id is required.", nameof(shaderAssetId));
            } else if (string.IsNullOrWhiteSpace(sourceHash)) {
                throw new ArgumentException("Shader source hash is required.", nameof(sourceHash));
            } else if (sourceText == null) {
                throw new ArgumentNullException(nameof(sourceText));
            }

            ShaderAssetIdValue = shaderAssetId;
            SourceHashValue = sourceHash;
            SourceTextValue = sourceText;
        }

        /// <summary>
        /// Initializes one resolved shader source and its fully qualified host-side source path for a platform shader cook operation.
        /// </summary>
        /// <param name="shaderAssetId">Stable shader asset identifier referenced by material dependencies.</param>
        /// <param name="sourceHash">SHA-256 identity of the authored source bytes.</param>
        /// <param name="sourceText">Complete authored HLSL source text snapshot.</param>
        /// <param name="sourcePath">Fully qualified path for the authored HLSL source.</param>
        public PlatformShaderArtifactCookSource(string shaderAssetId, string sourceHash, string sourceText, string sourcePath)
            : this(shaderAssetId, sourceHash, sourceText) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Shader source path is required.", nameof(sourcePath));
            } else if (!Path.IsPathFullyQualified(sourcePath)) {
                throw new ArgumentException("Shader source path must be fully qualified.", nameof(sourcePath));
            }

            SourcePathValue = Path.GetFullPath(sourcePath);
        }

        /// <summary>
        /// Gets the stable shader asset identifier.
        /// </summary>
        public string ShaderAssetId => ShaderAssetIdValue;

        /// <summary>
        /// Gets the SHA-256 identity of the authored source bytes.
        /// </summary>
        public string SourceHash => SourceHashValue;

        /// <summary>
        /// Gets the complete authored HLSL source text.
        /// </summary>
        public string SourceText => SourceTextValue;

        /// <summary>
        /// Gets the fully qualified authored source path, or null when the legacy source-only constructor was used.
        /// </summary>
        public string SourcePath => SourcePathValue;

        /// <summary>
        /// Gets whether a fully qualified authored source path was supplied.
        /// </summary>
        public bool HasSourcePath => SourcePathValue != null;
    }
}
