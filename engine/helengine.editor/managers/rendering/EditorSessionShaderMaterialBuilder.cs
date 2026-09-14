namespace helengine.editor {
    /// <summary>
    /// Builds the shader and material infrastructure one editor session needs at startup: the shader
    /// module manager that watches and rebuilds project shaders, the package build options and paths
    /// that manager works from, and the runtime materials created from built-in editor shaders such
    /// as the transform gizmo. Every input is passed in explicitly so the rules stay independent of
    /// editor session state and can be exercised on their own.
    /// </summary>
    public static class EditorSessionShaderMaterialBuilder {
        /// <summary>
        /// Default debounce delay applied to shader rebuilds triggered by the project shader watcher.
        /// </summary>
        public const int ShaderBuildDelayMilliseconds = 250;

        /// <summary>
        /// Built-in runtime shader variant compiled for editor materials.
        /// </summary>
        public const string DefaultRuntimeShaderVariant = "default";

        /// <summary>
        /// Built-in runtime shader file used for transform-gizmo materials.
        /// </summary>
        public const string TransformGizmoShaderFileName = "EditorTransformGizmo.hlsl";

        /// <summary>
        /// Built-in runtime shader file used for highlighted transform-gizmo materials.
        /// </summary>
        public const string TransformGizmoHighlightShaderFileName = "EditorTransformGizmoHighlight.hlsl";

        /// <summary>
        /// Resolves the runtime shader target declared by the active renderer backend.
        /// </summary>
        /// <param name="render3D">Renderer instance used by the editor session.</param>
        /// <returns>Shader compile target that matches the runtime renderer.</returns>
        public static ShaderCompileTarget ResolveRuntimeShaderTarget(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (render3D is IShaderCompileTargetProvider targetProvider) {
                return targetProvider.ShaderCompileTarget;
            }

            throw new InvalidOperationException("Unsupported renderer for shader runtime target resolution.");
        }

        /// <summary>
        /// Resolves the assets root path for one project root.
        /// </summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>Absolute assets root path.</returns>
        public static string ResolveAssetsRootPath(string projectRoot) {
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                throw new InvalidOperationException("Project root path is required to locate assets.");
            }

            string assetsRootPath = Path.Combine(projectRoot, "assets");
            return Path.GetFullPath(assetsRootPath);
        }

        /// <summary>
        /// Resolves the shader source root path for one project root. Editor shader sources live in
        /// the same authored assets tree as the rest of the project content.
        /// </summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>Absolute shader root path.</returns>
        public static string ResolveShaderRootPath(string projectRoot) {
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                throw new InvalidOperationException("Project root path is required to locate shader sources.");
            }

            return ResolveAssetsRootPath(projectRoot);
        }

        /// <summary>
        /// Resolves the compiled shader package output path for one project root.
        /// </summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>Absolute shader package output path.</returns>
        public static string ResolveShaderPackageOutputPath(string projectRoot) {
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                throw new InvalidOperationException("Project root path is required to locate shader output.");
            }

            string outputPath = Path.Combine(projectRoot, "cache", "shader-cache");
            return Path.GetFullPath(outputPath);
        }

        /// <summary>
        /// Builds the shader package build options used for editor shader compilation.
        /// </summary>
        /// <param name="runtimeTarget">Shader compile target matching the active renderer backend.</param>
        /// <returns>Shader package build options.</returns>
        public static ShaderPackageBuildOptions BuildShaderPackageOptions(ShaderCompileTarget runtimeTarget) {
            ShaderTargetBuildOptions targetOptions;
            switch (runtimeTarget) {
                case ShaderCompileTarget.DirectX11:
                    targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0));
                    break;
                case ShaderCompileTarget.Vulkan:
                    targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.Vulkan, new ShaderModel(4, 0));
                    break;
                default:
                    throw new InvalidOperationException("Unsupported runtime shader target.");
            }

            ShaderTargetBuildOptions[] targets = new ShaderTargetBuildOptions[] { targetOptions };
            ShaderDefine[] defines = Array.Empty<ShaderDefine>();
            return new ShaderPackageBuildOptions(
                targets,
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                defines);
        }

        /// <summary>
        /// Builds one shader module manager bound to the supplied project root and renderer target.
        /// </summary>
        /// <param name="projectRoot">Absolute project root path that owns the shader sources and cache.</param>
        /// <param name="runtimeTarget">Shader compile target matching the active renderer backend.</param>
        /// <param name="shaderBackends">Shader backends registered by the editor host.</param>
        /// <returns>Configured shader module manager.</returns>
        public static ShaderModuleManager BuildShaderModuleManager(string projectRoot, ShaderCompileTarget runtimeTarget, ShaderBackendRegistry shaderBackends) {
            if (shaderBackends == null) {
                throw new ArgumentNullException(nameof(shaderBackends));
            }

            string shaderRootPath = ResolveShaderRootPath(projectRoot);
            string packageOutputPath = ResolveShaderPackageOutputPath(projectRoot);
            ShaderPackageBuildOptions buildOptions = BuildShaderPackageOptions(runtimeTarget);
            ShaderModuleManagerOptions options = new ShaderModuleManagerOptions(
                shaderRootPath,
                packageOutputPath,
                buildOptions,
                runtimeTarget,
                shaderBackends,
                ShaderBuildDelayMilliseconds);
            return new ShaderModuleManager(options);
        }

        /// <summary>
        /// Builds the default material used by transform gizmo meshes.
        /// </summary>
        /// <param name="render3D">Renderer that owns the built material.</param>
        /// <param name="builtInShaderAssetLibrary">Session-owned built-in shader compiler and cache.</param>
        /// <returns>Runtime material instance.</returns>
        public static RuntimeMaterial BuildTransformGizmoNormalMaterial(RenderManager3D render3D, EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary) {
            return BuildBuiltInRuntimeMaterial(render3D, builtInShaderAssetLibrary, TransformGizmoShaderFileName);
        }

        /// <summary>
        /// Builds the highlighted material used by transform gizmo meshes.
        /// </summary>
        /// <param name="render3D">Renderer that owns the built material.</param>
        /// <param name="builtInShaderAssetLibrary">Session-owned built-in shader compiler and cache.</param>
        /// <returns>Runtime material instance.</returns>
        public static RuntimeMaterial BuildTransformGizmoHighlightMaterial(RenderManager3D render3D, EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary) {
            return BuildBuiltInRuntimeMaterial(render3D, builtInShaderAssetLibrary, TransformGizmoHighlightShaderFileName);
        }

        /// <summary>
        /// Builds a runtime material from one built-in editor shader source file.
        /// </summary>
        /// <param name="render3D">Renderer that compiles and owns the built material.</param>
        /// <param name="builtInShaderAssetLibrary">Session-owned built-in shader compiler and cache.</param>
        /// <param name="shaderFileName">Built-in editor shader source file name.</param>
        /// <returns>Runtime material instance.</returns>
        public static RuntimeMaterial BuildBuiltInRuntimeMaterial(RenderManager3D render3D, EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary, string shaderFileName) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (builtInShaderAssetLibrary == null) {
                throw new ArgumentNullException(nameof(builtInShaderAssetLibrary));
            }
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            IShaderCompileTargetProvider targetProvider = render3D as IShaderCompileTargetProvider;
            if (targetProvider == null) {
                throw new InvalidOperationException("Unsupported renderer backend for editor built-in shaders.");
            }

            ShaderAsset shaderAsset = builtInShaderAssetLibrary.Load(targetProvider.ShaderCompileTarget, shaderFileName);
            string shaderName = Path.GetFileNameWithoutExtension(shaderFileName);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Built-in shader name could not be resolved.");
            }
            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Shader asset id must be provided.");
            }

            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                Id = string.Concat(shaderName, ".material"),
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = string.Concat(shaderName, ".vs"),
                PixelProgram = string.Concat(shaderName, ".ps"),
                Variant = DefaultRuntimeShaderVariant
            };

            return render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }
    }
}
