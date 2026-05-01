using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Loads platform builders and metadata from the dynamic platform catalog.
    /// </summary>
    public sealed class EditorPlatformCatalogService {
        /// <summary>
        /// Loads builder assemblies from the editor platform catalog.
        /// </summary>
        readonly EditorPlatformAssetBuilderLoader BuilderLoader;

        /// <summary>
        /// Available platform descriptors resolved from the editor platform catalog.
        /// </summary>
        readonly IReadOnlyList<AvailablePlatformDescriptor> AvailablePlatforms;

        /// <summary>
        /// Cached loaded platform builders keyed by platform id.
        /// </summary>
        IReadOnlyDictionary<string, EditorLoadedPlatformBuilder> LoadedBuildersByPlatformId;

        /// <summary>
        /// Initializes one catalog service for the supplied platform catalog.
        /// </summary>
        /// <param name="availablePlatforms">Available platform descriptors resolved by the editor.</param>
        public EditorPlatformCatalogService(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms) {
            if (availablePlatforms == null) {
                throw new ArgumentNullException(nameof(availablePlatforms));
            }

            AvailablePlatforms = availablePlatforms;
            BuilderLoader = new EditorPlatformAssetBuilderLoader();
            LoadedBuildersByPlatformId = new Dictionary<string, EditorLoadedPlatformBuilder>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all available platform builders that provide a builder assembly path.
        /// </summary>
        /// <returns>Loaded platform builders keyed by platform id.</returns>
        public IReadOnlyList<EditorLoadedPlatformBuilder> LoadBuilders() {
            EnsureLoaded();
            return LoadedBuildersByPlatformId.Values.ToArray();
        }

        /// <summary>
        /// Resolves one loaded platform builder for the supplied platform id.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <returns>Loaded platform builder for the requested platform.</returns>
        public EditorLoadedPlatformBuilder Resolve(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EnsureLoaded();
            if (LoadedBuildersByPlatformId.TryGetValue(platformId, out EditorLoadedPlatformBuilder builder)) {
                return builder;
            }

            throw new InvalidOperationException($"No loaded platform builder is registered for '{platformId}'.");
        }

        /// <summary>
        /// Resolves one selection model for the supplied platform id, when available.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <returns>Loaded platform selection model, or null when the platform does not expose a builder.</returns>
        public EditorPlatformBuildSelectionModel ResolveSelectionModel(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return null;
            }

            return Resolve(platformId).SelectionModel;
        }

        /// <summary>
        /// Loads each builder assembly once and caches the results.
        /// </summary>
        void EnsureLoaded() {
            if (LoadedBuildersByPlatformId.Count > 0) {
                return;
            }

            Dictionary<string, EditorLoadedPlatformBuilder> buildersByPlatformId = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < AvailablePlatforms.Count; index++) {
                AvailablePlatformDescriptor platform = AvailablePlatforms[index];
                if (platform == null || !platform.IsInstalled || string.IsNullOrWhiteSpace(platform.BuilderAssemblyPath)) {
                    continue;
                }

                IPlatformAssetBuilder builder = BuilderLoader.Load(platform.BuilderAssemblyPath);
                buildersByPlatformId[platform.Id] = new EditorLoadedPlatformBuilder(platform, builder);
            }

            LoadedBuildersByPlatformId = buildersByPlatformId;
        }
    }
}
