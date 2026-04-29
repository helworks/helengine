namespace helengine.editor {
    /// <summary>
    /// Resolves persisted scene asset references back into runtime assets for editor scene loading.
    /// </summary>
    public class EditorSceneAssetReferenceResolver : ISceneAssetReferenceResolver {
        /// <summary>
        /// Absolute path to the project assets folder.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Content manager used to load file-backed model and material assets.
        /// </summary>
        readonly ContentManager AssetContentManager;
        /// <summary>
        /// Resolves file-system model source files through the processed model cache.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;

        /// <summary>
        /// Initializes a new runtime asset resolver for scene loading.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load file-backed assets.</param>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        public EditorSceneAssetReferenceResolver(ContentManager assetContentManager, string projectRootPath) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "assets"));
            AssetContentManager = assetContentManager;
        }

        /// <summary>
        /// Initializes a new runtime asset resolver for scene loading with support for file-system model source resolution.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load file-backed assets.</param>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        public EditorSceneAssetReferenceResolver(ContentManager assetContentManager, string projectRootPath, EditorFileSystemModelResolver fileSystemModelResolver) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (fileSystemModelResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemModelResolver));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "assets"));
            AssetContentManager = assetContentManager;
            FileSystemModelResolver = fileSystemModelResolver;
        }

        /// <summary>
        /// Resolves one persisted model reference into a runtime model instance.
        /// </summary>
        /// <param name="reference">Persisted asset reference to resolve.</param>
        /// <returns>Runtime model instance rebuilt for the editor session.</returns>
        public RuntimeModel ResolveModel(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return ResolveGeneratedModel(reference);
            } else if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return ResolveFileSystemModel(reference);
            } else {
                throw new InvalidOperationException($"Unsupported model reference source kind '{reference.SourceKind}'.");
            }
        }

        /// <summary>
        /// Resolves one persisted material reference into a runtime material instance.
        /// </summary>
        /// <param name="reference">Persisted asset reference to resolve.</param>
        /// <returns>Runtime material instance rebuilt for the editor session.</returns>
        public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return ResolveGeneratedMaterial(reference);
            } else if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return ResolveFileSystemMaterial(reference);
            } else {
                throw new InvalidOperationException($"Unsupported material reference source kind '{reference.SourceKind}'.");
            }
        }

        /// <summary>
        /// Resolves one generated model reference through the generated-asset registry.
        /// </summary>
        /// <param name="reference">Generated model reference to resolve.</param>
        /// <returns>Runtime model published by the owning generated-asset provider.</returns>
        RuntimeModel ResolveGeneratedModel(SceneAssetReference reference) {
            AssetBrowserEntry entry = BuildGeneratedEntry(reference, AssetEntryKind.Model);
            return GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
        }

        /// <summary>
        /// Resolves one file-backed model reference by importing or loading the processed cached model asset for the source file.
        /// </summary>
        /// <param name="reference">File-backed model reference to resolve.</param>
        /// <returns>Runtime model built from the processed model asset.</returns>
        RuntimeModel ResolveFileSystemModel(SceneAssetReference reference) {
            string fullPath = ResolveFileSystemAssetPath(reference);
            if (FileSystemModelResolver == null) {
                ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, EditorContentProcessorIds.ModelAsset);
                return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            }

            return FileSystemModelResolver.ResolveRuntimeModel(fullPath);
        }

        /// <summary>
        /// Resolves one generated material reference through the generated-asset registry.
        /// </summary>
        /// <param name="reference">Generated material reference to resolve.</param>
        /// <returns>Runtime material published by the owning generated-asset provider.</returns>
        RuntimeMaterial ResolveGeneratedMaterial(SceneAssetReference reference) {
            AssetBrowserEntry entry = BuildGeneratedEntry(reference, AssetEntryKind.Material);
            return GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(entry);
        }

        /// <summary>
        /// Resolves one file-backed material reference by loading the serialized material asset and its shader package.
        /// </summary>
        /// <param name="reference">File-backed material reference to resolve.</param>
        /// <returns>Runtime material built from the serialized material asset.</returns>
        RuntimeMaterial ResolveFileSystemMaterial(SceneAssetReference reference) {
            string fullPath = ResolveFileSystemAssetPath(reference);
            MaterialAsset materialAsset = AssetContentManager.Load<MaterialAsset>(fullPath, EditorContentProcessorIds.MaterialAsset);
            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                throw new InvalidOperationException("Material asset did not provide a shader asset id.");
            }

            ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAsset(materialAsset.ShaderAssetId);
            return Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Builds one generated asset-browser entry from a persisted generated asset reference.
        /// </summary>
        /// <param name="reference">Generated asset reference to convert.</param>
        /// <param name="entryKind">Entry kind expected by the generated provider.</param>
        /// <returns>Generated asset-browser entry used for runtime resolution.</returns>
        AssetBrowserEntry BuildGeneratedEntry(SceneAssetReference reference, AssetEntryKind entryKind) {
            if (string.IsNullOrWhiteSpace(reference.ProviderId)) {
                throw new InvalidOperationException("Generated asset references must include a provider id.");
            }
            if (string.IsNullOrWhiteSpace(reference.AssetId)) {
                throw new InvalidOperationException("Generated asset references must include an asset id.");
            }
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException("Generated asset references must include a relative path.");
            }

            string assetName = GetLeafName(reference.RelativePath);
            return AssetBrowserEntry.CreateGeneratedAsset(assetName, reference.RelativePath, entryKind, reference.ProviderId, reference.AssetId);
        }

        /// <summary>
        /// Resolves one project-relative asset reference to an absolute filesystem path under the assets folder.
        /// </summary>
        /// <param name="reference">File-backed asset reference to resolve.</param>
        /// <returns>Absolute path to the referenced asset file.</returns>
        string ResolveFileSystemAssetPath(SceneAssetReference reference) {
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException("File-backed asset references must include a relative path.");
            }

            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, reference.RelativePath));
            if (!IsPathInsideAssetsRoot(fullPath)) {
                throw new InvalidOperationException("File-backed asset references must stay inside the project assets folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Determines whether one absolute path points inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the current project assets folder.</returns>
        bool IsPathInsideAssetsRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }

            if (string.Equals(fullPath, AssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string rootWithSeparator;
            if (AssetsRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                rootWithSeparator = AssetsRootPath;
            } else {
                rootWithSeparator = AssetsRootPath + Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the leaf asset name from one project-relative or virtual path.
        /// </summary>
        /// <param name="relativePath">Project-relative or virtual path to inspect.</param>
        /// <returns>Leaf segment used as the generated asset-browser entry label.</returns>
        string GetLeafName(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedPath = relativePath.Replace('\\', '/');
            int separatorIndex = normalizedPath.LastIndexOf('/');
            if (separatorIndex < 0 || separatorIndex >= normalizedPath.Length - 1) {
                return normalizedPath;
            }

            return normalizedPath.Substring(separatorIndex + 1);
        }
    }
}
