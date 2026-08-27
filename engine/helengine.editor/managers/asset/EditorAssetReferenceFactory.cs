namespace helengine.editor {
    /// <summary>
    /// Provides project-scoped authored reference construction for editor tooling.
    /// </summary>
    public static class EditorAssetReferenceFactory {
        /// <summary>
        /// Creates a canonical file-backed reference from an assets-relative path.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets directory.</param>
        /// <param name="relativePath">Assets-relative authored path.</param>
        /// <param name="expectedKind">Expected editor asset category.</param>
        /// <returns>Canonical reference containing asset id and content hash.</returns>
        public static SceneAssetReference CreateFileReference(string projectRootPath, string relativePath, AssetEntryKind expectedKind) {
            string fullPath = ResolveAssetPath(projectRootPath, relativePath);
            using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(projectRootPath);
            return resolver.CreateFileReference(fullPath, expectedKind);
        }

        /// <summary>
        /// Canonicalizes one file-backed reference when its source file is present.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets directory.</param>
        /// <param name="reference">Reference to canonicalize.</param>
        /// <param name="expectedKind">Expected editor asset category.</param>
        /// <returns>Canonical reference when the source exists; otherwise the original reference.</returns>
        public static SceneAssetReference CanonicalizeFileReference(string projectRootPath, SceneAssetReference reference, AssetEntryKind expectedKind) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                return reference;
            }

            string fullPath = ResolveAssetPath(projectRootPath, reference.RelativePath);
            if (!File.Exists(fullPath)) {
                return reference;
            }

            using EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(projectRootPath);
            if (string.IsNullOrWhiteSpace(reference.AssetId) || string.IsNullOrWhiteSpace(reference.ContentHash)) {
                return resolver.CreateFileReference(fullPath, expectedKind);
            }

            return resolver.Resolve(reference, expectedKind).CanonicalReference;
        }

        /// <summary>
        /// Resolves the assets-relative path used by the public editor helpers.
        /// </summary>
        static string ResolveAssetPath(string projectRootPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided and must not be rooted.", nameof(relativePath));
            }

            string projectRoot = Path.GetFullPath(projectRootPath);
            string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "assets"));
            string fullPath = Path.GetFullPath(Path.Combine(assetsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRootWithSeparator = assetsRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? assetsRoot
                : assetsRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsRootWithSeparator, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Authored asset references must point beneath the project assets folder.");
            }

            return fullPath;
        }
    }
}
