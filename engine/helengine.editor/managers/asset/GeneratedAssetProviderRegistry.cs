namespace helengine.editor {
    /// <summary>
    /// Stores generated-asset providers and routes generated entry resolution to the owning provider.
    /// </summary>
    public static class GeneratedAssetProviderRegistry {
        /// <summary>
        /// Registered providers keyed by stable provider identifier.
        /// </summary>
        static readonly Dictionary<string, IGeneratedAssetProvider> Providers = new Dictionary<string, IGeneratedAssetProvider>(StringComparer.Ordinal);

        /// <summary>
        /// Removes every registered provider so tests can start from a known empty state.
        /// </summary>
        public static void ResetForTests() {
            Providers.Clear();
        }

        /// <summary>
        /// Registers one generated asset provider under its stable provider identifier.
        /// </summary>
        /// <param name="provider">Provider to register.</param>
        public static void Register(IGeneratedAssetProvider provider) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }
            if (string.IsNullOrWhiteSpace(provider.ProviderId)) {
                throw new InvalidOperationException("Generated asset providers must expose a provider id.");
            }

            Providers[provider.ProviderId] = provider;
        }

        /// <summary>
        /// Loads generated entries that live directly under one virtual relative path.
        /// </summary>
        /// <param name="relativePath">Virtual path whose direct children should be appended.</param>
        /// <param name="entries">Target list that receives generated entries.</param>
        public static void LoadEntries(string relativePath, List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            string normalizedPath = relativePath ?? string.Empty;
            foreach (IGeneratedAssetProvider provider in Providers.Values) {
                provider.LoadEntries(normalizedPath, entries);
            }
        }

        /// <summary>
        /// Resolves one generated model entry through its owning provider.
        /// </summary>
        /// <param name="entry">Generated entry selected by the editor.</param>
        /// <returns>Runtime model resolved by the provider.</returns>
        public static RuntimeModel ResolveRuntimeModel(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (string.IsNullOrWhiteSpace(entry.ProviderId)) {
                throw new InvalidOperationException("Generated asset entries must include a provider id.");
            }

            if (!Providers.TryGetValue(entry.ProviderId, out IGeneratedAssetProvider provider)) {
                throw new InvalidOperationException($"Generated asset provider '{entry.ProviderId}' is not registered.");
            }

            if (!provider.TryResolveRuntimeModel(entry, out RuntimeModel runtimeModel) || runtimeModel == null) {
                throw new InvalidOperationException($"Generated runtime model '{entry.AssetId}' could not be resolved.");
            }

            return runtimeModel;
        }
    }
}
