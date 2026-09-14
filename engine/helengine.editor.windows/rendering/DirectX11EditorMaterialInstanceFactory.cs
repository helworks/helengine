using helengine.directx11;

namespace helengine.editor.windows {
    /// <summary>
    /// Creates DirectX11 material instances for editor visuals while keeping the construction outside shared editor code.
    /// </summary>
    public sealed class DirectX11EditorMaterialInstanceFactory : helengine.editor.IEditorMaterialInstanceFactory {
        /// <summary>
        /// Creates one DirectX11 material instance by copying the backend-owned shader resource and shared material state.
        /// </summary>
        /// <param name="sourceMaterial">Generated DirectX11 material to clone.</param>
        /// <returns>Independent DirectX11 material instance.</returns>
        public RuntimeMaterial CreateInstance(RuntimeMaterial sourceMaterial) {
            if (sourceMaterial == null) {
                throw new ArgumentNullException(nameof(sourceMaterial));
            }
            if (sourceMaterial is not DirectX11MaterialResource) {
                throw new InvalidOperationException($"DirectX11 material cloning requires '{nameof(DirectX11MaterialResource)}'.");
            }

            DirectX11MaterialResource directX11Material = (DirectX11MaterialResource)sourceMaterial;
            ShaderRuntimeMaterial sourceShaderMaterial = ShaderRuntimeMaterialAccess.Require(sourceMaterial);
            DirectX11MaterialResource material = new DirectX11MaterialResource(
                directX11Material.ShaderResource,
                directX11Material.ShaderAssetId,
                directX11Material.VertexProgram,
                directX11Material.PixelProgram,
                directX11Material.Variant);
            material.SetId(sourceMaterial.Id);
            material.SetLayout(sourceShaderMaterial.Layout);
            material.SetRenderState(sourceMaterial.RenderState);
            material.Properties.CopyMatchingValuesFrom(sourceShaderMaterial.Properties);
            material.LightingModel = sourceMaterial.LightingModel;
            material.SupportsNormalMapping = sourceMaterial.SupportsNormalMapping;
            material.SupportsEmissive = sourceMaterial.SupportsEmissive;
            return material;
        }
    }
}