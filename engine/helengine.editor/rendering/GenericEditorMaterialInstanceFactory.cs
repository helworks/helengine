namespace helengine.editor {
    /// <summary>
    /// Creates neutral shader-runtime material instances without requiring a concrete graphics backend.
    /// </summary>
    public sealed class GenericEditorMaterialInstanceFactory : IEditorMaterialInstanceFactory {
        /// <summary>
        /// Creates one generic shader material that inherits the source material's resolved values.
        /// </summary>
        /// <param name="sourceMaterial">Material whose authored values should be copied.</param>
        /// <returns>Independent generic shader material instance.</returns>
        public RuntimeMaterial CreateInstance(RuntimeMaterial sourceMaterial) {
            if (sourceMaterial == null) {
                throw new ArgumentNullException(nameof(sourceMaterial));
            }

            ShaderRuntimeMaterial sourceShaderMaterial = ShaderRuntimeMaterialAccess.Require(sourceMaterial);
            ShaderRuntimeMaterial material = new ShaderRuntimeMaterial();
            if (!string.IsNullOrWhiteSpace(sourceMaterial.Id)) {
                material.SetId(sourceMaterial.Id);
            }
            material.SetParentMaterial(sourceShaderMaterial);
            material.LightingModel = sourceMaterial.LightingModel;
            material.SupportsNormalMapping = sourceMaterial.SupportsNormalMapping;
            material.SupportsEmissive = sourceMaterial.SupportsEmissive;
            return material;
        }
    }
}
