using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records model-build requests for editor gizmo factory tests.
    /// </summary>
    internal class TestRenderManager3D : RenderManager3D, IShaderRenderManager3D {
        /// <summary>
        /// Captured raw model assets passed through the build API.
        /// </summary>
        readonly List<ModelAsset> BuiltModelAssetsValue;
        /// <summary>
        /// Captured raw material assets passed through the build API.
        /// </summary>
        readonly List<ShaderMaterialAsset> BuiltMaterialAssetsValue;
        /// <summary>
        /// Runtime models released through this test renderer.
        /// </summary>
        readonly List<RuntimeModel> ReleasedModelsValue;
        /// <summary>
        /// Runtime materials released through this test renderer.
        /// </summary>
        readonly List<RuntimeMaterial> ReleasedMaterialsValue;
        /// <summary>
        /// Queued draw-call counts that should be published by subsequent draw calls.
        /// </summary>
        readonly Queue<int> QueuedDrawCallCountsValue;
        /// <summary>
        /// Most recent draw-call count published by the test renderer.
        /// </summary>
        int LastDrawCallCountValue;

        /// <summary>
        /// Gets how many times production code requested one deferred 3D release flush.
        /// </summary>
        public int FlushReleasedAssetsCallCount { get; private set; }

        /// <summary>
        /// Initializes a new test render manager.
        /// </summary>
        public TestRenderManager3D() {
            BuiltModelAssetsValue = new List<ModelAsset>();
            BuiltMaterialAssetsValue = new List<ShaderMaterialAsset>();
            ReleasedModelsValue = new List<RuntimeModel>();
            ReleasedMaterialsValue = new List<RuntimeMaterial>();
            QueuedDrawCallCountsValue = new Queue<int>();
        }

        /// <summary>
        /// Gets the raw model assets that were passed to the renderer.
        /// </summary>
        public IReadOnlyList<ModelAsset> BuiltModelAssets => BuiltModelAssetsValue;

        /// <summary>
        /// Gets the raw material assets that were passed to the renderer.
        /// </summary>
        public IReadOnlyList<ShaderMaterialAsset> BuiltMaterialAssets => BuiltMaterialAssetsValue;

        /// <summary>
        /// Gets the runtime models released through the shared renderer contract.
        /// </summary>
        public IReadOnlyList<RuntimeModel> ReleasedModels => ReleasedModelsValue;

        /// <summary>
        /// Gets the runtime materials released through the shared renderer contract.
        /// </summary>
        public IReadOnlyList<RuntimeMaterial> ReleasedMaterials => ReleasedMaterialsValue;

        /// <summary>
        /// Gets the shader compile target exposed by the test renderer.
        /// </summary>
        public ShaderCompileTarget ShaderCompileTarget => ShaderCompileTarget.Vulkan;

        /// <summary>
        /// Gets the capability profile published by the test renderer.
        /// </summary>
        /// <returns>Capability profile for the lightweight test renderer.</returns>
        public override RendererBackendCapabilityProfile GetCapabilityProfile() {
            return new RendererBackendCapabilityProfile(true, false, false, false, 0, 0);
        }

        /// <summary>
        /// Gets the draw-call count recorded by the most recent completed draw.
        /// </summary>
        public override int LastDrawCallCount => LastDrawCallCountValue;

        /// <summary>
        /// Queues deterministic draw-call counts for subsequent draw invocations.
        /// </summary>
        /// <param name="drawCallCounts">Draw-call counts that should be reported in order.</param>
        public void QueueDrawCallCounts(IEnumerable<int> drawCallCounts) {
            if (drawCallCounts == null) {
                throw new ArgumentNullException(nameof(drawCallCounts));
            }

            foreach (int drawCallCount in drawCallCounts) {
                QueuedDrawCallCountsValue.Enqueue(drawCallCount);
            }
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
        /// Records one deterministic draw-call count for the current frame.
        /// </summary>
        public override void Draw() {
            LastDrawCallCountValue = QueuedDrawCallCountsValue.Count == 0 ? 0 : QueuedDrawCallCountsValue.Dequeue();
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
            TestRuntimeModel model = new TestRuntimeModel();
            model.SetBounds(data.BoundsMin, data.BoundsMax);
            model.SetSubmeshes(ModelSubmeshResolver.BuildRuntimeSubmeshes(data));
            return model;
        }

        /// <summary>
        /// Rebuilds one packaged raw material through the shared shader runtime material loader.
        /// </summary>
        /// <param name="assetContentManager">Content manager that can deserialize companion shader packages.</param>
        /// <param name="contentRootPath">Absolute packaged content root.</param>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <returns>Placeholder runtime material for test assertions.</returns>
        public override RuntimeMaterial BuildMaterialFromRawAsset(
            ContentManager assetContentManager,
            string contentRootPath,
            string materialAssetPath) {
            return ShaderRuntimeMaterialLoader.BuildMaterialFromRawAsset(this, assetContentManager, contentRootPath, materialAssetPath);
        }

        /// <summary>
        /// Records the supplied material asset and returns a placeholder runtime material.
        /// </summary>
        /// <param name="materialAsset">Raw material data to capture.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Placeholder runtime material for test assertions.</returns>
        public RuntimeMaterial BuildMaterialFromRaw(ShaderMaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            BuiltMaterialAssetsValue.Add(materialAsset);
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
        /// Ignores shader invalidation because the test renderer does not cache backend shader resources.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset payload.</param>
        public void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }
        }

        /// <summary>
        /// Records one runtime model release request so scene unload tests can assert the shared contract.
        /// </summary>
        /// <param name="model">Runtime model released by production code.</param>
        public override void ReleaseModel(RuntimeModel model) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }

            ReleasedModelsValue.Add(model);
            base.ReleaseModel(model);
        }

        /// <summary>
        /// Records one runtime material release request so scene unload tests can assert the shared contract.
        /// </summary>
        /// <param name="material">Runtime material released by production code.</param>
        public override void ReleaseMaterial(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            ReleasedMaterialsValue.Add(material);
            base.ReleaseMaterial(material);
        }

        /// <summary>
        /// Records one deferred-release flush request so tests can assert frame-boundary ownership handoff.
        /// </summary>
        public override void FlushReleasedAssets() {
            FlushReleasedAssetsCallCount++;
        }
    }
}
