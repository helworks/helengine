namespace helengine.editor {
    /// <summary>
    /// Serializes the current editor blueprint authoring state into one `.hblueprint` asset stored under the project assets folder.
    /// </summary>
    public class BlueprintSaveService : IDisposable {
        /// <summary>
        /// Project authoring boundary shared with the scene serializer.
        /// </summary>
        readonly IEditorProjectAuthoringSession AuthoringSession;

        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project assets root.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Shared scene-save service reused to serialize the blueprint subtree payload.
        /// </summary>
        readonly SceneSaveService SceneSaveService;
        readonly ObjectManager ObjectManager;

        /// <summary>
        /// Initializes a new blueprint save service for one project root.
        /// </summary>
        /// <param name="authoringSession">Session that owns the project assets and authoring graph.</param>
        /// <param name="persistenceRegistry">Registry used to serialize persisted components.</param>
        public BlueprintSaveService(
            IEditorProjectAuthoringSession authoringSession,
            ComponentPersistenceRegistry persistenceRegistry) {
            AuthoringSession = authoringSession ?? throw new ArgumentNullException(nameof(authoringSession));
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }
            ProjectRootPath = Path.GetFullPath(authoringSession.ProjectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            ObjectManager = authoringSession.RendererResources?.ObjectManager
                ?? throw new InvalidOperationException("Authoring session must provide renderer resources.");
            SceneSaveService = new SceneSaveService(authoringSession, persistenceRegistry);
        }

        /// <summary>
        /// Releases resolver state owned by the intermediate scene serializer.
        /// </summary>
        public void Dispose() {
            SceneSaveService.Dispose();
        }

        /// <summary>
        /// Saves the current blueprint authoring state to one `.hblueprint` file on disk.
        /// </summary>
        /// <param name="fullPath">Absolute path where the blueprint file should be written.</param>
        public void Save(string fullPath) {
            Save(fullPath, null);
        }

        /// <summary>
        /// Saves the current Blueprint authoring state with an explicit stable embedded identity.
        /// </summary>
        /// <param name="fullPath">Absolute path where the Blueprint file should be written.</param>
        /// <param name="authoringAssetId">Stable lowercase 32-character identity, or null for ordinary editor saves.</param>
        public void Save(string fullPath, string authoringAssetId) {
            using EditorAuthoringTransaction transaction = AuthoringSession.BeginTransaction();
            Save(fullPath, authoringAssetId, transaction);
            transaction.Commit();
        }

        /// <summary>
        /// Stages the current blueprint authoring state in a caller-owned transaction.
        /// </summary>
        /// <param name="fullPath">Absolute path where the Blueprint file should be written.</param>
        /// <param name="authoringAssetId">Stable lowercase 32-character identity, or null for ordinary saves.</param>
        /// <param name="transaction">Active transaction owned by the same authoring session.</param>
        public void Save(string fullPath, string authoringAssetId, EditorAuthoringTransaction transaction) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Blueprint path must be provided.", nameof(fullPath));
            }
            if (transaction == null) {
                throw new ArgumentNullException(nameof(transaction));
            }
            if (!AuthoringSession.OwnsTransaction(transaction)) {
                throw new InvalidOperationException("The blueprint transaction belongs to a different project session.");
            }
            if (!string.Equals(transaction.ProjectRootPathValue, ProjectRootPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                throw new InvalidOperationException("The blueprint transaction belongs to a different project session.");
            }
            if (!string.IsNullOrWhiteSpace(authoringAssetId)
                && (authoringAssetId.Length != 32 || authoringAssetId.Any(character => character is < '0' or > '9' and < 'a' or > 'f'))) {
                throw new ArgumentException("Blueprint authoring asset ids must be lowercase 32-character hexadecimal values.", nameof(authoringAssetId));
            }

            EditorEntity rootEntity = BlueprintValidationService.ResolveSingleEditableRoot(ObjectManager.Entities);
            BlueprintValidationService.ValidateRootForSave(rootEntity);

            string normalizedPath = Path.GetFullPath(fullPath);
            if (!IsPathInsideAssetsRoot(normalizedPath)) {
                throw new InvalidOperationException("Blueprint files must be stored inside the project assets folder.");
            }

            SceneAsset sceneAsset = SceneSaveService.BuildAssetForBlueprint();
            SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            if (rootEntities.Length != 1) {
                throw new InvalidOperationException("Blueprint save must serialize exactly one editable root entity.");
            }

            BlueprintAsset blueprintAsset = new BlueprintAsset {
                Id = BuildBlueprintId(normalizedPath),
                RootEntity = rootEntities[0],
                AssetReferences = sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>()
            };
            if (!string.IsNullOrWhiteSpace(authoringAssetId)) {
                blueprintAsset.AuthoringAssetId = authoringAssetId;
                blueprintAsset.FormerAuthoringAssetIds = Array.Empty<string>();
            }

            transaction.WriteAsset(BuildBlueprintId(normalizedPath), blueprintAsset);
        }

        /// <summary>
        /// Builds the project-relative blueprint asset id for one output file path.
        /// </summary>
        /// <param name="fullPath">Absolute file path where the blueprint will be stored.</param>
        /// <returns>Project-relative blueprint asset id stored inside the blueprint file.</returns>
        string BuildBlueprintId(string fullPath) {
            string normalizedPath = Path.GetFullPath(fullPath);
            if (!IsPathInsideAssetsRoot(normalizedPath)) {
                throw new InvalidOperationException("Blueprint files must be stored inside the project assets folder.");
            }

            return Path.GetRelativePath(AssetsRootPath, normalizedPath).Replace('\\', '/');
        }

        /// <summary>
        /// Determines whether one full path points inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the assets folder.</returns>
        bool IsPathInsideAssetsRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(fullPath, AssetsRootPath, comparison)) {
                return true;
            }

            string rootWithSeparator = AssetsRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? AssetsRootPath
                : AssetsRootPath + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, comparison);
        }
    }
}
