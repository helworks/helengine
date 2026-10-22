namespace helengine.editor {
    /// <summary>Provides CPU geometry and shader-material authoring without initializing a window or graphics device.</summary>
    public sealed class EditorBuildRenderManager3D : RenderManager3D, IShaderRenderManager3D {
        /// <summary>Live geometry snapshots owned by this session.</summary>
        readonly HashSet<EditorBuildRuntimeModel> Models = new();
        /// <summary>Live CPU materials whose layouts participate in shader metadata refresh.</summary>
        readonly HashSet<ShaderRuntimeMaterial> Materials = new();
        /// <summary>Prevents new authoring resources after disposal.</summary>
        bool IsDisposed;

        /// <summary>Creates an asset-only renderer for one explicit shader package API.</summary>
        /// <param name="shaderCompileTarget">Actual platform shader package target selected by the build host.</param>
        public EditorBuildRenderManager3D(ShaderCompileTarget shaderCompileTarget) {
            if (!Enum.IsDefined(shaderCompileTarget)) {
                throw new ArgumentOutOfRangeException(nameof(shaderCompileTarget));
            }
            ShaderCompileTarget = shaderCompileTarget;
            Render2D = new EditorBuildRenderManager2D();
        }

        /// <summary>Gets the CPU texture authoring renderer owned by this session.</summary>
        public EditorBuildRenderManager2D Render2D { get; }
        /// <summary>Gets the actual target used when resolving shader packages for this build.</summary>
        public ShaderCompileTarget ShaderCompileTarget { get; }

        /// <summary>Copies raw geometry so later authoring and release cannot change caller-owned import data.</summary>
        /// <param name="data">Borrowed raw geometry.</param>
        /// <returns>An owned CPU geometry snapshot with bounds and submesh metadata.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            EditorBuildRuntimeModel model = new(this, data);
            Models.Add(model);
            return model;
        }

        /// <summary>Uses the shared material loader to resolve actual shader packages and texture references.</summary>
        /// <param name="assetContentManager">Authoring content manager.</param>
        /// <param name="materialAssetPath">Raw material asset path.</param>
        /// <returns>Resolved CPU shader material.</returns>
        public override RuntimeMaterial BuildMaterialFromRawAsset(ContentManager assetContentManager, string materialAssetPath) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return ShaderRuntimeMaterialLoader.BuildMaterialFromRawAsset(this, assetContentManager, materialAssetPath);
        }

        /// <summary>Builds reflected bindings, copied render state and authored constant buffers without allocating GPU resources.</summary>
        /// <param name="materialAsset">Borrowed authored material.</param>
        /// <param name="shaderAsset">Borrowed shader package with reflected bindings.</param>
        /// <returns>Owned CPU material preserving the shared shader runtime contract.</returns>
        public RuntimeMaterial BuildMaterialFromRaw(ShaderMaterialAsset materialAsset, ShaderAsset shaderAsset) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(materialAsset);
            ValidateShaderTarget(shaderAsset);
            ShaderRuntimeMaterial material = new();
            try {
                material.SetLayout(MaterialLayoutBuilder.Build(materialAsset, shaderAsset));
                material.SetRenderState(materialAsset.RenderState);
                material.LightingModel = RuntimeMaterialLightingModel.MetalRoughPbr;
                material.SupportsNormalMapping = !string.IsNullOrWhiteSpace(materialAsset.NormalTextureAssetId);
                material.SupportsEmissive = !string.IsNullOrWhiteSpace(materialAsset.EmissiveTextureAssetId);
                material.CastsShadows = materialAsset.CastsShadows;
                material.ReceivesShadows = materialAsset.ReceivesShadows;
                material.ApplyConstantBufferDefaults(materialAsset.ConstantBuffers ?? Array.Empty<MaterialConstantBufferAsset>());
                StandardMaterialTextureBindingDefaults.Apply(material, Render2D);
                Materials.Add(material);
                return material;
            } catch {
                material.Dispose();
                throw;
            }
        }

        /// <summary>Rebuilds matching CPU material layouts from changed shader reflection while preserving compatible property values.</summary>
        /// <param name="shaderAssetId">Shader whose reflected layout changed.</param>
        /// <param name="shaderAsset">Replacement shader package metadata.</param>
        public void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }
            ValidateShaderTarget(shaderAsset);
            foreach (ShaderRuntimeMaterial material in Materials) {
                MaterialLayout layout = material.Layout;
                if (layout == null || !string.Equals(layout.ShaderAssetId, shaderAssetId, StringComparison.Ordinal)) {
                    continue;
                }
                using ShaderMaterialAsset selection = new() {
                    ShaderAssetId = layout.ShaderAssetId, VertexProgram = layout.VertexProgram,
                    PixelProgram = layout.PixelProgram, Variant = layout.Variant, RenderState = material.RenderState.Clone()
                };
                material.SetLayout(MaterialLayoutBuilder.Build(selection, shaderAsset));
            }
        }

        /// <summary>Retires only models materialized by this authoring renderer.</summary>
        /// <param name="model">Owned runtime geometry snapshot.</param>
        public override void ReleaseModel(RuntimeModel model) {
            ArgumentNullException.ThrowIfNull(model);
            if (model is not EditorBuildRuntimeModel ownedModel || !ReferenceEquals(ownedModel.Owner, this)) {
                throw new ArgumentException("Model was not created by this headless authoring renderer.", nameof(model));
            }
            Models.Remove(ownedModel);
            ownedModel.Dispose();
        }

        /// <summary>Removes root materials from refresh tracking and disposes shared generic material instances normally.</summary>
        /// <param name="material">Material owned by the authoring graph.</param>
        public override void ReleaseMaterial(RuntimeMaterial material) {
            if (material is ShaderRuntimeMaterial shaderMaterial) {
                Materials.Remove(shaderMaterial);
            }
            base.ReleaseMaterial(material);
        }

        /// <summary>Reports no GPU rendering or lighting capabilities for this asset materialization host.</summary>
        /// <returns>Conservative asset-only capability profile.</returns>
        public override RendererBackendCapabilityProfile GetCapabilityProfile() => new(false, false, false, false, 0, 0);
        /// <summary>Rejects drawing rather than reporting a successful frame from an asset-only host.</summary>
        public override void Draw() => throw new NotSupportedException("Headless asset authoring cannot draw or present a 3D frame.");
        /// <summary>Rejects GPU render targets in the asset-only host.</summary>
        /// <param name="width">Requested target width.</param>
        /// <param name="height">Requested target height.</param>
        /// <returns>No GPU render target is available in this host.</returns>
        public override RenderTarget CreateRenderTarget(int width, int height) => throw new NotSupportedException("Headless asset authoring cannot create GPU render targets.");

        /// <summary>Retires CPU materials before their textures, then releases geometry and shared window metadata.</summary>
        public override void Dispose() {
            if (IsDisposed) {
                return;
            }
            foreach (ShaderRuntimeMaterial material in Materials) {
                material.Dispose();
            }
            Materials.Clear();
            foreach (EditorBuildRuntimeModel model in Models) {
                model.Dispose();
            }
            Models.Clear();
            Render2D.Dispose();
            base.Dispose();
            IsDisposed = true;
        }

        /// <summary>Rejects shader metadata belonging to a different runtime package API before materialization.</summary>
        /// <param name="shaderAsset">Shader package selected by the authoring resolver.</param>
        void ValidateShaderTarget(ShaderAsset shaderAsset) {
            ArgumentNullException.ThrowIfNull(shaderAsset);
            if (!string.Equals(shaderAsset.TargetName, ShaderTargetNames.GetTargetName(ShaderCompileTarget), StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Headless authoring target '{ShaderCompileTarget}' cannot consume shader package target '{shaderAsset.TargetName}'.");
            }
        }
    }
}
