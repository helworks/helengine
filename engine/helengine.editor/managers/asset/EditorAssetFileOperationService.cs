namespace helengine.editor {
    /// <summary>
    /// Performs editor asset moves and duplicates while preserving embedded and sidecar identities correctly.
    /// </summary>
    public sealed class EditorAssetFileOperationService {
        readonly string ProjectRootPath;
        readonly string AssetsRootPath;
        readonly AssetIdentityMetadataService MetadataService;
        readonly EditorAssetPathClassifier PathClassifier;

        /// <summary>Initializes a project-scoped file-operation service.</summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="metadataService">Optional identity metadata service.</param>
        /// <param name="pathClassifier">Optional path classifier.</param>
        public EditorAssetFileOperationService(string projectRootPath, AssetIdentityMetadataService metadataService = null, EditorAssetPathClassifier pathClassifier = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            MetadataService = metadataService ?? new AssetIdentityMetadataService(ProjectRootPath);
            PathClassifier = pathClassifier ?? new EditorAssetPathClassifier(ProjectRootPath);
        }

        /// <summary>Moves an authored asset and any adjacent editor sidecars.</summary>
        /// <param name="sourcePath">Existing source path.</param>
        /// <param name="destinationPath">Unused destination path.</param>
        public void Move(string sourcePath, string destinationPath) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            string source = ValidateSource(sourcePath);
            string destination = ValidateDestination(destinationPath, PathClassifier.Classify(source));
            List<Tuple<string, string>> moved = new List<Tuple<string, string>>();
            try {
                MoveIfPresent(source, destination, moved, true);
                if (!PathClassifier.UsesEmbeddedIdentity(destination)) {
                    MoveIfPresent(source + ".hasset", destination + ".hasset", moved, false);
                    MoveIfPresent(source + ".hmeta", destination + ".hmeta", moved, false);
                }
            } catch {
                for (int index = moved.Count - 1; index >= 0; index--) {
                    if (File.Exists(moved[index].Item2) && !File.Exists(moved[index].Item1)) {
                        EditorAuthoringMutationScope.MoveLeaf(ProjectRootPath, moved[index].Item2, moved[index].Item1);
                    }
                }
                throw;
            }
        }

        /// <summary>Duplicates an authored asset, copying importer settings but minting new identity metadata.</summary>
        /// <param name="sourcePath">Existing source path.</param>
        /// <param name="destinationPath">Unused destination path.</param>
        public void Duplicate(string sourcePath, string destinationPath) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            string source = ValidateSource(sourcePath);
            string destination = ValidateDestination(destinationPath, PathClassifier.Classify(source));
            EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source, destination);
            try {
                if (File.Exists(source + ".hasset")) {
                    EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source + ".hasset", destination + ".hasset");
                }
                if (PathClassifier.UsesEmbeddedIdentity(destination)) {
                    MetadataService.Save(destination, new AssetIdentityMetadataDocument {
                        AssetId = Guid.NewGuid().ToString("N")
                    });
                } else {
                    MetadataService.LoadOrCreate(destination, string.Empty);
                }
            } catch {
                DeleteIfPresent(destination + ".hmeta");
                DeleteIfPresent(destination + ".hasset");
                DeleteIfPresent(destination);
                throw;
            }
        }

        /// <summary>Moves one optional sidecar and records it for rollback.</summary>
        void MoveIfPresent(string source, string destination, ICollection<Tuple<string, string>> moved, bool required) {
            if (!File.Exists(source)) {
                if (required) {
                    throw new InvalidOperationException($"Required asset source '{source}' does not exist.");
                }
                return;
            }
            if (File.Exists(destination)) {
                throw new InvalidOperationException($"Asset destination '{destination}' already exists.");
            }
            EditorAuthoringMutationScope.MoveLeaf(ProjectRootPath, source, destination);
            moved.Add(Tuple.Create(source, destination));
        }

        /// <summary>Validates one source authored file.</summary>
        string ValidateSource(string path) {
            string fullPath = NormalizeInsideAssets(path);
            if (!File.Exists(fullPath) || !PathClassifier.IsAuthoredAsset(fullPath)) {
                throw new InvalidOperationException($"Asset source '{path}' is not an existing authored asset.");
            }
            return fullPath;
        }

        /// <summary>Validates one destination authored file.</summary>
        string ValidateDestination(string path, AssetEntryKind expectedKind) {
            string fullPath = NormalizeInsideAssets(path);
            if (File.Exists(fullPath) || File.Exists(fullPath + ".hasset") || File.Exists(fullPath + ".hmeta")) {
                throw new InvalidOperationException($"Asset destination '{path}' already exists.");
            }
            AssetEntryKind destinationKind = PathClassifier.Classify(fullPath);
            bool isNativeMaterialDestination = expectedKind == AssetEntryKind.Material &&
                string.Equals(Path.GetExtension(fullPath), EditorFileTemplateRegistry.MaterialExtension, StringComparison.OrdinalIgnoreCase);
            if (destinationKind != expectedKind && !isNativeMaterialDestination) {
                throw new InvalidOperationException($"Asset destination '{path}' does not preserve kind '{expectedKind}'.");
            }
            return fullPath;
        }

        /// <summary>Normalizes a path and requires it to remain below assets.</summary>
        string NormalizeInsideAssets(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Asset path must be provided.", nameof(path));
            }
            string fullPath = Path.GetFullPath(path);
            string prefix = AssetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, PathComparison)) {
                throw new InvalidOperationException($"Asset path '{path}' must be inside the project assets directory.");
            }
            ValidateNoReparseTraversal(fullPath);
            return fullPath;
        }

        /// <summary>Gets the platform-aware lexical comparison for path containment.</summary>
        static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        /// <summary>Rejects links or junctions anywhere between assets and the candidate path.</summary>
        void ValidateNoReparseTraversal(string fullPath) {
            string rootPath = Path.GetFullPath(AssetsRootPath);
            string currentPath = fullPath;
            while (true) {
                try {
                    FileAttributes attributes = File.GetAttributes(currentPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidOperationException($"Asset path '{fullPath}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }

                if (string.Equals(currentPath, rootPath, PathComparison)) {
                    return;
                }

                string parentPath = Path.GetDirectoryName(currentPath);
                string rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (string.IsNullOrWhiteSpace(parentPath) ||
                    (!string.Equals(parentPath, rootPath, PathComparison) && !parentPath.StartsWith(rootPrefix, PathComparison))) {
                    throw new InvalidOperationException($"Asset path '{fullPath}' must be inside the project assets directory.");
                }
                currentPath = parentPath;
            }
        }

        /// <summary>Deletes one file if present.</summary>
        void DeleteIfPresent(string path) {
            if (File.Exists(path)) {
                EditorAuthoringMutationScope.DeleteLeaf(ProjectRootPath, path);
            }
        }
    }
}
