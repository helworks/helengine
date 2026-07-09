namespace helengine.editor {
    /// <summary>
    /// Reconstructs one editable blueprint root from a serialized blueprint asset payload.
    /// </summary>
    public class BlueprintLoadService {
        /// <summary>
        /// Shared scene-load service reused for subtree materialization.
        /// </summary>
        readonly SceneLoadService SceneLoadService;

        /// <summary>
        /// Initializes a new blueprint load service.
        /// </summary>
        /// <param name="persistenceRegistry">Registry used to deserialize supported component types.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime assets.</param>
        public BlueprintLoadService(ComponentPersistenceRegistry persistenceRegistry, ISceneAssetReferenceResolver referenceResolver) {
            SceneLoadService = new SceneLoadService(
                persistenceRegistry ?? throw new ArgumentNullException(nameof(persistenceRegistry)),
                referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver)));
        }

        /// <summary>
        /// Loads one editable root entity from a blueprint asset payload.
        /// </summary>
        /// <param name="blueprintAsset">Blueprint asset payload to materialize.</param>
        /// <returns>Loaded blueprint document.</returns>
        public LoadedEditorBlueprintDocument Load(BlueprintAsset blueprintAsset) {
            BlueprintValidationService.ValidateAsset(blueprintAsset);

            IReadOnlyList<EditorEntity> loadedRoots = SceneLoadService.Load(new SceneAsset {
                RootEntities = new[] { blueprintAsset.RootEntity },
                AssetReferences = blueprintAsset.AssetReferences ?? Array.Empty<SceneAssetReference>(),
                SceneSettings = new SceneSettingsAsset()
            });

            if (loadedRoots.Count != 1) {
                throw new InvalidOperationException("Blueprint load must materialize exactly one root entity.");
            }

            return new LoadedEditorBlueprintDocument {
                RootEntity = loadedRoots[0]
            };
        }
    }
}
