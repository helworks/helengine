namespace helengine.editor {
    /// <summary>
    /// Provides shared access to compiled shader packages in the editor.
    /// </summary>
    public static class EditorShaderPackageService {
        /// <summary>
        /// Shader module manager used to compile shaders on demand.
        /// </summary>
        static ShaderModuleManager ModuleManager;
        /// <summary>
        /// Runtime shader target used to resolve shader package files.
        /// </summary>
        static ShaderCompileTarget RuntimeTarget = ShaderCompileTarget.DirectX11;
        /// <summary>
        /// Content manager used to load serialized shader packages.
        /// </summary>
        static ContentManager PackageContentManager;

        /// <summary>
        /// Initializes the shader package service with the active module manager.
        /// </summary>
        /// <param name="shaderModuleManager">Module manager used for on-demand compilation.</param>
        /// <param name="runtimeTarget">Runtime target used by the active renderer.</param>
        /// <param name="contentManager">Content manager used to read compiled shader packages.</param>
        public static void Initialize(ShaderModuleManager shaderModuleManager, ShaderCompileTarget runtimeTarget, ContentManager contentManager) {
            if (shaderModuleManager == null) {
                throw new ArgumentNullException(nameof(shaderModuleManager));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            ModuleManager = shaderModuleManager;
            RuntimeTarget = runtimeTarget;
            PackageContentManager = contentManager;
        }

        /// <summary>
        /// Loads a shader asset from the shader cache, compiling it if required.
        /// </summary>
        /// <param name="shaderId">Shader asset identifier to load.</param>
        /// <returns>Loaded shader asset.</returns>
        public static ShaderAsset LoadShaderAsset(string shaderId) {
            if (string.IsNullOrWhiteSpace(shaderId)) {
                throw new ArgumentException("Shader id must be provided.", nameof(shaderId));
            }

            if (ModuleManager == null) {
                throw new InvalidOperationException("Shader package service has not been initialized.");
            }

            string shaderCachePath = EditorProjectPaths.ShaderCache;
            if (string.IsNullOrWhiteSpace(shaderCachePath)) {
                throw new InvalidOperationException("Shader cache path has not been initialized.");
            }

            string packagePath = ShaderPackagePaths.GetPackagePath(shaderCachePath, shaderId, RuntimeTarget);
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
        public static ShaderAsset LoadShaderAssetFromPackage(string packagePath) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                throw new ArgumentException("Shader package path must be provided.", nameof(packagePath));
            }

            if (PackageContentManager == null) {
                throw new InvalidOperationException("Shader package service has not been initialized.");
            }

            return PackageContentManager.Load<ShaderAsset>(packagePath, EditorContentProcessorIds.ShaderAsset);
        }
    }
}
