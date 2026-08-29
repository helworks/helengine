namespace helengine.editor {
    /// <summary>
    /// Builds and caches built-in runtime materials for one editor session.
    /// </summary>
    public sealed class EngineGeneratedMaterialCache : IDisposable {
        /// <summary>Stable generated asset identifier for the built-in standard material.</summary>
        public const string StandardAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId;

        const string StandardShaderFileName = "ForwardStandardShader.hlsl";
        const string StandardMaterialAssetId = "Engine.Materials.Standard.material";
        const string StandardVertexProgramName = "ForwardStandardShader.vs";
        const string StandardPixelProgramName = "ForwardStandardShader.ps";
        const string DefaultVariantName = "default";

        readonly Core Core;
        readonly RenderManager3D RenderManager3D;
        readonly EditorBuiltInShaderAssetLibrary BuiltInShaderLibrary;
        readonly Dictionary<string, RuntimeMaterial> RuntimeMaterials = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);
        bool IsDisposed;

        /// <summary>Gets the explicit core that owns this cache's runtime materials.</summary>
        internal Core OwningCore => Core;

        /// <summary>Creates a generated-material cache bound to one explicit core and one shader library.</summary>
        public EngineGeneratedMaterialCache(Core core, EditorBuiltInShaderAssetLibrary builtInShaderLibrary) {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            RenderManager3D = core.RenderManager3D ?? throw new InvalidOperationException("The owning core must be initialized with a 3D renderer before creating generated materials.");
            BuiltInShaderLibrary = builtInShaderLibrary ?? throw new ArgumentNullException(nameof(builtInShaderLibrary));
        }

        /// <summary>Gets or creates one generated runtime material owned by this cache.</summary>
        public RuntimeMaterial GetRuntimeMaterial(string assetId) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }
            if (RuntimeMaterials.TryGetValue(assetId, out RuntimeMaterial runtimeMaterial)) {
                return runtimeMaterial;
            }

            runtimeMaterial = CreateRuntimeMaterial(assetId);
            RuntimeMaterials.Add(assetId, runtimeMaterial);
            return runtimeMaterial;
        }

        /// <summary>
        /// Loads one built-in shader through this cache's exact owning renderer
        /// and shader library.
        /// </summary>
        public ShaderAsset LoadBuiltInShaderAsset(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            return BuiltInShaderLibrary.LoadShaderAsset(RenderManager3D, shaderFileName);
        }

        /// <summary>
        /// Loads one built-in shader by its stable asset id through this cache's exact owner graph.
        /// </summary>
        public ShaderAsset LoadBuiltInShaderAssetById(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }
            if (!string.Equals(shaderAssetId, "ForwardStandardShader", StringComparison.Ordinal)) {
                throw new FileNotFoundException($"Built-in shader asset id '{shaderAssetId}' is not registered.");
            }
            return LoadBuiltInShaderAsset(StandardShaderFileName);
        }

        RuntimeMaterial CreateRuntimeMaterial(string assetId) {
            if (!string.Equals(assetId, StandardAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Generated engine material '{assetId}' is not registered.");
            }

            ShaderAsset shaderAsset = BuiltInShaderLibrary.LoadShaderAsset(RenderManager3D, StandardShaderFileName);
            var materialAsset = new ShaderMaterialAsset {
                Id = StandardMaterialAssetId,
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = DefaultVariantName,
                ConstantBuffers = [
                    StandardMaterialBaseColorDefaults.CreateWhiteConstantBufferAsset(),
                    new MaterialConstantBufferAsset {
                        Name = StandardMaterialEmissiveColorDefaults.EmissiveColorBufferName,
                        Data = StandardMaterialEmissiveColorDefaults.CreateDefaultConstantBufferData()
                    }
                ]
            };
            RuntimeMaterial runtimeMaterial = RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            StandardMaterialTextureBindingDefaults.Apply(ShaderRuntimeMaterialAccess.Require(runtimeMaterial), Core.RenderManager2D);
            return runtimeMaterial;
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EngineGeneratedMaterialCache));
            }
        }

        /// <summary>Releases the cached runtime materials owned by this session cache.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            foreach (RuntimeMaterial runtimeMaterial in RuntimeMaterials.Values) {
                runtimeMaterial?.Dispose();
            }
            RuntimeMaterials.Clear();
            IsDisposed = true;
        }
    }
}
