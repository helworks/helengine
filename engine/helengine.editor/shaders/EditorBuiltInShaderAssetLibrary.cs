namespace helengine.editor {
    /// <summary>
    /// Loads and caches built-in editor shader assets from disk so editor runtime materials use the same file-based HLSL flow as authored shaders.
    /// </summary>
    public static class EditorBuiltInShaderAssetLibrary {
        /// <summary>
        /// Directory name that stores built-in editor shader source files.
        /// </summary>
        const string BuiltInShaderDirectoryName = "builtin";
        /// <summary>
        /// Shared variant name used by built-in editor runtime shaders.
        /// </summary>
        const string DefaultVariantName = "default";
        /// <summary>
        /// Mesh-material variant name used by file-backed standard materials in editor scene loading paths.
        /// </summary>
        const string MeshVariantName = "Mesh";
        /// <summary>
        /// Shared vertex entry point used by built-in editor runtime shaders.
        /// </summary>
        const string DefaultVertexEntryPoint = "VS";
        /// <summary>
        /// Shared pixel entry point used by built-in editor runtime shaders.
        /// </summary>
        const string DefaultPixelEntryPoint = "PS";
        /// <summary>
        /// Shared shader model used by built-in editor runtime shaders.
        /// </summary>
        static readonly ShaderModel ShaderModelValue = new ShaderModel(4, 0);
        /// <summary>
        /// Synchronization object used to guard the in-memory shader cache.
        /// </summary>
        static readonly object SyncRoot = new object();
        /// <summary>
        /// Caches compiled built-in shader assets by target and absolute source path.
        /// </summary>
        static readonly Dictionary<string, ShaderAsset> ShaderAssetsByKey = new Dictionary<string, ShaderAsset>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Stores the shader backend registry configured by bootstrap code for built-in shader compilation.
        /// </summary>
        static ShaderBackendRegistry ConfiguredShaderBackendRegistry;

        /// <summary>
        /// Configures the shader backend registry used when built-in shader sources are compiled on demand.
        /// </summary>
        /// <param name="shaderBackendRegistry">Registry populated by bootstrap code with the available shader backends.</param>
        public static void ConfigureShaderBackends(ShaderBackendRegistry shaderBackendRegistry) {
            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            }

            lock (SyncRoot) {
                ConfiguredShaderBackendRegistry = shaderBackendRegistry;
                ShaderAssetsByKey.Clear();
            }
        }

        /// <summary>
        /// Loads one built-in editor shader asset for the backend used by the supplied renderer.
        /// </summary>
        /// <param name="render3D">Renderer whose backend determines the shader target.</param>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <returns>Compiled shader asset for the renderer backend.</returns>
        public static ShaderAsset LoadShaderAsset(RenderManager3D render3D, string shaderFileName) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            ShaderCompileTarget target = ResolveTarget(render3D);
            return LoadShaderAsset(target, shaderFileName);
        }

        /// <summary>
        /// Loads one built-in editor shader asset for the supplied backend target.
        /// </summary>
        /// <param name="target">Backend target that should consume the compiled shader asset.</param>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <returns>Compiled shader asset for the requested backend.</returns>
        public static ShaderAsset LoadShaderAsset(ShaderCompileTarget target, string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            string shaderPath = ResolveShaderPath(shaderFileName);
            string cacheKey = BuildCacheKey(target, shaderPath);
            lock (SyncRoot) {
                if (ShaderAssetsByKey.TryGetValue(cacheKey, out ShaderAsset cachedShaderAsset)) {
                    return cachedShaderAsset;
                }

                ShaderAsset shaderAsset = CompileShaderAsset(target, shaderPath);
                ShaderAssetsByKey[cacheKey] = shaderAsset;
                return shaderAsset;
            }
        }

        /// <summary>
        /// Attempts to load one built-in editor shader asset from its stable shader id.
        /// </summary>
        /// <param name="target">Backend target that should consume the compiled shader asset.</param>
        /// <param name="shaderId">Stable built-in shader asset id.</param>
        /// <param name="shaderAsset">Resolved built-in shader asset when the id maps to a built-in shader source file.</param>
        /// <returns>True when the shader id mapped to a built-in shader source file; otherwise false.</returns>
        public static bool TryLoadShaderAssetById(ShaderCompileTarget target, string shaderId, out ShaderAsset shaderAsset) {
            shaderAsset = null;
            if (string.IsNullOrWhiteSpace(shaderId)) {
                return false;
            }

            string shaderFileName = shaderId + ".hlsl";
            try {
                shaderAsset = LoadShaderAsset(target, shaderFileName);
                return true;
            } catch (FileNotFoundException) {
                shaderAsset = null;
                return false;
            }
        }

        /// <summary>
        /// Resolves the absolute path to one built-in editor shader source file.
        /// </summary>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <returns>Absolute path to the built-in shader source file.</returns>
        public static string ResolveShaderPath(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory)) {
                throw new InvalidOperationException("Application base directory could not be resolved.");
            }

            string packagedShaderPath = Path.GetFullPath(Path.Combine(baseDirectory, "shaders", BuiltInShaderDirectoryName, shaderFileName));
            if (File.Exists(packagedShaderPath)) {
                return packagedShaderPath;
            }

            string sourceBuildShaderPath = TryResolveSourceBuildShaderPath(shaderFileName);
            if (!string.IsNullOrWhiteSpace(sourceBuildShaderPath)) {
                return sourceBuildShaderPath;
            }

            throw new FileNotFoundException("Built-in shader source file was not found.", packagedShaderPath);
        }

        /// <summary>
        /// Attempts to resolve one built-in shader path from the source-tree editor checkout.
        /// </summary>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <returns>Absolute source-build shader path when available; otherwise <c>null</c>.</returns>
        static string TryResolveSourceBuildShaderPath(string shaderFileName) {
            try {
                string sourceRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
                string shaderPath = Path.Combine(sourceRootPath, "engine", "helengine.editor", "shaders", BuiltInShaderDirectoryName, shaderFileName);
                string fullShaderPath = Path.GetFullPath(shaderPath);
                return File.Exists(fullShaderPath) ? fullShaderPath : null;
            } catch (InvalidOperationException) {
                return null;
            }
        }

        /// <summary>
        /// Compiles one built-in shader source file into a runtime shader asset for the requested backend.
        /// </summary>
        /// <param name="target">Backend target that should consume the compiled shader asset.</param>
        /// <param name="shaderPath">Absolute path to the built-in shader source file.</param>
        /// <returns>Compiled shader asset.</returns>
        static ShaderAsset CompileShaderAsset(ShaderCompileTarget target, string shaderPath) {
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }

            string shaderDirectory = Path.GetDirectoryName(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderDirectory)) {
                throw new InvalidOperationException("Shader directory could not be resolved.");
            }

            string shaderName = Path.GetFileNameWithoutExtension(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Shader name could not be resolved.");
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(shaderDirectory));
            TextContentManagerConfiguration.Configure(contentManager);
            TextContent sourceContent = contentManager.Load<TextContent>(shaderPath);
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(shaderPath, sourceContent.Text);
            ShaderCompileService compileService = CreateCompileService(target, shaderDirectory);
            ShaderCompileOptions compileOptions = new ShaderCompileOptions(
                ShaderBindingPolicies.Default,
                true,
                false,
                false);
            ShaderDefine[] defines = ShaderPlatformDefines.BuildDefines(target, ShaderModelValue, Array.Empty<ShaderDefine>());
            string targetName = ShaderTargetNames.GetTargetName(target);
            string[] variants = GetSupportedVariants(shaderName);
            List<ShaderProgramBinary> binaries = new List<ShaderProgramBinary>();
            ShaderProgramDefinition vertexProgram = null;
            ShaderProgramDefinition pixelProgram = null;
            for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++) {
                string variantName = variants[variantIndex];
                ShaderCompileResult vertexResult = CompileStage(
                    compileService,
                    sourceInfo,
                    target,
                    ShaderStage.Vertex,
                    BuildProgramName(shaderName, ShaderStage.Vertex),
                    DefaultVertexEntryPoint,
                    compileOptions,
                    defines);
                ShaderCompileResult pixelResult = CompileStage(
                    compileService,
                    sourceInfo,
                    target,
                    ShaderStage.Pixel,
                    BuildProgramName(shaderName, ShaderStage.Pixel),
                    DefaultPixelEntryPoint,
                    compileOptions,
                    defines);

                ValidateCompileResult(vertexResult, "vertex");
                ValidateCompileResult(pixelResult, "pixel");

                if (vertexProgram == null) {
                    vertexProgram = vertexResult.ProgramDefinition;
                }
                if (pixelProgram == null) {
                    pixelProgram = pixelResult.ProgramDefinition;
                }

                binaries.Add(new ShaderProgramBinary(vertexResult.ProgramDefinition.Name, ShaderStage.Vertex, targetName, variantName, vertexResult.Binary.Bytecode));
                binaries.Add(new ShaderProgramBinary(pixelResult.ProgramDefinition.Name, ShaderStage.Pixel, targetName, variantName, pixelResult.Binary.Bytecode));
            }

            ShaderProgramDefinition[] programs = new[] {
                vertexProgram,
                pixelProgram
            };
            var moduleDefinition = new ShaderModuleDefinition(shaderName, programs, binaries.ToArray());
            return ShaderAsset.FromDefinition(moduleDefinition, target);
        }

        /// <summary>
        /// Resolves the built-in shader variants that should be published for one built-in shader source file.
        /// </summary>
        /// <param name="shaderName">Stable built-in shader asset id.</param>
        /// <returns>Ordered built-in shader variants that should be emitted into the shader asset.</returns>
        static string[] GetSupportedVariants(string shaderName) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (string.Equals(shaderName, "ForwardStandardShader", StringComparison.Ordinal)
                || string.Equals(shaderName, "ForwardSolidColorShader", StringComparison.Ordinal)) {
                return new[] {
                    DefaultVariantName,
                    MeshVariantName
                };
            }

            return new[] {
                DefaultVariantName
            };
        }

        /// <summary>
        /// Creates a shader compile service configured for the requested backend target.
        /// </summary>
        /// <param name="target">Backend target that should consume compiled shader bytecode.</param>
        /// <param name="includeRootPath">Directory used to resolve shader includes.</param>
        /// <returns>Configured shader compile service.</returns>
        static ShaderCompileService CreateCompileService(ShaderCompileTarget target, string includeRootPath) {
            if (string.IsNullOrWhiteSpace(includeRootPath)) {
                throw new ArgumentException("Include root path must be provided.", nameof(includeRootPath));
            }

            ShaderBackendRegistry shaderBackendRegistry = GetRequiredShaderBackendRegistry(target);
            var includeResolver = new ShaderFilesystemIncludeResolver(includeRootPath);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            return shaderBackendRegistry.CreateCompileService(includeResolver, cache, hasher);
        }

        /// <summary>
        /// Compiles one shader stage from a built-in source file.
        /// </summary>
        /// <param name="compileService">Compile service used for shader compilation.</param>
        /// <param name="sourceInfo">Shader source and logical source path.</param>
        /// <param name="target">Backend target that should consume the compiled shader bytecode.</param>
        /// <param name="stage">Shader stage being compiled.</param>
        /// <param name="programName">Logical program name stored in the shader asset.</param>
        /// <param name="entryPoint">Entry point to compile.</param>
        /// <param name="compileOptions">Compile options applied to the request.</param>
        /// <param name="defines">Preprocessor defines applied to the request.</param>
        /// <returns>Compile result for the requested shader stage.</returns>
        static ShaderCompileResult CompileStage(
            ShaderCompileService compileService,
            ShaderSourceInfo sourceInfo,
            ShaderCompileTarget target,
            ShaderStage stage,
            string programName,
            string entryPoint,
            ShaderCompileOptions compileOptions,
            IReadOnlyList<ShaderDefine> defines) {
            if (compileService == null) {
                throw new ArgumentNullException(nameof(compileService));
            }

            if (sourceInfo == null) {
                throw new ArgumentNullException(nameof(sourceInfo));
            }

            if (string.IsNullOrWhiteSpace(programName)) {
                throw new ArgumentException("Program name must be provided.", nameof(programName));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Entry point must be provided.", nameof(entryPoint));
            }

            if (compileOptions == null) {
                throw new ArgumentNullException(nameof(compileOptions));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            var request = new ShaderCompileRequest(
                sourceInfo,
                programName,
                entryPoint,
                stage,
                target,
                ShaderModelValue,
                DefaultVariantName,
                defines,
                compileOptions);
            return compileService.Compile(request);
        }

        /// <summary>
        /// Validates one compile result and throws the leading diagnostic when compilation failed.
        /// </summary>
        /// <param name="result">Compile result to validate.</param>
        /// <param name="stageName">Display stage name used in failure diagnostics.</param>
        static void ValidateCompileResult(ShaderCompileResult result, string stageName) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            if (string.IsNullOrWhiteSpace(stageName)) {
                throw new ArgumentException("Stage name must be provided.", nameof(stageName));
            }

            if (result.Success) {
                return;
            }

            string message = string.Concat("Editor built-in ", stageName, " shader compilation failed.");
            if (result.Diagnostics.Count > 0 && !string.IsNullOrWhiteSpace(result.Diagnostics[0].Message)) {
                message = result.Diagnostics[0].Message;
            }

            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Builds the logical program name stored for one compiled shader stage.
        /// </summary>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="stage">Shader stage whose program name should be built.</param>
        /// <returns>Program name stored in the shader asset.</returns>
        static string BuildProgramName(string shaderName, ShaderStage stage) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            switch (stage) {
                case ShaderStage.Vertex:
                    return string.Concat(shaderName, ".vs");
                case ShaderStage.Pixel:
                    return string.Concat(shaderName, ".ps");
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported built-in shader stage.");
            }
        }

        /// <summary>
        /// Resolves the shader target used by one renderer.
        /// </summary>
        /// <param name="render3D">Renderer whose backend target should be resolved.</param>
        /// <returns>Shader compile target matching the renderer backend.</returns>
        static ShaderCompileTarget ResolveTarget(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (render3D is IShaderCompileTargetProvider targetProvider) {
                return targetProvider.ShaderCompileTarget;
            }

            throw new InvalidOperationException("Unsupported renderer backend for editor built-in shaders.");
        }

        /// <summary>
        /// Resolves the configured shader backend registry and validates that it contains the requested target.
        /// </summary>
        /// <param name="target">Target that will be compiled from the built-in shader source.</param>
        /// <returns>Configured registry that can service the requested target.</returns>
        static ShaderBackendRegistry GetRequiredShaderBackendRegistry(ShaderCompileTarget target) {
            lock (SyncRoot) {
                if (ConfiguredShaderBackendRegistry == null) {
                    throw new InvalidOperationException("Editor built-in shader backends have not been configured.");
                }

                if (!ConfiguredShaderBackendRegistry.ContainsTarget(target)) {
                    throw new InvalidOperationException("No configured built-in shader backend matches the requested target.");
                }

                return ConfiguredShaderBackendRegistry;
            }
        }

        /// <summary>
        /// Builds the cache key used to store one compiled built-in shader asset.
        /// </summary>
        /// <param name="target">Backend target used to compile the shader asset.</param>
        /// <param name="shaderPath">Absolute path to the built-in shader source file.</param>
        /// <returns>Stable cache key for the compiled shader asset.</returns>
        static string BuildCacheKey(ShaderCompileTarget target, string shaderPath) {
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }

            return string.Concat(target.ToString(), "|", shaderPath);
        }
    }
}
