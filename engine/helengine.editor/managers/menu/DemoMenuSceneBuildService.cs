namespace helengine.editor {
    /// <summary>
    /// Resolves menu-definition providers and produces baked demo menu scene assets for editor-side generation workflows.
    /// </summary>
    public class DemoMenuSceneBuildService {
        /// <summary>
        /// Provider resolver used to instantiate the authored menu definition provider.
        /// </summary>
        readonly MenuDefinitionProviderResolver ProviderResolver;

        /// <summary>
        /// Scene-asset factory used to bake menu definitions into scene payloads.
        /// </summary>
        readonly DemoMenuSceneAssetFactory SceneAssetFactory;

        /// <summary>
        /// Initializes a new demo menu scene build service.
        /// </summary>
        public DemoMenuSceneBuildService() {
            ProviderResolver = new MenuDefinitionProviderResolver();
            SceneAssetFactory = new DemoMenuSceneAssetFactory();
        }

        /// <summary>
        /// Builds one baked scene asset from the supplied provider type name.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the baked scene asset.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type name.</param>
        /// <returns>Baked demo menu scene asset.</returns>
        public SceneAsset BuildSceneAsset(string sceneId, string providerTypeName) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

            IMenuDefinitionProvider provider = ProviderResolver.Resolve(providerTypeName);
            MenuDefinition definition = provider.CreateMenuDefinition();
            if (definition == null) {
                throw new InvalidOperationException($"Menu provider '{providerTypeName}' returned a null definition.");
            }

            return BuildSceneAsset(sceneId, providerTypeName, definition);
        }

        /// <summary>
        /// Builds one baked scene asset from an already-authored menu definition.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the baked scene asset.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type name retained for future rebuilds.</param>
        /// <param name="definition">Menu definition that should be baked into the scene.</param>
        /// <returns>Baked demo menu scene asset.</returns>
        public SceneAsset BuildSceneAsset(string sceneId, string providerTypeName, MenuDefinition definition) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return SceneAssetFactory.BuildSceneAsset(sceneId, providerTypeName, definition);
        }
    }
}
