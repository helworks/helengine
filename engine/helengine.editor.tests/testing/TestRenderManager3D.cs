using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records model-build requests for editor gizmo factory tests.
    /// </summary>
    public class TestRenderManager3D : RenderManager3D, IShaderCompileTargetProvider {
        /// <summary>
        /// Captured raw model assets passed through the build API.
        /// </summary>
        readonly List<ModelAsset> BuiltModelAssetsValue;
        /// <summary>
        /// Captured raw material assets passed through the build API.
        /// </summary>
        readonly List<MaterialAsset> BuiltMaterialAssetsValue;

        /// <summary>
        /// Initializes a new test render manager.
        /// </summary>
        public TestRenderManager3D() {
            BuiltModelAssetsValue = new List<ModelAsset>();
            BuiltMaterialAssetsValue = new List<MaterialAsset>();
        }

        /// <summary>
        /// Gets the raw model assets that were passed to the renderer.
        /// </summary>
        public IReadOnlyList<ModelAsset> BuiltModelAssets => BuiltModelAssetsValue;

        /// <summary>
        /// Gets the raw material assets that were passed to the renderer.
        /// </summary>
        public IReadOnlyList<MaterialAsset> BuiltMaterialAssets => BuiltMaterialAssetsValue;

        /// <summary>
        /// Gets the shader compile target exposed by the test renderer.
        /// </summary>
        public ShaderCompileTarget ShaderCompileTarget => ShaderCompileTarget.Vulkan;

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
        /// Records the supplied model asset and returns a placeholder runtime model.
        /// </summary>
        /// <param name="data">Raw model data to capture.</param>
        /// <returns>Placeholder runtime model for test assertions.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            BuiltModelAssetsValue.Add(data);
            return new TestRuntimeModel();
        }

        /// <summary>
        /// Records the supplied material asset and returns a placeholder runtime material.
        /// </summary>
        /// <param name="materialAsset">Raw material data to capture.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Placeholder runtime material for test assertions.</returns>
        public override RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            BuiltMaterialAssetsValue.Add(materialAsset);
            return new TestRuntimeMaterial();
        }
    }
}
