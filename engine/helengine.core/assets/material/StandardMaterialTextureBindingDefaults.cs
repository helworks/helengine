namespace helengine {
    /// <summary>
    /// Applies standard-material texture defaults so shader layouts with albedo sampling remain safe when no authored texture is present.
    /// </summary>
    public static class StandardMaterialTextureBindingDefaults {
        /// <summary>
        /// Stable binding name used by the built-in forward standard shader for albedo sampling.
        /// </summary>
        public const string DiffuseTextureBindingName = "DiffuseTexture";

        /// <summary>
        /// Applies the default white albedo texture when one runtime material exposes the standard diffuse binding but no authored texture is assigned.
        /// </summary>
        /// <param name="material">Runtime material whose standard diffuse binding should be normalized.</param>
        public static void Apply(RuntimeMaterial material) {
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
