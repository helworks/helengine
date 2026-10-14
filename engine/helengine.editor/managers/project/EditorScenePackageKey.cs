namespace helengine.editor {
    /// <summary>
    /// A scene-package cache key together with the content hash of every project file it covers, so two keys computed
    /// around packaging can be compared file by file.
    /// </summary>
    internal sealed class EditorScenePackageKey {
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
        /// Extension of the identity sidecar the asset identity index writes next to an authored file that has no
        /// embedded identity.
        /// </summary>
        const string IdentitySidecarExtension = ".hmeta";

        /// <summary>
        /// Reports whether a key computed after packaging differs from this one only because packaging created or
        /// rewrote sidecars of sources whose own content did not change: import-settings sidecars
        /// (<c>&lt;source&gt;.hasset</c>) and identity sidecars (<c>&lt;source&gt;.hmeta</c>). Importing a texture
        /// during packaging persists the importer settings next to its source, and the sidecar's derived asset id
        /// depends on the platform being cooked; a fresh identity reconcile during packaging assigns an identity
        /// sidecar to a source that had none. Both change key inputs. Any other difference (an edited source, a
        /// removed file, a non-sidecar change) means something else changed the project while it was being packaged,
        /// and the packaged output cannot be attributed to the later inputs.
        /// </summary>
        /// <param name="packagedKey">Key computed from the same options after packaging.</param>
        /// <returns>True when the keys differ and every difference is such a sidecar.</returns>
        public bool DiffersOnlyByPackagerSidecars(EditorScenePackageKey packagedKey) {
            if (packagedKey == null) {
                throw new ArgumentNullException(nameof(packagedKey));
            }

            if (string.Equals(Key, packagedKey.Key, StringComparison.Ordinal)) {
                return false;
            }

            bool foundDifference = false;
            foreach (KeyValuePair<string, string> input in InputHashes) {
                if (!packagedKey.InputHashes.TryGetValue(input.Key, out string packagedHash)) {
                    return false;
                } else if (!string.Equals(input.Value, packagedHash, StringComparison.Ordinal)) {
                    if (!IsUnchangedSourceSidecar(input.Key, packagedKey)) {
                        return false;
                    }

                    foundDifference = true;
                }
            }

            foreach (string packagedPath in packagedKey.InputHashes.Keys) {
                if (!InputHashes.ContainsKey(packagedPath)) {
                    if (!IsUnchangedSourceSidecar(packagedPath, packagedKey)) {
                        return false;
                    }

                    foundDifference = true;
                }
            }

            return foundDifference;
        }

        /// <summary>
        /// Reports whether one path is the import-settings or identity sidecar of an assets source present, with
        /// identical content, in both keys. The source must not itself be a sidecar.
        /// </summary>
        /// <param name="relativePath">Project-relative path that differs between the keys.</param>
        /// <param name="packagedKey">Key computed after packaging.</param>
        /// <returns>True when the path is such a sidecar.</returns>
        bool IsUnchangedSourceSidecar(string relativePath, EditorScenePackageKey packagedKey) {
            string extension = relativePath.EndsWith(AssetImportManager.SettingsExtension, StringComparison.OrdinalIgnoreCase)
                ? AssetImportManager.SettingsExtension
                : relativePath.EndsWith(IdentitySidecarExtension, StringComparison.OrdinalIgnoreCase)
                    ? IdentitySidecarExtension
                    : null;
            if (extension == null || !relativePath.StartsWith("assets/", StringComparison.Ordinal)) {
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
