using System.Runtime.CompilerServices;
using helengine.directx11;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a DirectX11-shaped render-manager test double without constructing the real graphics backend.
    /// </summary>
    internal class TestDirectX11RenderManager3D : DirectX11Renderer3D {
        /// <summary>
        /// Creates one uninitialized DirectX11-shaped renderer for tests that only need backend type identity.
        /// </summary>
        /// <returns>Renderer instance whose overridden build methods return test resources.</returns>
        public static TestDirectX11RenderManager3D Create() {
            return (TestDirectX11RenderManager3D)RuntimeHelpers.GetUninitializedObject(typeof(TestDirectX11RenderManager3D));
        }

        /// <summary>
        /// Returns a placeholder runtime model for tests that build overlay billboard geometry.
        /// </summary>
        /// <param name="data">Raw model data requested by the viewport overlay.</param>
        /// <returns>Placeholder runtime model.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            return new TestRuntimeModel();
        }

        /// <summary>
        /// Returns a placeholder runtime material for tests that build overlay billboard materials.
        /// </summary>
        /// <param name="materialAsset">Material asset requested by the viewport overlay.</param>
        /// <param name="shaderAsset">Shader asset requested by the viewport overlay.</param>
        /// <returns>Placeholder runtime material.</returns>
        public override RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            var material = new TestRuntimeMaterial();
            material.SetLayout(new MaterialLayout(
                materialAsset.ShaderAssetId,
                materialAsset.VertexProgram,
                materialAsset.PixelProgram,
                materialAsset.Variant,
                materialAsset.RenderState ?? new MaterialRenderState(),
                new[] {
                    new MaterialLayoutBinding("LabelTexture", ShaderResourceType.Texture2D, 0, 0, 0)
                },
                Array.Empty<MaterialLayoutBinding>(),
                Array.Empty<MaterialLayoutBinding>()));
            material.LightingModel = RuntimeMaterialLightingModel.MetalRoughPbr;
            material.SupportsNormalMapping = !string.IsNullOrWhiteSpace(materialAsset.NormalTextureAssetId);
            material.SupportsEmissive = !string.IsNullOrWhiteSpace(materialAsset.EmissiveTextureAssetId);
            return material;
        }

        /// <summary>
        /// Suppresses disposal because the renderer instance is intentionally uninitialized.
        /// </summary>
        public override void Dispose() {
        }
    }
}
