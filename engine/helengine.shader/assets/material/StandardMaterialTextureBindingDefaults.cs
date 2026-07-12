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
        /// Stable emissive-texture binding name used by the shared forward standard shader conventions.
        /// </summary>
        public const string EmissiveTextureBindingName = "EmissiveTexture";

        /// <summary>
        /// Stable roughness-texture binding name used by the shared forward standard shader conventions.
        /// </summary>
        public const string RoughnessTextureBindingName = "RoughnessTexture";

        /// <summary>
        /// Applies the default white albedo texture when one shader runtime material exposes the standard diffuse binding but no authored texture is assigned.
        /// </summary>
        /// <param name="material">Shader runtime material whose diffuse binding should be normalized.</param>
        public static void Apply(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            Core core = Core.Instance;
            if (core == null || core.RenderManager2D == null) {
                return;
            }

            ApplyDefaultTexture(material, DiffuseTextureBindingName);
            ApplyDefaultTexture(material, EmissiveTextureBindingName, TextureUtils.BlackPixelTexture);
            ApplyDefaultTexture(material, RoughnessTextureBindingName);
        }

        /// <summary>
        /// Applies the shared white pixel texture to one standard-material texture binding when it is present but currently unassigned.
        /// </summary>
        /// <param name="material">Shader runtime material whose binding should be normalized.</param>
        /// <param name="bindingName">Standard-material texture binding name to normalize.</param>
        static void ApplyDefaultTexture(ShaderRuntimeMaterial material, string bindingName, RuntimeTexture fallbackTexture = null) {
            int bindingIndex = material.Layout.FindTextureBindingIndex(bindingName);
            if (bindingIndex < 0) {
                return;
            }

            RuntimeTexture texture = material.Properties.GetTexture(bindingIndex);
            if (texture != null) {
                return;
            }

            material.Properties.SetTexture(bindingIndex, fallbackTexture ?? TextureUtils.PixelTexture);
        }
    }
}
