using SharpDX.D3DCompiler;
using SharpDX.Direct3D;

namespace helengine.directx11 {
    /// <summary>
    /// Compiles HLSL shaders for Direct3D 11 using the FXC toolchain.
    /// </summary>
    public class DirectX11ShaderBackend : IShaderBackend {
        /// <summary>
        /// Default program variant name used by the backend.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Stores backend capability metadata.
        /// </summary>
        readonly ShaderBackendCapabilities capabilities;

        /// <summary>
        /// Initializes a new Direct3D 11 shader backend.
        /// </summary>
        public DirectX11ShaderBackend() {
            ShaderModel minModel = new ShaderModel(4, 0);
            ShaderModel maxModel = new ShaderModel(5, 0);
            ShaderStage[] stages = new[] {
                ShaderStage.Vertex,
                ShaderStage.Pixel,
                ShaderStage.Geometry,
                ShaderStage.Hull,
                ShaderStage.Domain,
                ShaderStage.Compute
            };
            capabilities = new ShaderBackendCapabilities(minModel, maxModel, stages, false);
        }

        /// <summary>
        /// Gets the backend target this compiler emits.
        /// </summary>
        public ShaderCompileTarget Target {
            get {
                return ShaderCompileTarget.DirectX11;
            }
        }

        /// <summary>
        /// Gets the capabilities supported by the backend.
        /// </summary>
        public ShaderBackendCapabilities Capabilities {
            get {
                return capabilities;
            }
        }

        /// <summary>
        /// Compiles the provided shader request into bytecode and reflection metadata.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <returns>Compilation result.</returns>
        public ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (includeResolver == null) {
                throw new ArgumentNullException(nameof(includeResolver));
            }

            if (request.Target != ShaderCompileTarget.DirectX11) {
                throw new InvalidOperationException("DirectX11ShaderBackend only supports DirectX11 targets.");
            }

            ValidateRequest(request);
            ShaderFlags flags = BuildShaderFlags(request.Options);
            ShaderMacro[] macros = BuildMacros(request.Defines);
            string profile = request.ShaderModel.GetProfile(request.Stage);

            using (var include = new DirectX11ShaderIncludeAdapter(includeResolver, request.Source.Path))
            using (CompilationResult compilation = ShaderBytecode.Compile(
                request.Source.Source,
                request.EntryPoint,
                profile,
                flags,
                EffectFlags.None,
                macros,
                include,
                request.Source.Path)) {
                if (compilation.HasErrors) {
                    throw new InvalidOperationException(compilation.Message);
                }

                if (compilation.Bytecode == null || compilation.Bytecode.Data == null || compilation.Bytecode.Data.Length == 0) {
                    throw new InvalidOperationException("Shader compilation produced no bytecode.");
                }

                ShaderProgramDefinition programDefinition = BuildProgramDefinition(request);
                ShaderCompiledBinary binary = new ShaderCompiledBinary(
                    request.Target,
                    request.Stage,
                    request.EntryPoint,
                    request.Variant,
                    compilation.Bytecode.Data);
                ShaderCompileDiagnostic[] diagnostics = BuildDiagnostics(compilation.Message, request.Source.Path);
                return new ShaderCompileResult(request, programDefinition, binary, diagnostics, true);
            }
        }

        /// <summary>
        /// Builds the shader program definition for the compile request.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <returns>Program definition instance.</returns>
        ShaderProgramDefinition BuildProgramDefinition(ShaderCompileRequest request) {
            ShaderBinding[] bindings = Array.Empty<ShaderBinding>();
            ShaderVertexElement[] inputs = Array.Empty<ShaderVertexElement>();
            ShaderVertexElement[] outputs = Array.Empty<ShaderVertexElement>();
            ShaderVariant[] variants = BuildVariants(request);
            return new ShaderProgramDefinition(
                request.ProgramName,
                request.Stage,
                request.EntryPoint,
                bindings,
                inputs,
                outputs,
                variants);
        }

        /// <summary>
        /// Builds variant metadata for the compile request.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <returns>Array of shader variants.</returns>
        ShaderVariant[] BuildVariants(ShaderCompileRequest request) {
            string variantName = string.IsNullOrWhiteSpace(request.Variant) ? DefaultVariantName : request.Variant;
            string[] defineList = BuildVariantDefines(request.Defines);
            return new[] { new ShaderVariant(variantName, defineList) };
        }

        /// <summary>
        /// Builds an array of define strings for the variant metadata.
        /// </summary>
        /// <param name="defines">Define list to convert.</param>
        /// <returns>Array of define strings.</returns>
        string[] BuildVariantDefines(IReadOnlyList<ShaderDefine> defines) {
            if (defines.Count == 0) {
                return Array.Empty<string>();
            }

            string[] values = new string[defines.Count];
            for (int i = 0; i < defines.Count; i++) {
                ShaderDefine define = defines[i];
                if (string.IsNullOrWhiteSpace(define.Value)) {
                    values[i] = define.Name;
                } else {
                    values[i] = string.Concat(define.Name, "=", define.Value);
                }
            }

            return values;
        }

        /// <summary>
        /// Builds shader macros for the compiler from the define list.
        /// </summary>
        /// <param name="defines">Define list to convert.</param>
        /// <returns>Array of shader macros.</returns>
        ShaderMacro[] BuildMacros(IReadOnlyList<ShaderDefine> defines) {
            if (defines.Count == 0) {
                return Array.Empty<ShaderMacro>();
            }

            ShaderMacro[] macros = new ShaderMacro[defines.Count];
            for (int i = 0; i < defines.Count; i++) {
                ShaderDefine define = defines[i];
                macros[i] = new ShaderMacro(define.Name, define.Value);
            }

            return macros;
        }

        /// <summary>
        /// Builds compiler flags based on the shared compile options.
        /// </summary>
        /// <param name="options">Shared compilation options.</param>
        /// <returns>Shader compiler flags.</returns>
        ShaderFlags BuildShaderFlags(ShaderCompileOptions options) {
            ShaderFlags flags = ShaderFlags.EnableStrictness;
            if (options.GenerateDebugInfo) {
                flags |= ShaderFlags.Debug;
            }

            if (options.TreatWarningsAsErrors) {
                flags |= ShaderFlags.WarningsAreErrors;
            }

            if (options.Optimize) {
                flags |= ShaderFlags.OptimizationLevel3;
            } else {
                flags |= ShaderFlags.SkipOptimization;
            }

            return flags;
        }

        /// <summary>
        /// Builds diagnostic entries from compiler output text.
        /// </summary>
        /// <param name="message">Compiler output message text.</param>
        /// <param name="sourcePath">Source path for diagnostics.</param>
        /// <returns>Array of diagnostic entries.</returns>
        ShaderCompileDiagnostic[] BuildDiagnostics(string message, string sourcePath) {
            if (string.IsNullOrWhiteSpace(message)) {
                return Array.Empty<ShaderCompileDiagnostic>();
            }

            ShaderCompileDiagnostic diagnostic = new ShaderCompileDiagnostic(
                ShaderDiagnosticSeverity.Warning,
                message.Trim(),
                sourcePath,
                0,
                0);
            return new[] { diagnostic };
        }

        /// <summary>
        /// Validates the compile request against backend capabilities.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        void ValidateRequest(ShaderCompileRequest request) {
            if (!IsStageSupported(request.Stage)) {
                throw new InvalidOperationException("Shader stage is not supported by the DirectX11 backend.");
            }

            if (!IsShaderModelSupported(request.ShaderModel)) {
                throw new InvalidOperationException("Shader model is not supported by the DirectX11 backend.");
            }
        }

        /// <summary>
        /// Checks whether a shader stage is supported by the backend.
        /// </summary>
        /// <param name="stage">Shader stage to validate.</param>
        /// <returns>True when the stage is supported.</returns>
        bool IsStageSupported(ShaderStage stage) {
            IReadOnlyList<ShaderStage> stages = capabilities.SupportedStages;
            for (int i = 0; i < stages.Count; i++) {
                if (stages[i] == stage) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a shader model is supported by the backend.
        /// </summary>
        /// <param name="shaderModel">Shader model to validate.</param>
        /// <returns>True when the shader model is supported.</returns>
        bool IsShaderModelSupported(ShaderModel shaderModel) {
            int minComparison = CompareShaderModel(shaderModel, capabilities.MinimumShaderModel);
            int maxComparison = CompareShaderModel(shaderModel, capabilities.MaximumShaderModel);
            return minComparison >= 0 && maxComparison <= 0;
        }

        /// <summary>
        /// Compares two shader model versions.
        /// </summary>
        /// <param name="left">Left shader model.</param>
        /// <param name="right">Right shader model.</param>
        /// <returns>Negative when left is smaller, zero when equal, positive when greater.</returns>
        int CompareShaderModel(ShaderModel left, ShaderModel right) {
            if (left.Major != right.Major) {
                return left.Major.CompareTo(right.Major);
            }

            return left.Minor.CompareTo(right.Minor);
        }
    }
}
