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
        /// Reports whether a key computed after packaging differs from this one only because packaging created identity
        /// sidecars (<c>&lt;source&gt;.hmeta</c>) for sources whose own content did not change: a fresh identity
        /// reconcile during packaging gives a source that had none an identity. Import-settings sidecars are not
        /// accepted: they hold authored data only and an import never rewrites an unchanged one, so any change to one
        /// is an edit. Any other difference (an edited source, a created, rewritten or removed sidecar, any other file)
        /// means something else may have changed the project while it was packaged, so the packaged output cannot be
        /// attributed to the later inputs.
        /// </summary>
        /// <param name="packagedKey">Key computed from the same options after packaging.</param>
        /// <returns>True when the keys differ and every difference is such a created identity sidecar.</returns>
        public bool DiffersOnlyByCreatedIdentitySidecars(EditorScenePackageKey packagedKey) {
            if (packagedKey == null) {
                throw new ArgumentNullException(nameof(packagedKey));
            }

            if (string.Equals(Key, packagedKey.Key, StringComparison.Ordinal)) {
                return false;
            }

            foreach (KeyValuePair<string, string> input in InputHashes) {
                if (!packagedKey.InputHashes.TryGetValue(input.Key, out string packagedHash)
                    || !string.Equals(input.Value, packagedHash, StringComparison.Ordinal)) {
                    return false;
                }
            }

            bool foundDifference = false;
            foreach (string packagedPath in packagedKey.InputHashes.Keys) {
                if (!InputHashes.ContainsKey(packagedPath)) {
                    if (!IsIdentitySidecarOfUnchangedSource(packagedPath, packagedKey)) {
                        return false;
                    }

                    foundDifference = true;
                }
            }

            return foundDifference;
        }

        /// <summary>
        /// Reports whether one path is the identity sidecar of an assets source present, with identical content, in
        /// both keys. The source must not itself be a sidecar.
        /// </summary>
        /// <param name="relativePath">Project-relative path present only in the post-packaging key.</param>
        /// <param name="packagedKey">Key computed after packaging.</param>
        /// <returns>True when the path is such an identity sidecar.</returns>
        bool IsIdentitySidecarOfUnchangedSource(string relativePath, EditorScenePackageKey packagedKey) {
            if (!relativePath.StartsWith("assets/", StringComparison.Ordinal)
                || !relativePath.EndsWith(IdentitySidecarExtension, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string sourcePath = relativePath.Substring(0, relativePath.Length - IdentitySidecarExtension.Length);
            return !sourcePath.EndsWith(AssetImportManager.SettingsExtension, StringComparison.OrdinalIgnoreCase)
                && !sourcePath.EndsWith(IdentitySidecarExtension, StringComparison.OrdinalIgnoreCase)
                && InputHashes.TryGetValue(sourcePath, out string sourceHash)
                && packagedKey.InputHashes.TryGetValue(sourcePath, out string packagedSourceHash)
                && string.Equals(sourceHash, packagedSourceHash, StringComparison.Ordinal);
        }
    }
}
