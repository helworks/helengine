using helengine.baseplatform.Builders;
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
        readonly Dictionary<string, EditorLoadedPlatformBuilder> LoadedBuildersByPlatformId;

        /// <summary>
        /// Initializes one catalog service for the supplied platform catalog.
        /// </summary>
        /// <param name="availablePlatforms">Available platform descriptors resolved by the editor.</param>
        public EditorPlatformCatalogService(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms)
            : this(availablePlatforms, new EditorPlatformAssetBuilderLoader()) {
        }

        /// <summary>
        /// Initializes one catalog service for the supplied platform catalog and builder loader.
        /// </summary>
        /// <param name="availablePlatforms">Available platform descriptors resolved by the editor.</param>
        /// <param name="builderLoader">Builder loader used to hydrate platform assemblies on demand.</param>
        internal EditorPlatformCatalogService(
            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms,
            EditorPlatformAssetBuilderLoader builderLoader) {
            if (availablePlatforms == null) {
                throw new ArgumentNullException(nameof(availablePlatforms));
            } else if (builderLoader == null) {
                throw new ArgumentNullException(nameof(builderLoader));
            }

            AvailablePlatforms = availablePlatforms;
            BuilderLoader = builderLoader;
            LoadedBuildersByPlatformId = new Dictionary<string, EditorLoadedPlatformBuilder>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all available platform builders that provide a builder assembly path.
        /// </summary>
        /// <returns>Loaded platform builders keyed by platform id.</returns>
        public IReadOnlyList<EditorLoadedPlatformBuilder> LoadBuilders() {
            EnsureAllLoaded();
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

            EnsureLoaded(platformId);
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
        /// Registers shader backends contributed by dynamically loaded platform builders into the supplied registry.
        /// </summary>
        /// <param name="shaderBackendRegistry">Registry that should receive contributed shader backends.</param>
        public void RegisterShaderBackends(ShaderBackendRegistry shaderBackendRegistry) {
            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            }

            IReadOnlyList<EditorLoadedPlatformBuilder> loadedBuilders = LoadBuilders();
            for (int index = 0; index < loadedBuilders.Count; index++) {
                loadedBuilders[index].RegisterShaderBackends(shaderBackendRegistry);
            }
        }

        /// <summary>
        /// Registers shader backends contributed by the requested dynamically loaded platform builder into the supplied registry.
        /// </summary>
        /// <param name="shaderBackendRegistry">Registry that should receive contributed shader backends.</param>
        /// <param name="platformId">Stable platform identifier to load on demand.</param>
        public void RegisterShaderBackends(ShaderBackendRegistry shaderBackendRegistry, string platformId) {
            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            Resolve(platformId).RegisterShaderBackends(shaderBackendRegistry);
        }

        /// <summary>
        /// Loads every installed builder assembly once and caches the results.
        /// </summary>
        void EnsureAllLoaded() {
            for (int index = 0; index < AvailablePlatforms.Count; index++) {
                AvailablePlatformDescriptor platform = AvailablePlatforms[index];
                LoadBuilder(platform);
            }
        }

        /// <summary>
        /// Loads the requested builder assembly when it has not already been cached.
        /// </summary>
        /// <param name="platformId">Stable platform identifier to load on demand.</param>
        void EnsureLoaded(string platformId) {
            if (LoadedBuildersByPlatformId.ContainsKey(platformId)) {
                return;
            }

            AvailablePlatformDescriptor platform = FindAvailablePlatform(platformId);
            if (platform == null) {
                return;
            }

            LoadBuilder(platform);
        }

        /// <summary>
        /// Finds one available platform descriptor that matches the supplied platform id.
        /// </summary>
        /// <param name="platformId">Stable platform identifier to resolve.</param>
        /// <returns>Matching platform descriptor when present; otherwise null.</returns>
        AvailablePlatformDescriptor FindAvailablePlatform(string platformId) {
            for (int index = 0; index < AvailablePlatforms.Count; index++) {
                AvailablePlatformDescriptor platform = AvailablePlatforms[index];
                if (platform == null || !string.Equals(platform.Id, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return platform;
            }

            return null;
        }

        /// <summary>
        /// Loads one installed platform builder and caches it when the builder has not already been loaded.
        /// </summary>
        /// <param name="platform">Available platform descriptor that should be loaded.</param>
        void LoadBuilder(AvailablePlatformDescriptor platform) {
            if (platform == null
                || !platform.IsInstalled
                || string.IsNullOrWhiteSpace(platform.Id)
                || string.IsNullOrWhiteSpace(platform.BuilderAssemblyPath)
                || LoadedBuildersByPlatformId.ContainsKey(platform.Id)) {
                return;
            }

            IPlatformAssetBuilder builder = BuilderLoader.Load(platform.BuilderAssemblyPath);
            LoadedBuildersByPlatformId[platform.Id] = new EditorLoadedPlatformBuilder(platform, builder);
        }
    }
}
