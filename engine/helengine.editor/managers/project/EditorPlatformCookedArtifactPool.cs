using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Collects cooked runtime artifacts and computes stable content hashes for the shared build graph.
    /// </summary>
    internal sealed class EditorPlatformCookedArtifactPool {
        readonly AssetFileHasher FileHasher;
        readonly List<PlatformBuildArtifact> Artifacts;

        public EditorPlatformCookedArtifactPool(string projectRootPath, AssetFileHasher fileHasher = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            FileHasher = fileHasher ?? new AssetFileHasher(projectRootPath);
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
            string contentHash = string.Concat("sha256:", ComputeCookedArtifactHash(fullPath));
            Artifacts.Add(new PlatformBuildArtifact(normalizedRelativePath, contentHash, artifactKind, variantId));
        }

        /// <summary>
        /// Adds one producer-declared material or shader file while preserving its explicit logical identity and artifact kind.
        /// </summary>
        /// <param name="fullPath">Absolute path to the already-written cooked file.</param>
        /// <param name="declaration">Producer-declared identity for the cooked material or shader file.</param>
        public void AddDeclaredFile(string fullPath, PlatformCookedArtifactDeclaration declaration) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided.", nameof(fullPath));
            } else if (declaration == null) {
                throw new ArgumentNullException(nameof(declaration));
            }

            string contentHash = string.Concat("sha256:", ComputeCookedArtifactHash(fullPath));
            Artifacts.Add(new PlatformBuildArtifact(
                declaration.RelativePath,
                declaration.LogicalArtifactId,
                contentHash,
                declaration.ArtifactKind,
                declaration.VariantId));
        }

        /// <summary>
        /// Computes a cooked-output hash without treating the build cache as authored project content.
        /// </summary>
        /// <param name="fullPath">Absolute path to the generated cooked file.</param>
        /// <returns>Lowercase hexadecimal SHA-256 hash.</returns>
        string ComputeCookedArtifactHash(string fullPath) {
            using FileStream stream = File.OpenRead(fullPath);
            return FileHasher.ComputeHash(stream);
        }

        public PlatformBuildArtifact[] ToArray() {
            return [.. Artifacts];
        }
    }
}
