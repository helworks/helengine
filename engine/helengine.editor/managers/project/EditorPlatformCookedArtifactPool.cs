using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Collects cooked runtime artifacts and computes stable content hashes for the shared build graph.
    /// </summary>
    internal sealed class EditorPlatformCookedArtifactPool {
        readonly AssetFileHasher FileHasher;
        readonly List<PlatformBuildArtifact> Artifacts;

        public EditorPlatformCookedArtifactPool(AssetFileHasher fileHasher = null) {
            FileHasher = fileHasher ?? new AssetFileHasher();
            Artifacts = new List<PlatformBuildArtifact>();
        }

        public void AddFile(string fullPath, string relativePath, string artifactKind, string variantId) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided.", nameof(fullPath));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (string.IsNullOrWhiteSpace(artifactKind)) {
                throw new ArgumentException("Artifact kind must be provided.", nameof(artifactKind));
            }
            if (string.IsNullOrWhiteSpace(variantId)) {
                throw new ArgumentException("Variant id must be provided.", nameof(variantId));
            }

            string normalizedRelativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
            string contentHash = string.Concat("sha256:", FileHasher.ComputeHash(fullPath));
            Artifacts.Add(new PlatformBuildArtifact(normalizedRelativePath, contentHash, artifactKind, variantId));
        }

        public PlatformBuildArtifact[] ToArray() {
            return [.. Artifacts];
        }
    }
}
