namespace helengine.editor {
    /// <summary>
    /// Infers stable scene asset references from live runtime component assignments during scene save.
    /// </summary>
    public class SceneAssetReferenceInferenceService {
        /// <summary>
        /// Stable save-state slot name used for mesh model references.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Stable save-state slot name used for mesh material references.
        /// </summary>
        const string MeshMaterialReferenceName = "Material";

        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project assets root.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Service used to inspect authored material settings documents.
        /// </summary>
        readonly MaterialAssetSettingsService MaterialAssetSettingsService;

        /// <summary>
        /// Cached authored model source paths keyed by their stable imported model asset id.
        /// </summary>
        Dictionary<string, string> ModelRelativePathsByAssetId;

        /// <summary>
        /// Cached authored material paths keyed by their stable material asset id.
        /// </summary>
        Dictionary<string, string> MaterialRelativePathsByAssetId;

        /// <summary>
        /// Initializes a new save-time scene asset reference inference service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        public SceneAssetReferenceInferenceService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            MaterialAssetSettingsService = new MaterialAssetSettingsService();
        }

        /// <summary>
        /// Populates any missing component asset references that can be inferred from the current runtime assignments.
        /// </summary>
        /// <param name="component">Live component being prepared for serialization.</param>
        /// <param name="saveState">Save-state container that should receive inferred references.</param>
        public void PopulateAssetReferences(Component component, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            }

            if (component is MeshComponent meshComponent) {
                PopulateMeshAssetReferences(meshComponent, saveState);
                return;
            }

            if (component is FPSComponent fpsComponent) {
                PopulateFpsAssetReferences(fpsComponent, saveState);
            }
        }

        /// <summary>
        /// Populates any missing mesh model and material references from the current runtime assignments.
        /// </summary>
        /// <param name="meshComponent">Mesh component being prepared for serialization.</param>
        /// <param name="saveState">Save-state container that should receive inferred references.</param>
        void PopulateMeshAssetReferences(MeshComponent meshComponent, EntityComponentSaveState saveState) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            }

            EnsureMeshModelReference(meshComponent.Model, saveState);

            RuntimeMaterial[] runtimeMaterials = meshComponent.Materials;
            for (int materialIndex = 0; materialIndex < runtimeMaterials.Length; materialIndex++) {
                EnsureMeshMaterialReference(runtimeMaterials[materialIndex], materialIndex, saveState);
            }
        }

        /// <summary>
        /// Populates any missing FPS font reference from the current runtime assignment.
        /// </summary>
        /// <param name="fpsComponent">FPS component being prepared for serialization.</param>
        /// <param name="saveState">Save-state container that should receive inferred references.</param>
        void PopulateFpsAssetReferences(FPSComponent fpsComponent, EntityComponentSaveState saveState) {
            if (fpsComponent == null) {
                throw new ArgumentNullException(nameof(fpsComponent));
            }
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            }
            if (saveState.TryGetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, out _)) {
                return;
            }
            if (fpsComponent.Font == null) {
                return;
            }
            if (Core.Instance != null && Core.Instance.DefaultFontAsset != null && ReferenceEquals(fpsComponent.Font, Core.Instance.DefaultFontAsset)) {
                saveState.SetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, FontAssetScenePersistenceSupport.BuildEditorFontReference());
                return;
            }

            throw new InvalidOperationException("FPSComponent Font is assigned but could not be inferred into a stable scene asset reference.");
        }

        /// <summary>
        /// Ensures the mesh model reference slot is populated when the model can be inferred.
        /// </summary>
        /// <param name="runtimeModel">Runtime model currently assigned to the mesh component.</param>
        /// <param name="saveState">Save-state container that should receive the inferred reference.</param>
        void EnsureMeshModelReference(RuntimeModel runtimeModel, EntityComponentSaveState saveState) {
            if (runtimeModel == null) {
                return;
            }
            if (saveState.TryGetAssetReference(MeshModelReferenceName, out _)) {
                return;
            }

            if (TryInferGeneratedModelReference(runtimeModel, out SceneAssetReference modelReference)) {
                saveState.SetAssetReference(MeshModelReferenceName, modelReference);
                return;
            }
            if (TryInferFileSystemModelReference(runtimeModel, out SceneAssetReference fileSystemModelReference)) {
                saveState.SetAssetReference(MeshModelReferenceName, fileSystemModelReference);
                return;
            }

            throw new InvalidOperationException("MeshComponent Model is assigned but could not be inferred into a stable scene asset reference.");
        }

        /// <summary>
        /// Ensures one mesh material reference slot is populated when the material can be inferred.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material currently assigned to the mesh slot.</param>
        /// <param name="slotIndex">Zero-based material slot index.</param>
        /// <param name="saveState">Save-state container that should receive the inferred reference.</param>
        void EnsureMeshMaterialReference(RuntimeMaterial runtimeMaterial, int slotIndex, EntityComponentSaveState saveState) {
            if (runtimeMaterial == null) {
                return;
            }
            if (slotIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Material slot index must be non-negative.");
            }
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            }

            string referenceName = BuildMaterialReferenceName(slotIndex);
            if (saveState.TryGetAssetReference(referenceName, out _)) {
                return;
            }

            if (TryInferGeneratedMaterialReference(runtimeMaterial, out SceneAssetReference generatedMaterialReference)) {
                saveState.SetAssetReference(referenceName, generatedMaterialReference);
                return;
            }
            if (TryInferFileSystemMaterialReference(runtimeMaterial, out SceneAssetReference fileSystemMaterialReference)) {
                saveState.SetAssetReference(referenceName, fileSystemMaterialReference);
                return;
            }

            throw new InvalidOperationException($"MeshComponent {referenceName} is assigned but could not be inferred into a stable scene asset reference.");
        }

        /// <summary>
        /// Attempts to infer one generated model reference from the supplied runtime model.
        /// </summary>
        /// <param name="runtimeModel">Runtime model to inspect.</param>
        /// <param name="reference">Inferred generated model reference when one could be resolved.</param>
        /// <returns>True when the runtime model matches a generated engine primitive.</returns>
        bool TryInferGeneratedModelReference(RuntimeModel runtimeModel, out SceneAssetReference reference) {
            if (runtimeModel == null) {
                throw new ArgumentNullException(nameof(runtimeModel));
            }

            if (ReferenceEquals(runtimeModel, EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId))) {
                reference = CreateGeneratedReference(EngineGeneratedAssetProvider.CubeRelativePath, EngineGeneratedModelCache.CubeAssetId);
                return true;
            }
            if (ReferenceEquals(runtimeModel, EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.PlaneAssetId))) {
                reference = CreateGeneratedReference(EngineGeneratedAssetProvider.PlaneRelativePath, EngineGeneratedModelCache.PlaneAssetId);
                return true;
            }
            if (ReferenceEquals(runtimeModel, EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.SphereAssetId))) {
                reference = CreateGeneratedReference(EngineGeneratedAssetProvider.SphereRelativePath, EngineGeneratedModelCache.SphereAssetId);
                return true;
            }

            reference = null;
            return false;
        }

        /// <summary>
        /// Attempts to infer one file-system model reference from the supplied runtime model id.
        /// </summary>
        /// <param name="runtimeModel">Runtime model to inspect.</param>
        /// <param name="reference">Inferred file-system model reference when one could be resolved.</param>
        /// <returns>True when the runtime model id matches an authored model source file.</returns>
        bool TryInferFileSystemModelReference(RuntimeModel runtimeModel, out SceneAssetReference reference) {
            if (runtimeModel == null) {
                throw new ArgumentNullException(nameof(runtimeModel));
            }

            string runtimeModelId = runtimeModel.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(runtimeModelId)) {
                reference = null;
                return false;
            }

            Dictionary<string, string> modelRelativePathsByAssetId = GetModelRelativePathsByAssetId();
            if (!modelRelativePathsByAssetId.TryGetValue(runtimeModelId, out string relativePath)) {
                reference = null;
                return false;
            }

            reference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
            return true;
        }

        /// <summary>
        /// Attempts to infer one generated material reference from the supplied runtime material.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material to inspect.</param>
        /// <param name="reference">Inferred generated material reference when one could be resolved.</param>
        /// <returns>True when the runtime material matches a generated engine material.</returns>
        bool TryInferGeneratedMaterialReference(RuntimeMaterial runtimeMaterial, out SceneAssetReference reference) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
            RuntimeMaterial standardMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId);
            if (ReferenceEquals(rootMaterial, standardMaterial) ||
                ReferenceEquals(runtimeMaterial, standardMaterial) ||
                string.Equals(rootMaterial.Id, BuiltInMaterialIds.StandardRuntimeMaterialAssetId, StringComparison.Ordinal)) {
                reference = CreateGeneratedReference(EngineGeneratedAssetProvider.StandardMaterialRelativePath, EngineGeneratedMaterialCache.StandardAssetId);
                return true;
            }

            reference = null;
            return false;
        }

        /// <summary>
        /// Attempts to infer one file-system material reference from the supplied runtime material id.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material to inspect.</param>
        /// <param name="reference">Inferred file-system material reference when one could be resolved.</param>
        /// <returns>True when the runtime material id matches an authored material document.</returns>
        bool TryInferFileSystemMaterialReference(RuntimeMaterial runtimeMaterial, out SceneAssetReference reference) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
            string runtimeMaterialId = rootMaterial.Id ?? runtimeMaterial.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(runtimeMaterialId)) {
                reference = null;
                return false;
            }

            Dictionary<string, string> materialRelativePathsByAssetId = GetMaterialRelativePathsByAssetId();
            if (!materialRelativePathsByAssetId.TryGetValue(runtimeMaterialId, out string relativePath)) {
                reference = null;
                return false;
            }

            reference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
            return true;
        }

        /// <summary>
        /// Returns the authored material lookup keyed by stable material asset id.
        /// </summary>
        /// <returns>Authored material lookup keyed by stable material asset id.</returns>
        Dictionary<string, string> GetMaterialRelativePathsByAssetId() {
            if (MaterialRelativePathsByAssetId != null) {
                return MaterialRelativePathsByAssetId;
            }

            MaterialRelativePathsByAssetId = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(AssetsRootPath)) {
                return MaterialRelativePathsByAssetId;
            }

            string[] materialPaths = Directory.GetFiles(AssetsRootPath, "*.hasset", SearchOption.AllDirectories);
            for (int materialIndex = 0; materialIndex < materialPaths.Length; materialIndex++) {
                string materialPath = materialPaths[materialIndex];
                if (!MaterialAssetSettingsService.TryLoad(materialPath, out MaterialAssetImportSettings settings) ||
                    settings == null ||
                    settings.Importer == null ||
                    string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                    continue;
                }

                if (MaterialRelativePathsByAssetId.ContainsKey(settings.Importer.AssetId)) {
                    continue;
                }

                string relativePath = Path.GetRelativePath(AssetsRootPath, materialPath).Replace('\\', '/');
                MaterialRelativePathsByAssetId.Add(settings.Importer.AssetId, relativePath);
            }

            return MaterialRelativePathsByAssetId;
        }

        /// <summary>
        /// Returns the authored model-source lookup keyed by stable imported model asset id.
        /// </summary>
        /// <returns>Authored model-source lookup keyed by stable imported model asset id.</returns>
        Dictionary<string, string> GetModelRelativePathsByAssetId() {
            if (ModelRelativePathsByAssetId != null) {
                return ModelRelativePathsByAssetId;
            }

            ModelRelativePathsByAssetId = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(AssetsRootPath)) {
                return ModelRelativePathsByAssetId;
            }

            string[] settingsPaths = Directory.GetFiles(AssetsRootPath, "*.hasset", SearchOption.AllDirectories);
            for (int settingsIndex = 0; settingsIndex < settingsPaths.Length; settingsIndex++) {
                string settingsPath = settingsPaths[settingsIndex];
                if (!TryLoadModelImportSettings(settingsPath, out ModelAssetImportSettings settings) ||
                    settings == null ||
                    settings.Importer == null ||
                    string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                    continue;
                }

                string relativeSourcePath = Path.GetRelativePath(AssetsRootPath, settingsPath).Replace('\\', '/');
                if (!relativeSourcePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                relativeSourcePath = relativeSourcePath.Substring(0, relativeSourcePath.Length - ".hasset".Length);
                if (ModelRelativePathsByAssetId.ContainsKey(settings.Importer.AssetId)) {
                    continue;
                }

                ModelRelativePathsByAssetId.Add(settings.Importer.AssetId, relativeSourcePath);
            }

            return ModelRelativePathsByAssetId;
        }

        /// <summary>
        /// Attempts to load one model import-settings document from disk.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the candidate model import-settings file.</param>
        /// <param name="settings">Loaded model import-settings document when deserialization succeeds.</param>
        /// <returns>True when the file contains a valid model import-settings document.</returns>
        bool TryLoadModelImportSettings(string settingsPath, out ModelAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            settings = null;
            try {
                using FileStream stream = File.OpenRead(settingsPath);
                settings = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);
                return settings != null;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Builds the stable save-state material-reference name for the supplied slot index.
        /// </summary>
        /// <param name="slotIndex">Zero-based material slot index.</param>
        /// <returns>Stable save-state material-reference name.</returns>
        static string BuildMaterialReferenceName(int slotIndex) {
            if (slotIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Material slot index must be non-negative.");
            }

            return slotIndex == 0
                ? MeshMaterialReferenceName
                : string.Concat(MeshMaterialReferenceName, "[", slotIndex.ToString(), "]");
        }

        /// <summary>
        /// Builds one generated scene asset reference for the engine generated-asset provider.
        /// </summary>
        /// <param name="relativePath">Virtual generated asset path.</param>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Generated scene asset reference.</returns>
        static SceneAssetReference CreateGeneratedReference(string relativePath, string assetId) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Generated asset path must be provided.", nameof(relativePath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }

            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = assetId
            };
        }
    }
}
