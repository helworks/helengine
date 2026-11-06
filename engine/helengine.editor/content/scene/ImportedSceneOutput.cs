namespace helengine.editor {
    /// <summary>
    /// Collects what the contributors produce for one imported scene: the blueprint entities, the generated files and which
    /// parts of the source data were consumed. Files are written lazily; asking for a reference to a file writes it first,
    /// because a file reference needs the identity and content hash of the written file.
    /// </summary>
    public sealed class ImportedSceneOutput {
        /// <summary>
        /// Writes one generated file and returns its reference, or null when the requested kind is <see cref="AssetEntryKind.Unknown"/>.
        /// </summary>
        readonly Func<ImportedSceneGeneratedFile, AssetEntryKind, SceneAssetReference> Publisher;

        /// <summary>
        /// Generated files in the order they were added.
        /// </summary>
        readonly List<ImportedSceneGeneratedFile> Files = new List<ImportedSceneGeneratedFile>();

        /// <summary>
        /// Generated files keyed by their relative path inside the folder.
        /// </summary>
        readonly Dictionary<string, ImportedSceneGeneratedFile> FilesByPath = new Dictionary<string, ImportedSceneGeneratedFile>(StringComparer.Ordinal);

        /// <summary>
        /// Blueprint entities keyed by the unique node path.
        /// </summary>
        readonly Dictionary<string, SceneEntityAsset> EntitiesByNodePath = new Dictionary<string, SceneEntityAsset>(StringComparer.Ordinal);

        /// <summary>
        /// Animation indices a contributor imported.
        /// </summary>
        readonly HashSet<int> ConsumedAnimations = new HashSet<int>();

        /// <summary>
        /// Material indices a contributor imported.
        /// </summary>
        readonly HashSet<int> ConsumedMaterials = new HashSet<int>();

        /// <summary>
        /// Mesh indices a contributor imported.
        /// </summary>
        readonly HashSet<int> ConsumedMeshes = new HashSet<int>();

        /// <summary>
        /// Number of animations in the source scene.
        /// </summary>
        readonly int AnimationCount;

        /// <summary>
        /// Number of distinct materials the source meshes use.
        /// </summary>
        readonly int MaterialCount;

        /// <summary>
        /// Number of entries in the scene material list, which bounds valid material indices.
        /// </summary>
        readonly int MaterialIndexCount;

        /// <summary>
        /// Path inside the generated folder that the importer reserves for the blueprint.
        /// </summary>
        readonly string ReservedRelativePath;

        /// <summary>
        /// Number of meshes in the source scene.
        /// </summary>
        readonly int MeshCount;

        /// <summary>
        /// Creates the output for one scene import.
        /// </summary>
        /// <param name="scene">Neutral scene data being imported; only its counts are kept.</param>
        /// <param name="generatedFolderRelativePath">Assets-relative path of the generated folder.</param>
        /// <param name="processedAssetId">Processed asset id of the source; generated asset ids derive from it.</param>
        /// <param name="reservedRelativePath">Path inside the generated folder owned by the importer (the blueprint); contributors cannot add a file there.</param>
        /// <param name="publisher">Writes one generated file and returns its reference.</param>
        public ImportedSceneOutput(
            ImportedSceneData scene,
            string generatedFolderRelativePath,
            string processedAssetId,
            string reservedRelativePath,
            Func<ImportedSceneGeneratedFile, AssetEntryKind, SceneAssetReference> publisher) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (string.IsNullOrWhiteSpace(generatedFolderRelativePath)) {
                throw new ArgumentException("Generated folder path must be provided.", nameof(generatedFolderRelativePath));
            } else if (string.IsNullOrWhiteSpace(processedAssetId)) {
                throw new ArgumentException("Processed asset id must be provided.", nameof(processedAssetId));
            }

            if (string.IsNullOrWhiteSpace(reservedRelativePath)) {
                throw new ArgumentException("Reserved path must be provided.", nameof(reservedRelativePath));
            }

            ReservedRelativePath = reservedRelativePath;
            MaterialIndexCount = scene.Materials.Length;
            GeneratedFolderRelativePath = generatedFolderRelativePath;
            ProcessedAssetId = processedAssetId;
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            AnimationCount = scene.Animations.Length;
            MeshCount = scene.Meshes.Length;
            MaterialCount = CountUsedMaterials(scene);
        }

        /// <summary>
        /// Gets the assets-relative path of the generated folder, which is the source relative path plus ".generated".
        /// </summary>
        public string GeneratedFolderRelativePath { get; }

        /// <summary>
        /// Gets the processed asset id of the source file.
        /// </summary>
        public string ProcessedAssetId { get; }

        /// <summary>
        /// Gets the generated files in the order they were added.
        /// </summary>
        public IReadOnlyList<ImportedSceneGeneratedFile> GeneratedFiles => Files;

        /// <summary>
        /// Registers the blueprint entity created for a node. Only the core contributor calls this.
        /// </summary>
        /// <param name="nodePath">Unique node path, empty for the root.</param>
        /// <param name="entity">Entity created for the node.</param>
        internal void RegisterNodeEntity(string nodePath, SceneEntityAsset entity) {
            if (nodePath == null) {
                throw new ArgumentNullException(nameof(nodePath));
            } else if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (EntitiesByNodePath.ContainsKey(nodePath)) {
                throw new InvalidOperationException($"A blueprint entity is already registered for node path '{nodePath}'.");
            }

            EntitiesByNodePath.Add(nodePath, entity);
        }

        /// <summary>
        /// Gets the blueprint entity created for a node.
        /// </summary>
        /// <param name="nodePath">Unique node path, empty for the root.</param>
        /// <returns>The entity the core contributor created for the node.</returns>
        public SceneEntityAsset GetNodeEntity(string nodePath) {
            if (nodePath == null) {
                throw new ArgumentNullException(nameof(nodePath));
            }

            SceneEntityAsset entity;
            if (!EntitiesByNodePath.TryGetValue(nodePath, out entity)) {
                throw new InvalidOperationException($"No blueprint entity exists for node path '{nodePath}'.");
            }

            return entity;
        }

        /// <summary>
        /// Adds a file that will be written inside the generated folder.
        /// </summary>
        /// <param name="relativePathInFolder">Forward-slash path relative to the generated folder.</param>
        /// <param name="asset">Asset payload to serialize at that path.</param>
        public void AddGeneratedFile(string relativePathInFolder, Asset asset) {
            if (string.IsNullOrWhiteSpace(relativePathInFolder)) {
                throw new ArgumentException("Generated file path must be provided.", nameof(relativePathInFolder));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (relativePathInFolder.Contains('\\') || relativePathInFolder.StartsWith("/", StringComparison.Ordinal)
                || Array.IndexOf(relativePathInFolder.Split('/'), "..") >= 0) {
                throw new ArgumentException($"Generated file path '{relativePathInFolder}' must be a forward-slash path inside the generated folder.", nameof(relativePathInFolder));
            } else if (string.Equals(relativePathInFolder, ReservedRelativePath, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Generated file path '{relativePathInFolder}' is reserved for the blueprint.", nameof(relativePathInFolder));
            } else if (FilesByPath.ContainsKey(relativePathInFolder)) {
                throw new InvalidOperationException($"A generated file already exists at '{relativePathInFolder}'.");
            }

            ImportedSceneGeneratedFile file = new ImportedSceneGeneratedFile(relativePathInFolder, asset);
            Files.Add(file);
            FilesByPath.Add(relativePathInFolder, file);
        }

        /// <summary>
        /// Writes a previously added file and returns a normal file-system reference to it.
        /// </summary>
        /// <param name="relativePathInFolder">Path the file was added under.</param>
        /// <param name="kind">Asset category of the file.</param>
        /// <returns>File-system reference to the written file.</returns>
        public SceneAssetReference ReferenceGeneratedFile(string relativePathInFolder, AssetEntryKind kind) {
            if (string.IsNullOrWhiteSpace(relativePathInFolder)) {
                throw new ArgumentException("Generated file path must be provided.", nameof(relativePathInFolder));
            } else if (kind == AssetEntryKind.Unknown) {
                throw new ArgumentException("A reference needs a concrete asset kind.", nameof(kind));
            }

            ImportedSceneGeneratedFile file;
            if (!FilesByPath.TryGetValue(relativePathInFolder, out file)) {
                throw new InvalidOperationException($"No generated file was added at '{relativePathInFolder}'.");
            } else if (file.Reference != null) {
                if (file.ReferenceKind != kind) {
                    throw new InvalidOperationException($"Generated file '{relativePathInFolder}' was already referenced as {file.ReferenceKind}, not {kind}.");
                }

                return file.Reference;
            } else if (file.IsPublished) {
                throw new InvalidOperationException($"Generated file '{relativePathInFolder}' was already written without a reference.");
            }

            file.Reference = Publisher(file, kind);
            file.ReferenceKind = kind;
            file.IsPublished = true;
            return file.Reference;
        }

        /// <summary>
        /// Writes every added file that nothing referenced yet.
        /// </summary>
        internal void PublishRemainingFiles() {
            for (int index = 0; index < Files.Count; index++) {
                ImportedSceneGeneratedFile file = Files[index];
                if (!file.IsPublished) {
                    Publisher(file, AssetEntryKind.Unknown);
                    file.IsPublished = true;
                }
            }
        }

        /// <summary>
        /// Gets every reference handed out so far, in the order the files were added.
        /// </summary>
        /// <returns>References for the blueprint's asset reference table.</returns>
        internal SceneAssetReference[] GetReferences() {
            List<SceneAssetReference> references = new List<SceneAssetReference>();
            for (int index = 0; index < Files.Count; index++) {
                if (Files[index].Reference != null) {
                    references.Add(Files[index].Reference);
                }
            }

            return references.ToArray();
        }

        /// <summary>
        /// Records that a contributor imported an animation.
        /// </summary>
        /// <param name="animationIndex">Index into the scene animations.</param>
        public void MarkAnimationConsumed(int animationIndex) {
            EnsureIndexInRange(animationIndex, AnimationCount, nameof(animationIndex));
            ConsumedAnimations.Add(animationIndex);
        }

        /// <summary>
        /// Records that a contributor imported a material.
        /// </summary>
        /// <param name="materialIndex">Index into the scene materials.</param>
        public void MarkMaterialConsumed(int materialIndex) {
            EnsureIndexInRange(materialIndex, MaterialIndexCount, nameof(materialIndex));
            ConsumedMaterials.Add(materialIndex);
        }

        /// <summary>
        /// Records that a contributor imported a mesh.
        /// </summary>
        /// <param name="meshIndex">Index into the scene meshes.</param>
        public void MarkMeshConsumed(int meshIndex) {
            EnsureIndexInRange(meshIndex, MeshCount, nameof(meshIndex));
            ConsumedMeshes.Add(meshIndex);
        }

        /// <summary>
        /// Describes the source data no contributor consumed.
        /// </summary>
        /// <returns>One line per kind of unconsumed data; empty when everything was imported.</returns>
        public IReadOnlyList<string> DescribeUnconsumed() {
            List<string> lines = new List<string>();
            int animations = AnimationCount - ConsumedAnimations.Count;
            if (animations > 0) {
                lines.Add($"{animations} animations not imported: no animation plugin in the project");
            }
            int meshes = MeshCount - ConsumedMeshes.Count;
            if (meshes > 0) {
                lines.Add($"{meshes} meshes not imported: no contributor consumed them");
            }
            int materials = MaterialCount - ConsumedMaterials.Count;
            if (materials > 0) {
                lines.Add($"{materials} materials not imported: no contributor consumed them");
            }

            return lines;
        }

        /// <summary>
        /// Rejects an index outside the valid range of a scene list.
        /// </summary>
        /// <param name="index">Index to check.</param>
        /// <param name="count">Number of entries in the list.</param>
        /// <param name="parameterName">Name of the offending parameter.</param>
        static void EnsureIndexInRange(int index, int count, string parameterName) {
            if (index < 0 || index >= count) {
                throw new ArgumentOutOfRangeException(parameterName, index, $"Index must be in [0, {count}).");
            }
        }

        /// <summary>
        /// Counts the distinct materials the scene meshes use, because the reader may append an unused default material.
        /// </summary>
        /// <param name="scene">Scene data to inspect.</param>
        /// <returns>Number of distinct material indices used by meshes.</returns>
        static int CountUsedMaterials(ImportedSceneData scene) {
            HashSet<int> used = new HashSet<int>();
            for (int index = 0; index < scene.Meshes.Length; index++) {
                used.Add(scene.Meshes[index].MaterialIndex);
            }

            return used.Count;
        }
    }
}
