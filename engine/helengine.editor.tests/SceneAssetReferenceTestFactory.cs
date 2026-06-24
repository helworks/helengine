namespace helengine.editor.tests {
    /// <summary>
    /// Creates scene asset references for tests without reopening the production authoring surface.
    /// </summary>
    public static class SceneAssetReferenceTestFactory {
        /// <summary>
        /// Creates one file-backed font scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative font path.</param>
        /// <returns>Validated file-backed font reference.</returns>
        public static SceneAssetReference CreateFileSystemFont(string relativePath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemFont(relativePath);
        }

        /// <summary>
        /// Creates one file-backed texture scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative texture path.</param>
        /// <returns>Validated file-backed texture reference.</returns>
        public static SceneAssetReference CreateFileSystemTexture(string relativePath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemTexture(relativePath);
        }

        /// <summary>
        /// Creates one file-backed model scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative model path.</param>
        /// <returns>Validated file-backed model reference.</returns>
        public static SceneAssetReference CreateFileSystemModel(string relativePath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel(relativePath);
        }

        /// <summary>
        /// Creates one file-backed material scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative material path.</param>
        /// <returns>Validated file-backed material reference.</returns>
        public static SceneAssetReference CreateFileSystemMaterial(string relativePath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemMaterial(relativePath);
        }

        /// <summary>
        /// Creates one file-backed animation clip scene asset reference.
        /// </summary>
        /// <param name="relativePath">Project-relative animation clip path.</param>
        /// <returns>Validated file-backed animation clip reference.</returns>
        public static SceneAssetReference CreateFileSystemAnimationClip(string relativePath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemAnimationClip(relativePath);
        }

        /// <summary>
        /// Creates one editor-generated UI-font scene asset reference.
        /// </summary>
        /// <returns>Validated editor-generated UI-font reference.</returns>
        public static SceneAssetReference CreateEditorUiFont() {
            return EditorSceneAssetReferenceFactory.CreateEditorUiFont();
        }

        /// <summary>
        /// Creates one generated engine cube-model scene asset reference.
        /// </summary>
        /// <returns>Validated generated cube-model reference.</returns>
        public static SceneAssetReference CreateEngineCubeModel() {
            return global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel();
        }

        /// <summary>
        /// Creates one generated engine plane-model scene asset reference.
        /// </summary>
        /// <returns>Validated generated plane-model reference.</returns>
        public static SceneAssetReference CreateEnginePlaneModel() {
            return global::helengine.EngineSceneAssetReferenceFactory.CreatePlaneModel();
        }

        /// <summary>
        /// Creates one generated engine sphere-model scene asset reference.
        /// </summary>
        /// <returns>Validated generated sphere-model reference.</returns>
        public static SceneAssetReference CreateEngineSphereModel() {
            return global::helengine.EngineSceneAssetReferenceFactory.CreateSphereModel();
        }

        /// <summary>
        /// Creates one generated engine standard-material scene asset reference.
        /// </summary>
        /// <returns>Validated generated standard-material reference.</returns>
        public static SceneAssetReference CreateEngineStandardMaterial() {
            return global::helengine.EngineSceneAssetReferenceFactory.CreateStandardMaterial();
        }

        /// <summary>
        /// Creates one scene asset reference by rehydrating the serialized wire format.
        /// </summary>
        /// <param name="sourceKind">Serialized source kind.</param>
        /// <param name="relativePath">Serialized relative path.</param>
        /// <param name="providerId">Serialized provider id.</param>
        /// <param name="assetId">Serialized asset id.</param>
        /// <returns>Rehydrated scene asset reference.</returns>
        public static SceneAssetReference CreateSerialized(
            SceneAssetReferenceSourceKind sourceKind,
            string relativePath,
            string providerId,
            string assetId) {
            using MemoryStream stream = new();
            using BinaryWriterLE writer = new(stream);

            writer.WriteByte(1);
            writer.WriteInt32((int)sourceKind);
            writer.WriteString(relativePath ?? string.Empty);
            writer.WriteString(providerId ?? string.Empty);
            writer.WriteString(assetId ?? string.Empty);

            stream.Position = 0;

            using BinaryReaderLE reader = new(stream);
            return global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
        }
    }
}
