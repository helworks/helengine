namespace helengine.directx11 {
    /// <summary>
    /// Builds DirectX11 shader assets from HLSL source files.
    /// </summary>
    public class DirectX11ShaderAssetBuilder {
        /// <summary>
        /// Default vertex shader entry point name.
        /// </summary>
        const string DefaultVertexEntryPoint = "VS";

        /// <summary>
        /// Default pixel shader entry point name.
        /// </summary>
        const string DefaultPixelEntryPoint = "PS";

        /// <summary>
        /// Default variant name used for compiled shaders.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Stores the compile service used for shader compilation.
        /// </summary>
        readonly ShaderCompileService CompileService;

        /// <summary>
        /// Stores the compile options applied to each shader entry.
        /// </summary>
        readonly ShaderCompileOptions CompileOptions;

        /// <summary>
        /// Stores the shader model used for compilation.
        /// </summary>
        readonly ShaderModel TargetShaderModel;

        /// <summary>
        /// Stores the shader target for this builder.
        /// </summary>
        readonly ShaderCompileTarget Target;

        /// <summary>
        /// Stores the define list applied to compilation.
        /// </summary>
        readonly ShaderDefine[] Defines;
        /// <summary>
        /// Content manager used to read shader source files.
        /// </summary>
        readonly ContentManager SourceContentManager;

        /// <summary>
        /// Initializes a new shader asset builder for DirectX11.
        /// </summary>
        /// <param name="includeRootPath">Root path used for resolving shader includes.</param>
        /// <param name="shaderModel">Shader model used for compilation.</param>
        public DirectX11ShaderAssetBuilder(string includeRootPath, ShaderModel shaderModel) {
            if (string.IsNullOrWhiteSpace(includeRootPath)) {
                throw new ArgumentException("Include root path must be provided.", nameof(includeRootPath));
            }

            if (shaderModel == null) {
                throw new ArgumentNullException(nameof(shaderModel));
            }

            TargetShaderModel = shaderModel;
            Target = ShaderCompileTarget.DirectX11;
            Defines = ShaderPlatformDefines.BuildDefines(Target, TargetShaderModel, Array.Empty<ShaderDefine>());
            CompileOptions = new ShaderCompileOptions(ShaderBindingPolicies.Default, false, false, false);
            SourceContentManager = new ContentManager(includeRootPath);

            ShaderFilesystemIncludeResolver includeResolver = new ShaderFilesystemIncludeResolver(includeRootPath);
            ShaderMemoryCompileCache cache = new ShaderMemoryCompileCache();
            ShaderSourceHasher sourceHasher = new ShaderSourceHasher();
            CompileService = new ShaderCompileService(includeResolver, cache, sourceHasher);
            CompileService.RegisterBackend(new DirectX11ShaderBackend());
        }

        /// <summary>
        /// Builds a shader asset from a source file using default entry points.
        /// </summary>
        /// <param name="shaderPath">Absolute path to the shader source file.</param>
        /// <param name="shaderName">Logical shader name used for the asset.</param>
        /// <returns>Compiled shader asset.</returns>
        public ShaderAsset BuildFromFile(string shaderPath, string shaderName) {
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }

            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (!File.Exists(shaderPath)) {
                throw new FileNotFoundException("Shader source file was not found.", shaderPath);
            }

            ShaderSourceInfo sourceInfo = LoadSource(shaderPath);
            ShaderCompileResult[] results = CompilePrograms(shaderName, sourceInfo);
            ValidateCompileResults(results);
            ShaderModuleDefinition definition = BuildModuleDefinition(shaderName, results);
            return ShaderAsset.FromDefinition(definition, Target);
        }

        /// <summary>
        /// Loads shader source from disk into a source info container.
        /// </summary>
        /// <param name="shaderPath">Absolute shader source path.</param>
        /// <returns>Loaded source info.</returns>
        ShaderSourceInfo LoadSource(string shaderPath) {
            TextContent sourceContent = SourceContentManager.Load<TextContent>(shaderPath);
            return new ShaderSourceInfo(shaderPath, sourceContent.Text);
        }

        /// <summary>
        /// Compiles the default vertex and pixel programs for a shader source.
        /// </summary>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="sourceInfo">Loaded shader source info.</param>
        /// <returns>Array of compile results.</returns>
        ShaderCompileResult[] CompilePrograms(string shaderName, ShaderSourceInfo sourceInfo) {
            ShaderCompileResult[] results = new ShaderCompileResult[2];

            ShaderCompileRequest vertexRequest = BuildCompileRequest(
                shaderName,
                sourceInfo,
                ShaderStage.Vertex,
                DefaultVertexEntryPoint);
            results[0] = CompileService.Compile(vertexRequest);

            ShaderCompileRequest pixelRequest = BuildCompileRequest(
                shaderName,
                sourceInfo,
                ShaderStage.Pixel,
                DefaultPixelEntryPoint);
            results[1] = CompileService.Compile(pixelRequest);

            return results;
        }

        /// <summary>
        /// Ensures all compile results succeeded.
        /// </summary>
        /// <param name="results">Compilation results to validate.</param>
        void ValidateCompileResults(IReadOnlyList<ShaderCompileResult> results) {
            for (int i = 0; i < results.Count; i++) {
                ShaderCompileResult result = results[i];
                if (!result.Success) {
                    throw new InvalidOperationException("Shader compilation failed for one or more programs.");
                }
            }
        }

        /// <summary>
        /// Builds a compile request for a specific program.
        /// </summary>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="sourceInfo">Loaded shader source info.</param>
        /// <param name="stage">Shader stage to compile.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <returns>Constructed compile request.</returns>
        ShaderCompileRequest BuildCompileRequest(
            string shaderName,
            ShaderSourceInfo sourceInfo,
            ShaderStage stage,
            string entryPoint) {
            string programName = BuildProgramName(shaderName, stage);
            return new ShaderCompileRequest(
                sourceInfo,
                programName,
                entryPoint,
                stage,
                Target,
                TargetShaderModel,
                DefaultVariantName,
                Defines,
                CompileOptions);
        }

        /// <summary>
        /// Builds a logical program name from the shader name and stage.
        /// </summary>
        /// <param name="shaderName">Shader name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <returns>Program name string.</returns>
        string BuildProgramName(string shaderName, ShaderStage stage) {
            string suffix = GetStageSuffix(stage);
            return string.Concat(shaderName, ".", suffix);
        }

        /// <summary>
        /// Maps a shader stage to a program name suffix.
        /// </summary>
        /// <param name="stage">Shader stage to map.</param>
        /// <returns>Program name suffix.</returns>
        string GetStageSuffix(ShaderStage stage) {
            switch (stage) {
                case ShaderStage.Vertex:
                    return "vs";
                case ShaderStage.Pixel:
                    return "ps";
                case ShaderStage.Geometry:
                    return "gs";
                case ShaderStage.Hull:
                    return "hs";
                case ShaderStage.Domain:
                    return "ds";
                case ShaderStage.Compute:
                    return "cs";
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported shader stage.");
            }
        }

        /// <summary>
        /// Builds a shader module definition from compile results.
        /// </summary>
        /// <param name="moduleName">Module name for the definition.</param>
        /// <param name="results">Compile results to include.</param>
        /// <returns>Shader module definition.</returns>
        ShaderModuleDefinition BuildModuleDefinition(string moduleName, IReadOnlyList<ShaderCompileResult> results) {
            ShaderProgramDefinition[] programs = new ShaderProgramDefinition[results.Count];
            ShaderProgramBinary[] binaries = new ShaderProgramBinary[results.Count];
            string targetName = ShaderTargetNames.GetTargetName(Target);
            for (int i = 0; i < results.Count; i++) {
                ShaderCompileResult result = results[i];
                programs[i] = result.ProgramDefinition;
                binaries[i] = new ShaderProgramBinary(
                    result.ProgramDefinition.Name,
                    result.ProgramDefinition.Stage,
                    targetName,
                    result.Request.Variant,
                    result.Binary.Bytecode);
            }

            return new ShaderModuleDefinition(moduleName, programs, binaries);
        }
    }
}
