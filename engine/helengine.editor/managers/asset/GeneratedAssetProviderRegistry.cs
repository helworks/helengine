namespace helengine.editor {
    /// <summary>Stores generated-asset providers for one editor session or CLI graph.</summary>
    public sealed class GeneratedAssetProviderRegistry : IDisposable {
        readonly Dictionary<string, IGeneratedAssetProvider> Providers = new Dictionary<string, IGeneratedAssetProvider>(StringComparer.Ordinal);
        bool IsDisposed;

        /// <summary>Registers one generated provider in this isolated registry.</summary>
        public void Register(IGeneratedAssetProvider provider) {
            EnsureNotDisposed();
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }
            if (string.IsNullOrWhiteSpace(provider.ProviderId)) {
                throw new InvalidOperationException("Generated asset providers must expose a provider id.");
            }
            if (Providers.ContainsKey(provider.ProviderId)) {
                throw new InvalidOperationException($"Generated asset provider '{provider.ProviderId}' is already registered.");
            }
            Providers.Add(provider.ProviderId, provider);
        }

        /// <summary>Loads generated entries from this registry.</summary>
        public void LoadEntries(string relativePath, List<AssetBrowserEntry> entries) {
            EnsureNotDisposed();
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }
            string normalizedPath = relativePath ?? string.Empty;
            foreach (IGeneratedAssetProvider provider in Providers.Values) {
                provider.LoadEntries(normalizedPath, entries);
            }
        }

        /// <summary>Resolves one generated model through its provider.</summary>
        public RuntimeModel ResolveRuntimeModel(AssetBrowserEntry entry) {
            EnsureNotDisposed();
            IGeneratedAssetProvider provider = ResolveProvider(entry);
            if (!provider.TryResolveRuntimeModel(entry, out RuntimeModel runtimeModel) || runtimeModel == null) {
                throw new InvalidOperationException($"Generated runtime model '{entry.AssetId}' could not be resolved.");
            }
            return runtimeModel;
        }

        /// <summary>Resolves one generated material through its provider.</summary>
        public RuntimeMaterial ResolveRuntimeMaterial(AssetBrowserEntry entry) {
            EnsureNotDisposed();
            IGeneratedAssetProvider provider = ResolveProvider(entry);
            if (!provider.TryResolveRuntimeMaterial(entry, out RuntimeMaterial runtimeMaterial) || runtimeMaterial == null) {
                throw new InvalidOperationException($"Generated runtime material '{entry.AssetId}' could not be resolved.");
            }
            return runtimeMaterial;
        }

        IGeneratedAssetProvider ResolveProvider(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (string.IsNullOrWhiteSpace(entry.ProviderId)) {
                throw new InvalidOperationException("Generated asset entries must include a provider id.");
            }
            if (!Providers.TryGetValue(entry.ProviderId, out IGeneratedAssetProvider provider)) {
                throw new InvalidOperationException($"Generated asset provider '{entry.ProviderId}' is not registered.");
            }
            return provider;
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(GeneratedAssetProviderRegistry));
            }
        }

        /// <summary>Disposes registered disposable providers and closes this registry.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            List<Exception> failures = new List<Exception>();
            foreach (IGeneratedAssetProvider provider in Providers.Values) {
                if (provider is not IDisposable disposable) {
                    continue;
                }
                try {
                    disposable.Dispose();
                } catch (Exception exception) {
                    failures.Add(exception);
                }
            }
            if (failures.Count > 0) {
                throw failures.Count == 1 ? failures[0] : new AggregateException("Generated provider disposal failed.", failures);
            }
            Providers.Clear();
            IsDisposed = true;
        }
    }
}
