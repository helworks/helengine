namespace helengine.editor {
    /// <summary>
    /// Builds per-target shader packages from HLSL source files.
    /// </summary>
    public class ShaderPackageBuilder {
        /// <summary>
        /// Default vertex shader entry point name.
        /// </summary>
        const string DefaultVertexEntryPoint = "VS";

        /// <summary>
        /// Default pixel shader entry point name.
        /// </summary>
        const string DefaultPixelEntryPoint = "PS";

        /// <summary>
        /// Default variant name used for shader packages.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Stores the compile service used to build shader binaries.
        /// </summary>
        readonly ShaderCompileService compileService;

        /// <summary>
        /// Stores the shader package writer used to serialize packages.
        /// </summary>
        readonly ShaderModulePackageWriter packageWriter;

        /// <summary>
        /// Stores the build options applied during compilation.
        /// </summary>
        readonly ShaderPackageBuildOptions options;
        /// <summary>
        /// Content manager used to load shader source text.
        /// </summary>
        readonly ContentManager ShaderSourceContentManager;

        /// <summary>
        /// Initializes a new shader package builder.
        /// </summary>
        /// <param name="compileService">Compile service used for shader compilation.</param>
        /// <param name="packageWriter">Package writer used to serialize outputs.</param>
        /// <param name="options">Build options applied during compilation.</param>
        /// <param name="contentManager">Content manager used to read shader source files.</param>
        public ShaderPackageBuilder(
            ShaderCompileService compileService,
            ShaderModulePackageWriter packageWriter,
            ShaderPackageBuildOptions options,
            ContentManager contentManager) {
            if (compileService == null) {
                throw new ArgumentNullException(nameof(compileService));
            }

            if (packageWriter == null) {
                throw new ArgumentNullException(nameof(packageWriter));
            }

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            this.compileService = compileService;
            this.packageWriter = packageWriter;
            this.options = options;
            ShaderSourceContentManager = contentManager;
        }

        /// <summary>
        /// Builds shader packages for all configured targets.
        /// </summary>
        /// <param name="entry">Shader source entry describing the shader source.</param>
        /// <param name="outputDirectory">Output directory for packages.</param>
        /// <returns>Array of build results for each target.</returns>
        public ShaderPackageBuildResult[] BuildPackages(ShaderSourceEntry entry, string outputDirectory) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
            }

            if (!File.Exists(entry.SourcePath)) {
                throw new FileNotFoundException("Shader source file was not found.", entry.SourcePath);
            }

            Directory.CreateDirectory(outputDirectory);
            ShaderSourceInfo sourceInfo = LoadSource(entry.SourcePath);
            IReadOnlyList<ShaderTargetBuildOptions> targets = options.Targets;
            ShaderPackageBuildResult[] results = new ShaderPackageBuildResult[targets.Count];
            for (int i = 0; i < targets.Count; i++) {
                results[i] = BuildPackageForTarget(entry, sourceInfo, outputDirectory, targets[i]);
            }

            return results;
        }

        /// <summary>
        /// Loads shader source from disk into a source info container.
        /// </summary>
        /// <param name="sourcePath">Absolute shader source path.</param>
        /// <returns>Loaded source info.</returns>
        ShaderSourceInfo LoadSource(string sourcePath) {
            TextContent sourceContent = ShaderSourceContentManager.Load<TextContent>(sourcePath);
            return new ShaderSourceInfo(sourcePath, sourceContent.Text);
        }

        /// <summary>
        /// Builds a shader package for a specific target.
        /// </summary>
        /// <param name="entry">Shader source entry describing the shader.</param>
        /// <param name="sourceInfo">Loaded shader source info.</param>
        /// <param name="outputDirectory">Output directory for the package.</param>
        /// <param name="targetOptions">Target-specific build options.</param>
        /// <returns>Build result for the target.</returns>
        ShaderPackageBuildResult BuildPackageForTarget(
            ShaderSourceEntry entry,
            ShaderSourceInfo sourceInfo,
            string outputDirectory,
            ShaderTargetBuildOptions targetOptions) {
            string packagePath = ShaderPackagePaths.GetPackagePath(outputDirectory, entry.Name, targetOptions.Target);
            try {
                ShaderCompileResult[] compileResults = CompilePrograms(entry.Name, sourceInfo, targetOptions);
                ValidateCompileResults(compileResults);
                ShaderModuleDefinition definition = BuildModuleDefinition(entry.Name, targetOptions.Target, compileResults);
                packageWriter.Write(packagePath, definition, targetOptions.Target);
                return new ShaderPackageBuildResult(targetOptions.Target, packagePath, compileResults, true, string.Empty);
            } catch (Exception ex) {
                return new ShaderPackageBuildResult(targetOptions.Target, packagePath, Array.Empty<ShaderCompileResult>(), false, ex.Message);
            }
        }

        /// <summary>
        /// Compiles the default vertex and pixel programs for the shader source.
        /// </summary>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="sourceInfo">Loaded shader source info.</param>
        /// <param name="targetOptions">Target-specific build options.</param>
        /// <returns>Array of compile results.</returns>
        ShaderCompileResult[] CompilePrograms(
            string shaderName,
            ShaderSourceInfo sourceInfo,
            ShaderTargetBuildOptions targetOptions) {
            ShaderCompileOptions compileOptions = BuildCompileOptions();
            ShaderDefine[] defines = BuildDefines(targetOptions);
            ShaderCompileResult[] results = new ShaderCompileResult[2];

            ShaderCompileRequest vertexRequest = BuildCompileRequest(
                shaderName,
                sourceInfo,
                ShaderStage.Vertex,
                DefaultVertexEntryPoint,
                targetOptions,
                compileOptions,
                defines);
            results[0] = compileService.Compile(vertexRequest);

            ShaderCompileRequest pixelRequest = BuildCompileRequest(
                shaderName,
                sourceInfo,
                ShaderStage.Pixel,
                DefaultPixelEntryPoint,
                targetOptions,
                compileOptions,
                defines);
            results[1] = compileService.Compile(pixelRequest);

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
        /// Builds compile options from the configured build options.
        /// </summary>
        /// <returns>Compile options instance.</returns>
        ShaderCompileOptions BuildCompileOptions() {
            return new ShaderCompileOptions(
                options.BindingPolicy,
                options.GenerateDebugInfo,
                options.Optimize,
                options.TreatWarningsAsErrors);
        }

        /// <summary>
        /// Builds a compile request for a specific program.
        /// </summary>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="sourceInfo">Loaded shader source info.</param>
        /// <param name="stage">Shader stage to compile.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="targetOptions">Target-specific build options.</param>
        /// <param name="compileOptions">Shared compile options.</param>
        /// <param name="defines">Defines to apply.</param>
        /// <returns>Constructed compile request.</returns>
        ShaderCompileRequest BuildCompileRequest(
            string shaderName,
            ShaderSourceInfo sourceInfo,
            ShaderStage stage,
            string entryPoint,
            ShaderTargetBuildOptions targetOptions,
            ShaderCompileOptions compileOptions,
            IReadOnlyList<ShaderDefine> defines) {
            string programName = BuildProgramName(shaderName, stage);
            return new ShaderCompileRequest(
                sourceInfo,
                programName,
                entryPoint,
                stage,
                targetOptions.Target,
                targetOptions.ShaderModel,
                DefaultVariantName,
                defines,
                compileOptions);
        }

        /// <summary>
        /// Builds the define list for a target compilation.
        /// </summary>
        /// <param name="targetOptions">Target-specific build options.</param>
        /// <returns>Array of defines for compilation.</returns>
        ShaderDefine[] BuildDefines(ShaderTargetBuildOptions targetOptions) {
            return ShaderPlatformDefines.BuildDefines(targetOptions.Target, targetOptions.ShaderModel, options.Defines);
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
        /// <param name="target">Target backend for the binaries.</param>
        /// <param name="results">Compile results to include.</param>
        /// <returns>Shader module definition.</returns>
        ShaderModuleDefinition BuildModuleDefinition(
            string moduleName,
            ShaderCompileTarget target,
            IReadOnlyList<ShaderCompileResult> results) {
            ShaderProgramDefinition[] programs = new ShaderProgramDefinition[results.Count];
            ShaderProgramBinary[] binaries = new ShaderProgramBinary[results.Count];
            string targetName = ShaderTargetNames.GetTargetName(target);
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
