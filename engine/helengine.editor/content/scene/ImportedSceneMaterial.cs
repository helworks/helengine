namespace helengine.editor {
    /// <summary>
    /// Engine-neutral physically based material parameters read from the source, with a base color texture slot that is either empty or "*K", an index into <see cref="ImportedSceneData.Textures"/> (see <see cref="ImportedSceneTextureReference"/>). The normal and roughness slots are not consumed yet and hold the raw source path, unvalidated and not in the texture table.
    /// </summary>
    public sealed class ImportedSceneMaterial {
        /// <summary>
        /// Creates the immutable ImportedSceneMaterial from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="name">The material name.</param>
        /// <param name="baseColor">The base color factor.</param>
        /// <param name="roughness">The roughness factor.</param>
        /// <param name="metallic">The metallic factor.</param>
        /// <param name="baseColorTexturePath">The base color texture slot, empty when absent, otherwise "*K" for texture table entry K.</param>
        /// <param name="normalTexturePath">The raw source path of the normal map texture, empty when absent; not extracted or validated yet.</param>
        /// <param name="roughnessTexturePath">The raw source path of the metallic-roughness texture, empty when absent; not extracted or validated yet.</param>
        public ImportedSceneMaterial(string name, float4 baseColor, float roughness, float metallic, string baseColorTexturePath, string normalTexturePath, string roughnessTexturePath) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            BaseColor = baseColor;
            Roughness = roughness;
            Metallic = metallic;
            BaseColorTexturePath = baseColorTexturePath ?? throw new ArgumentNullException(nameof(baseColorTexturePath));
            NormalTexturePath = normalTexturePath ?? throw new ArgumentNullException(nameof(normalTexturePath));
            RoughnessTexturePath = roughnessTexturePath ?? throw new ArgumentNullException(nameof(roughnessTexturePath));
        }

        /// <summary>
        /// Gets the material name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the base color factor.
        /// </summary>
        public float4 BaseColor { get; }

        /// <summary>
        /// Gets the roughness factor.
        /// </summary>
        public float Roughness { get; }

        /// <summary>
        /// Gets the metallic factor.
        /// </summary>
        public float Metallic { get; }

        /// <summary>
        /// Gets the base color texture slot, empty when absent, otherwise "*K" for texture table entry K.
        /// </summary>
        public string BaseColorTexturePath { get; }

        /// <summary>
        /// Gets the raw source path of the normal map texture, empty when absent; not extracted or validated yet.
        /// </summary>
        public string NormalTexturePath { get; }

        /// <summary>
        /// Gets the raw source path of the metallic-roughness texture, empty when absent; not extracted or validated yet.
        /// </summary>
        public string RoughnessTexturePath { get; }
    }
}
