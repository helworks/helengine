namespace helengine.editor {
    /// <summary>
    /// Describes one generated material asset emitted while importing a model source.
    /// </summary>
    public sealed class ImportedModelMaterialAsset {
        /// <summary>
        /// Initializes one generated material asset description.
        /// </summary>
        /// <param name="materialName">Source material name resolved from the imported model.</param>
        /// <param name="relativeMaterialPath">Relative path where the generated material should be written.</param>
        /// <param name="materialAsset">Material asset payload to serialize.</param>
        public ImportedModelMaterialAsset(string materialName, string relativeMaterialPath, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(materialName)) {
                throw new ArgumentException("Material name must be provided.", nameof(materialName));
            } else if (string.IsNullOrWhiteSpace(relativeMaterialPath)) {
                throw new ArgumentException("Relative material path must be provided.", nameof(relativeMaterialPath));
            }

            MaterialName = materialName;
            RelativeMaterialPath = relativeMaterialPath;
            MaterialAsset = materialAsset ?? throw new ArgumentNullException(nameof(materialAsset));
        }

        /// <summary>
        /// Gets the source material name resolved from the imported model.
        /// </summary>
        public string MaterialName { get; }

        /// <summary>
        /// Gets the relative path where the generated material should be written.
        /// </summary>
        public string RelativeMaterialPath { get; }

        /// <summary>
        /// Gets the material asset payload to serialize.
        /// </summary>
        public MaterialAsset MaterialAsset { get; }
    }
}
