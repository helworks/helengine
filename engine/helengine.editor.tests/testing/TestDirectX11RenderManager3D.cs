using System.Runtime.CompilerServices;
using helengine.directx11;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a DirectX11-shaped render-manager test double without constructing the real graphics backend.
    /// </summary>
    internal class TestDirectX11RenderManager3D : DirectX11Renderer3D {
        /// <summary>
        /// Captured raw model assets passed through the build API.
        /// </summary>
        readonly List<ModelAsset> BuiltModelAssetsValue;

        /// <summary>
        /// Creates one uninitialized DirectX11-shaped renderer for tests that only need backend type identity.
        /// </summary>
        /// <returns>Renderer instance whose overridden build methods return test resources.</returns>
        public static TestDirectX11RenderManager3D Create() {
            TestDirectX11RenderManager3D renderer =
                (TestDirectX11RenderManager3D)RuntimeHelpers.GetUninitializedObject(typeof(TestDirectX11RenderManager3D));
            typeof(TestDirectX11RenderManager3D)
                .GetField(nameof(BuiltModelAssetsValue), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(renderer, new List<ModelAsset>());
            return renderer;
        }

        /// <summary>
        /// Gets the raw model assets that were passed to the renderer.
        /// </summary>
        public IReadOnlyList<ModelAsset> BuiltModelAssets => BuiltModelAssetsValue;

        /// <summary>
        /// Returns a placeholder runtime model for tests that build overlay billboard geometry.
        /// </summary>
        /// <param name="data">Raw model data requested by the viewport overlay.</param>
        /// <returns>Placeholder runtime model.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            BuiltModelAssetsValue.Add(data);
            return new TestRuntimeModel();
        }

        /// <summary>
        /// Returns a placeholder runtime material for tests that build overlay billboard materials.
        /// </summary>
        /// <param name="materialAsset">Material asset requested by the viewport overlay.</param>
        /// <param name="shaderAsset">Shader asset requested by the viewport overlay.</param>
        /// <returns>Placeholder runtime material.</returns>
        public override RuntimeMaterial BuildMaterialFromRaw(ShaderMaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            var material = new TestRuntimeMaterial();
            material.SetLayout(MaterialLayoutBuilder.Build(materialAsset, shaderAsset));
            material.LightingModel = RuntimeMaterialLightingModel.MetalRoughPbr;
            material.SupportsNormalMapping = !string.IsNullOrWhiteSpace(materialAsset.NormalTextureAssetId);
            material.SupportsEmissive = !string.IsNullOrWhiteSpace(materialAsset.EmissiveTextureAssetId);
            material.CastsShadows = materialAsset.CastsShadows;
            material.ReceivesShadows = materialAsset.ReceivesShadows;
            StandardMaterialTextureBindingDefaults.Apply(material);
            return material;
        }

        /// <summary>
        /// Creates a lightweight test render target with the requested dimensions.
        /// </summary>
        /// <param name="width">Requested target width.</param>
        /// <param name="height">Requested target height.</param>
        /// <returns>Test render target with matching dimensions.</returns>
        public override RenderTarget CreateRenderTarget(int width, int height) {
            return new TestRenderTarget {
                Width = width,
                Height = height
            };
        }

        /// <summary>
        /// Suppresses disposal because the renderer instance is intentionally uninitialized.
        /// </summary>
        public override void Dispose() {
        }
    }
}
