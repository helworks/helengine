namespace helengine.editor {
    /// <summary>
    /// A scene-package cache key together with the content hash of every project file it covers, so two keys computed
    /// around packaging can be compared file by file.
    /// </summary>
    internal sealed class EditorScenePackageKey {
        /// <summary>
        /// Extension of the identity sidecar the asset identity index writes next to an authored file that has no
        /// embedded identity.
        /// </summary>
        const string IdentitySidecarExtension = ".hmeta";

        /// <summary>
        /// Creates a computed key.
        /// </summary>
        /// <param name="key">Lowercase hexadecimal SHA-256 cache key.</param>
        /// <param name="inputHashes">Content hash of every covered project file by project-relative path.</param>
        public EditorScenePackageKey(string key, IReadOnlyDictionary<string, string> inputHashes) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("A scene-package key must be provided.", nameof(key));
            }

            Key = key;
            InputHashes = inputHashes ?? throw new ArgumentNullException(nameof(inputHashes));
        }

        /// <summary>
        /// Lowercase hexadecimal SHA-256 cache key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Content hash of every covered project file, keyed by the project-relative path appended to the key.
        /// </summary>
        public IReadOnlyDictionary<string, string> InputHashes { get; }

        /// <summary>
        /// Reports whether a key computed after packaging differs from this one only because packaging wrote sidecars
        /// of sources whose own content did not change, in the two ways packaging is known to:
        /// <list type="bullet">
        /// <item>An existing import-settings sidecar (<c>&lt;source&gt;.hasset</c>) was rewritten, and the caller's
        /// <paramref name="isCookDerivedSettingsRewrite"/> confirms that only the cook-derived fields changed.
        /// Importing a texture during packaging persists its settings, whose asset id depends on the platform being
        /// cooked and whose checksum follows the source.</item>
        /// <item>An identity sidecar (<c>&lt;source&gt;.hmeta</c>) was created: a fresh identity reconcile during
        /// packaging gives a source that had none an identity.</item>
        /// </list>
        /// Any other difference (an edited source, a created or removed import-settings sidecar, a rewritten identity
        /// sidecar, an import-settings edit that changes how the asset cooks, any other file) means something else may
        /// have changed the project while it was packaged, so the packaged output cannot be attributed to the later
        /// inputs.
        /// </summary>
        /// <param name="packagedKey">Key computed from the same options after packaging.</param>
        /// <param name="isCookDerivedSettingsRewrite">Reports, for a rewritten import-settings sidecar's project-relative
        /// path, whether its settings before and after packaging differ only in cook-derived fields.</param>
        /// <returns>True when the keys differ and every difference is such a sidecar write.</returns>
        public bool DiffersOnlyByPackagerSidecars(EditorScenePackageKey packagedKey, Func<string, bool> isCookDerivedSettingsRewrite) {
            if (packagedKey == null) {
                throw new ArgumentNullException(nameof(packagedKey));
            } else if (isCookDerivedSettingsRewrite == null) {
                throw new ArgumentNullException(nameof(isCookDerivedSettingsRewrite));
            }

            if (string.Equals(Key, packagedKey.Key, StringComparison.Ordinal)) {
                return false;
            }

            bool foundDifference = false;
            foreach (KeyValuePair<string, string> input in InputHashes) {
                if (!packagedKey.InputHashes.TryGetValue(input.Key, out string packagedHash)) {
                    return false;
                } else if (!string.Equals(input.Value, packagedHash, StringComparison.Ordinal)) {
                    if (!IsUnchangedSourceSidecar(input.Key, AssetImportManager.SettingsExtension, packagedKey)
                        || !isCookDerivedSettingsRewrite(input.Key)) {
                        return false;
                    }

                    foundDifference = true;
                }
            }

            foreach (string packagedPath in packagedKey.InputHashes.Keys) {
                if (!InputHashes.ContainsKey(packagedPath)) {
                    if (!IsUnchangedSourceSidecar(packagedPath, IdentitySidecarExtension, packagedKey)) {
                        return false;
                    }

                    foundDifference = true;
                }
            }

            return foundDifference;
        }

        /// <summary>
        /// Reports whether a covered path is the import-settings sidecar of a covered assets source, which is what
        /// packaging may rewrite and what must be captured before packaging to judge that rewrite.
        /// </summary>
        /// <param name="relativePath">Covered project-relative path.</param>
        /// <returns>True when the path is <c>&lt;source&gt;.hasset</c> and the source is covered and is not itself a sidecar.</returns>
        public bool IsImportSettingsSidecar(string relativePath) {
            return IsUnchangedSourceSidecar(relativePath, AssetImportManager.SettingsExtension, this);
        }

        /// <summary>
        /// Reports whether one path is a sidecar with the given extension of an assets source present, with identical
        /// content, in both keys. The source must not itself be a sidecar.
        /// </summary>
        /// <param name="relativePath">Project-relative path that differs between the keys.</param>
        /// <param name="extension">Sidecar extension the path must carry.</param>
        /// <param name="packagedKey">Key computed after packaging.</param>
        /// <returns>True when the path is such a sidecar.</returns>
        bool IsUnchangedSourceSidecar(string relativePath, string extension, EditorScenePackageKey packagedKey) {
            if (!relativePath.StartsWith("assets/", StringComparison.Ordinal)
                || !relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string sourcePath = relativePath.Substring(0, relativePath.Length - extension.Length);
            return !sourcePath.EndsWith(AssetImportManager.SettingsExtension, StringComparison.OrdinalIgnoreCase)
                && !sourcePath.EndsWith(IdentitySidecarExtension, StringComparison.OrdinalIgnoreCase)
                && InputHashes.TryGetValue(sourcePath, out string sourceHash)
                && packagedKey.InputHashes.TryGetValue(sourcePath, out string packagedSourceHash)
                && string.Equals(sourceHash, packagedSourceHash, StringComparison.Ordinal);
        }
    }
}
