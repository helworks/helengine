namespace helengine {
    /// <summary>
    /// Applies shared shader-material texture defaults so standard lit materials remain sample-safe when authored texture bindings are omitted.
    /// </summary>
    public static class StandardMaterialTextureBindingDefaults {
        /// <summary>
        /// Stable diffuse-texture binding name used by the shared forward standard shader conventions.
        /// </summary>
        public const string DiffuseTextureBindingName = "DiffuseTexture";

        /// <summary>
        /// Applies the default white albedo texture when one shader runtime material exposes the standard diffuse binding but no authored texture is assigned.
        /// </summary>
        /// <param name="material">Shader runtime material whose diffuse binding should be normalized.</param>
        public static void Apply(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            int diffuseTextureBindingIndex = material.Layout.FindTextureBindingIndex(DiffuseTextureBindingName);
            if (diffuseTextureBindingIndex < 0) {
                return;
            }

            RuntimeTexture diffuseTexture = material.Properties.GetTexture(diffuseTextureBindingIndex);
            if (diffuseTexture != null) {
                return;
            }

            Core core = Core.Instance;
            if (core == null || core.RenderManager2D == null) {
                return;
            }

            material.Properties.SetTexture(diffuseTextureBindingIndex, TextureUtils.PixelTexture);
        }
    }
}
