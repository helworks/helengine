namespace helengine.editor {
    /// <summary>
    /// Builds and caches built-in runtime materials exposed by the engine generated-asset provider.
    /// </summary>
    public static class EngineGeneratedMaterialCache {
        /// <summary>
        /// Stable generated asset identifier for the built-in standard material.
        /// </summary>
        public const string StandardAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId;

        /// <summary>
        /// Built-in shader source file used by the generated standard material.
        /// </summary>
        const string StandardShaderFileName = "EditorDefaultMesh.hlsl";
        /// <summary>
        /// Logical material asset identifier used when building the generated standard material.
        /// </summary>
        const string StandardMaterialAssetId = "Engine.Materials.Standard.material";
        /// <summary>
        /// Vertex program name used by the generated standard material.
        /// </summary>
        const string StandardVertexProgramName = "EditorDefaultMesh.vs";
        /// <summary>
        /// Pixel program name used by the generated standard material.
        /// </summary>
        const string StandardPixelProgramName = "EditorDefaultMesh.ps";
        /// <summary>
        /// Shader variant used by the generated standard material.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Cached runtime materials keyed by stable generated asset identifier.
        /// </summary>
        static readonly Dictionary<string, RuntimeMaterial> RuntimeMaterials = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);

        /// <summary>
        /// Clears the generated material cache so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeMaterials.Clear();
        }

        /// <summary>
        /// Gets a cached runtime material for one built-in generated asset id, building it on first use.
        /// </summary>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Cached runtime material for the requested generated material.</returns>
        public static RuntimeMaterial GetRuntimeMaterial(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }

            if (RuntimeMaterials.TryGetValue(assetId, out RuntimeMaterial runtimeMaterial)) {
                return runtimeMaterial;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before generated engine materials can be resolved.");
            }
            if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before generated engine materials can be resolved.");
            }

            runtimeMaterial = CreateRuntimeMaterial(assetId, core.RenderManager3D);
            RuntimeMaterials.Add(assetId, runtimeMaterial);
            return runtimeMaterial;
        }

        /// <summary>
        /// Creates the runtime material for one supported built-in generated material.
        /// </summary>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <param name="renderManager3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material instance for the generated material.</returns>
        static RuntimeMaterial CreateRuntimeMaterial(string assetId, RenderManager3D renderManager3D) {
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }

            if (!string.Equals(assetId, StandardAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Generated engine material '{assetId}' is not registered.");
            }

            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(renderManager3D, StandardShaderFileName);
            var materialAsset = new MaterialAsset {
                Id = StandardMaterialAssetId,
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = DefaultVariantName
            };
            return renderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }
    }
}
