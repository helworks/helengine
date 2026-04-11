using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides deterministic generated entries and one runtime model for editor tests.
    /// </summary>
    public class TestGeneratedAssetProvider : IGeneratedAssetProvider {
        /// <summary>
        /// Generated entries exposed by the provider.
        /// </summary>
        readonly IReadOnlyList<AssetBrowserEntry> Entries;

        /// <summary>
        /// Runtime model returned when the test entry is resolved.
        /// </summary>
        readonly RuntimeModel RuntimeModel;
        /// <summary>
        /// Runtime material returned when the test entry is resolved as a material.
        /// </summary>
        readonly RuntimeMaterial RuntimeMaterial;

        /// <summary>
        /// Initializes a deterministic generated provider for tests.
        /// </summary>
        /// <param name="providerId">Stable provider identifier used by the test entries.</param>
        /// <param name="entries">Generated entries exposed by the provider.</param>
        /// <param name="runtimeModel">Runtime model returned by model-resolution requests.</param>
        public TestGeneratedAssetProvider(string providerId, IReadOnlyList<AssetBrowserEntry> entries, RuntimeModel runtimeModel)
            : this(providerId, entries, runtimeModel, new TestRuntimeMaterial()) {
        }

        /// <summary>
        /// Initializes a deterministic generated provider for tests with model and material resolution payloads.
        /// </summary>
        /// <param name="providerId">Stable provider identifier used by the test entries.</param>
        /// <param name="entries">Generated entries exposed by the provider.</param>
        /// <param name="runtimeModel">Runtime model returned by model-resolution requests.</param>
        /// <param name="runtimeMaterial">Runtime material returned by material-resolution requests.</param>
        public TestGeneratedAssetProvider(string providerId, IReadOnlyList<AssetBrowserEntry> entries, RuntimeModel runtimeModel, RuntimeMaterial runtimeMaterial) {
            if (string.IsNullOrWhiteSpace(providerId)) {
                throw new ArgumentException("Provider id must be provided.", nameof(providerId));
            }
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }
            if (runtimeModel == null) {
                throw new ArgumentNullException(nameof(runtimeModel));
            }
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            ProviderId = providerId;
            Entries = entries;
            RuntimeModel = runtimeModel;
            RuntimeMaterial = runtimeMaterial;
        }

        /// <summary>
        /// Gets the stable provider identifier used by the generated test entries.
        /// </summary>
        public string ProviderId { get; }

        /// <summary>
        /// Appends entries that live directly under the requested virtual path.
        /// </summary>
        /// <param name="relativePath">Virtual path whose direct children should be appended.</param>
        /// <param name="entries">Target list that receives matching generated entries.</param>
        public void LoadEntries(string relativePath, List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            string normalizedPath = relativePath ?? string.Empty;
            for (int entryIndex = 0; entryIndex < Entries.Count; entryIndex++) {
                AssetBrowserEntry entry = Entries[entryIndex];
                string parentPath = GetParentPath(entry.RelativePath);
                if (string.Equals(parentPath, normalizedPath, StringComparison.Ordinal)) {
                    entries.Add(entry);
                }
            }
        }

        /// <summary>
        /// Resolves one generated model entry when it belongs to this test provider.
        /// </summary>
        /// <param name="entry">Generated entry requested by the test.</param>
        /// <param name="runtimeModel">Runtime model owned by the test provider.</param>
        /// <returns>True when the provider owns the entry; otherwise false.</returns>
        public bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel runtimeModel) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            runtimeModel = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
                return false;
            }

            runtimeModel = RuntimeModel;
            return true;
        }

        /// <summary>
        /// Resolves one generated material entry when it belongs to this test provider.
        /// </summary>
        /// <param name="entry">Generated entry requested by the test.</param>
        /// <param name="runtimeMaterial">Runtime material owned by the test provider.</param>
        /// <returns>True when the provider owns the entry; otherwise false.</returns>
        public bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            runtimeMaterial = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
                return false;
            }
            if (entry.EntryKind != AssetEntryKind.Material) {
                return false;
            }

            runtimeMaterial = RuntimeMaterial;
            return true;
        }

        /// <summary>
        /// Resolves the parent path for one generated entry path.
        /// </summary>
        /// <param name="relativePath">Entry path whose parent should be resolved.</param>
        /// <returns>Direct parent path or an empty string for root entries.</returns>
        string GetParentPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            int slashIndex = relativePath.LastIndexOf('/');
            return slashIndex < 0 ? string.Empty : relativePath.Substring(0, slashIndex);
        }
    }
}
