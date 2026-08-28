namespace helengine.editor {
    /// <summary>
    /// Provides shared access to compiled shader packages in the editor.
    /// </summary>
    public sealed class EditorShaderPackageService {
        /// <summary>
        /// Shader module manager used to compile shaders on demand.
        /// </summary>
        readonly ShaderModuleManager ModuleManager;
        /// <summary>
        /// Runtime shader target used to resolve shader package files.
        /// </summary>
        readonly ShaderCompileTarget RuntimeTarget;
        /// <summary>
        /// Content manager used to load serialized shader packages.
        /// </summary>
        readonly ContentManager PackageContentManager;
        /// <summary>
        /// Package output root owned by the initialized module manager.
        /// </summary>
        readonly string ShaderCachePath;
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes the shader package service with the active module manager.
        /// </summary>
        /// <param name="shaderModuleManager">Module manager used for on-demand compilation.</param>
        /// <param name="runtimeTarget">Runtime target used by the active renderer.</param>
        /// <param name="contentManager">Content manager used to read compiled shader packages.</param>
        public EditorShaderPackageService(string projectRootPath, ShaderModuleManager shaderModuleManager, ShaderCompileTarget runtimeTarget, ContentManager contentManager) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            ModuleManager = shaderModuleManager ?? throw new ArgumentNullException(nameof(shaderModuleManager));
            PackageContentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            ProjectRootPath = Path.GetFullPath(projectRootPath);
            RuntimeTarget = runtimeTarget;
            ShaderCachePath = Path.GetFullPath(shaderModuleManager.PackageOutputPath);
            string projectPrefix = ProjectRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!ShaderCachePath.StartsWith(projectPrefix, comparison)) {
                throw new InvalidOperationException("Shader package output must remain beneath the owning project.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(ProjectRootPath, ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(ShaderCachePath, ProjectRootPath);
        }

        /// <summary>
        /// Loads a shader asset from the shader cache, compiling it if required.
        /// </summary>
        /// <param name="shaderId">Shader asset identifier to load.</param>
        /// <returns>Loaded shader asset.</returns>
        public ShaderAsset LoadShaderAsset(string shaderId) {
            if (string.IsNullOrWhiteSpace(shaderId)) {
                throw new ArgumentException("Shader id must be provided.", nameof(shaderId));
            }

            string shaderCachePath = ShaderCachePath;
            string packagePath = ShaderPackagePaths.GetPackagePath(shaderCachePath, shaderId, RuntimeTarget);
            ValidatePackagePath(packagePath);
            bool compiled = ModuleManager.EnsureShaderCompiled(shaderId);
            if (!compiled && !File.Exists(packagePath)) {
                if (EditorBuiltInShaderAssetLibrary.TryLoadShaderAssetById(RuntimeTarget, shaderId, out ShaderAsset builtInShaderAsset)) {
                    return builtInShaderAsset;
                }

                throw new FileNotFoundException("Shader package was not found.", packagePath);
            }

            ModuleManager.TrackShaderUsage(shaderId);
            return LoadShaderAssetFromPackage(packagePath);
        }

        /// <summary>
        /// Loads a shader asset from a compiled package file.
        /// </summary>
        /// <param name="packagePath">Shader package path.</param>
        /// <returns>Loaded shader asset.</returns>
        public ShaderAsset LoadShaderAssetFromPackage(string packagePath) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                throw new ArgumentException("Shader package path must be provided.", nameof(packagePath));
            }

            string fullPackagePath = ValidatePackagePath(packagePath);
            return PackageContentManager.Load<ShaderAsset>(fullPackagePath, EditorContentProcessorIds.ShaderAsset);
        }

        string ValidatePackagePath(string packagePath) {
            string fullPackagePath = Path.GetFullPath(packagePath);
            string projectPrefix = ProjectRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPackagePath.StartsWith(projectPrefix, comparison)) {
                throw new InvalidDataException("Shader package paths must remain beneath the owning project.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPackagePath, ProjectRootPath);
            return fullPackagePath;
        }
    }
}
