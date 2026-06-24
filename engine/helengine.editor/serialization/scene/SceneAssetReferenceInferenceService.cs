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
        const string MeshMaterialReferenceName = "Materials";

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
                PopulateOverlayFontAssetReferences(nameof(FPSComponent), fpsComponent.Font, saveState);
                return;
            }

            if (component is DebugComponent debugComponent) {
                PopulateOverlayFontAssetReferences(nameof(DebugComponent), debugComponent.Font, saveState);
                return;
            }

            if (component is TextComponent textComponent) {
                PopulateOverlayFontAssetReferences(nameof(TextComponent), textComponent.Font, saveState);
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
        /// Populates any missing overlay font reference from the current runtime assignment.
        /// </summary>
        /// <param name="componentName">Friendly component name used for diagnostics.</param>
        /// <param name="font">Runtime font currently assigned to the overlay component.</param>
        /// <param name="saveState">Save-state container that should receive inferred references.</param>
        void PopulateOverlayFontAssetReferences(string componentName, FontAsset font, EntityComponentSaveState saveState) {
            if (string.IsNullOrWhiteSpace(componentName)) {
                throw new ArgumentException("Component name must be provided.", nameof(componentName));
            }
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            }
            if (saveState.TryGetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, out _)) {
                return;
            }
            if (font == null) {
                return;
            }
            if (FontAssetScenePersistenceSupport.TryResolveEditorCoreFont(font, out SceneAssetReference fontReference)) {
                saveState.SetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
                return;
            }

            throw new InvalidOperationException(componentName + " Font is assigned but could not be inferred into a stable scene asset reference.");
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

            RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
            string runtimeMaterialId = runtimeMaterial.Id ?? string.Empty;
            string rootMaterialId = rootMaterial == null || rootMaterial.Id == null ? string.Empty : rootMaterial.Id;
            throw new InvalidOperationException(
                $"MeshComponent {referenceName} is assigned but could not be inferred into a stable scene asset reference. " +
                $"Runtime material id='{runtimeMaterialId}', root material id='{rootMaterialId}'.");
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
                reference = global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel();
                return true;
            }
            if (ReferenceEquals(runtimeModel, EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.PlaneAssetId))) {
                reference = global::helengine.EngineSceneAssetReferenceFactory.CreatePlaneModel();
                return true;
            }
            if (ReferenceEquals(runtimeModel, EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.SphereAssetId))) {
                reference = global::helengine.EngineSceneAssetReferenceFactory.CreateSphereModel();
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

            reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel(relativePath);
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
                reference = global::helengine.EngineSceneAssetReferenceFactory.CreateStandardMaterial();
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

            reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemMaterial(relativePath);
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
                if (!MaterialAssetSettingsService.TryLoadMaterialAssetId(materialPath, out string assetId) ||
                    string.IsNullOrWhiteSpace(assetId)) {
                    continue;
                }

                if (MaterialRelativePathsByAssetId.ContainsKey(assetId)) {
                    continue;
                }

                string relativePath = Path.GetRelativePath(AssetsRootPath, materialPath).Replace('\\', '/');
                MaterialRelativePathsByAssetId.Add(assetId, relativePath);
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

            return AutomaticComponentAssetReferenceSupport.BuildIndexedReferenceName(MeshMaterialReferenceName, slotIndex);
        }

    }
}
