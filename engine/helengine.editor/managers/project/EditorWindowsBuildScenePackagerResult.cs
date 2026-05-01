namespace helengine.editor {
    /// <summary>
    /// Captures the shader packages referenced while packaging a Windows player build.
    /// </summary>
    public sealed class EditorWindowsBuildScenePackagerResult {
        /// <summary>
        /// Stores the deduplicated shader asset ids referenced by the packaged scenes.
        /// </summary>
        readonly string[] ReferencedShaderAssetIdsValue;

        /// <summary>
        /// Initializes a new scene-packaging result.
        /// </summary>
        /// <param name="referencedShaderAssetIds">Deduplicated shader asset ids referenced by the packaged scenes.</param>
        public EditorWindowsBuildScenePackagerResult(IReadOnlyList<string> referencedShaderAssetIds) {
            if (referencedShaderAssetIds == null) {
                throw new ArgumentNullException(nameof(referencedShaderAssetIds));
            }

            ReferencedShaderAssetIdsValue = referencedShaderAssetIds.ToArray();
        }

        /// <summary>
        /// Gets the deduplicated shader asset ids referenced by the packaged scenes.
        /// </summary>
        public IReadOnlyList<string> ReferencedShaderAssetIds {
            get {
                return ReferencedShaderAssetIdsValue;
            }
        }
    }
}
