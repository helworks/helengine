namespace helengine {
    /// <summary>
    /// Provides the request validation and program-metadata construction that every HLSL-fed shader backend shares, leaving concrete backends responsible only for invoking their platform compiler.
    /// </summary>
    public abstract class ShaderBackendBase : IShaderBackend {
        /// <summary>
        /// Variant name recorded when a compile request does not name one.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Stores the compile target this backend emits.
        /// </summary>
        readonly ShaderCompileTarget BackendTarget;

        /// <summary>
        /// Stores the capability metadata used to validate incoming requests.
        /// </summary>
        readonly ShaderBackendCapabilities BackendCapabilities;

        /// <summary>
        /// Stores the human-readable backend name used in validation failure messages.
        /// </summary>
        readonly string BackendDisplayName;

        /// <summary>
        /// Initializes the shared backend state.
        /// </summary>
        /// <param name="target">Compile target the backend emits.</param>
        /// <param name="capabilities">Capability metadata describing supported stages and shader models.</param>
        /// <param name="displayName">Human-readable backend name used in validation failure messages.</param>
        protected ShaderBackendBase(ShaderCompileTarget target, ShaderBackendCapabilities capabilities, string displayName) {
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }

            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Backend display name is required.", nameof(displayName));
            }

            BackendTarget = target;
            BackendCapabilities = capabilities;
            BackendDisplayName = displayName;
        }

        /// <summary>
        /// Gets the backend target this compiler emits.
        /// </summary>
        public ShaderCompileTarget Target {
            get {
                return BackendTarget;
            }
        }

        /// <summary>
        /// Gets the capabilities supported by the backend.
        /// </summary>
        public ShaderBackendCapabilities Capabilities {
            get {
                return BackendCapabilities;
            }
        }

        /// <summary>
        /// Validates the request, invokes the concrete backend compiler and assembles the shared compile result.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <returns>Compilation result containing program metadata and compiled bytes.</returns>
        public ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (includeResolver == null) {
                throw new ArgumentNullException(nameof(includeResolver));
            }

            if (request.Target != BackendTarget) {
                throw new InvalidOperationException(GetType().Name + " only supports " + BackendDisplayName + " targets.");
            }

            ValidateRequest(request);

            ShaderCompileDiagnostic[] diagnostics;
            byte[] bytecode = CompileBytecode(request, includeResolver, out diagnostics);
            if (bytecode == null || bytecode.Length == 0) {
                throw new InvalidOperationException("Shader compilation produced no bytecode.");
            }

            if (diagnostics == null) {
                throw new InvalidOperationException("Shader backend did not report a diagnostic collection.");
            }

            ShaderProgramDefinition programDefinition = BuildProgramDefinition(request);
            ShaderCompiledBinary binary = new ShaderCompiledBinary(
                request.Target,
                request.Stage,
                request.EntryPoint,
                request.Variant,
                bytecode);
            return new ShaderCompileResult(request, programDefinition, binary, diagnostics, true);
        }

        /// <summary>
        /// Invokes the concrete platform compiler for an already validated request.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <param name="diagnostics">Diagnostics reported by the platform compiler; an empty array when it reported none.</param>
        /// <returns>Compiled bytecode for the requested stage.</returns>
        protected abstract byte[] CompileBytecode(
            ShaderCompileRequest request,
            IShaderIncludeResolver includeResolver,
            out ShaderCompileDiagnostic[] diagnostics);

        /// <summary>
        /// Builds the shader program definition describing bindings and variants for the compile request.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <returns>Program definition instance.</returns>
        protected ShaderProgramDefinition BuildProgramDefinition(ShaderCompileRequest request) {
            ShaderBinding[] bindings = HlslShaderBindingParser.ParseBindings(
                request.Source.Source,
                request.Options.BindingPolicy,
                request.Defines);
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
        /// <returns>Array containing the single variant the request describes.</returns>
        protected ShaderVariant[] BuildVariants(ShaderCompileRequest request) {
            string variantName = request.Variant;
            if (string.IsNullOrWhiteSpace(variantName)) {
                variantName = DefaultVariantName;
            }

            string[] defineList = BuildVariantDefines(request.Defines);
            return new ShaderVariant[] { new ShaderVariant(variantName, defineList) };
        }

        /// <summary>
        /// Converts define entries into the stable "NAME=VALUE" strings stored in variant metadata.
        /// </summary>
        /// <param name="defines">Define list to convert.</param>
        /// <returns>Array of define strings.</returns>
        protected string[] BuildVariantDefines(IReadOnlyList<ShaderDefine> defines) {
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
        /// Validates the compile request against the backend capabilities.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        void ValidateRequest(ShaderCompileRequest request) {
            if (!IsStageSupported(request.Stage)) {
                throw new InvalidOperationException("Shader stage is not supported by the " + BackendDisplayName + " backend.");
            }

            if (!IsShaderModelSupported(request.ShaderModel)) {
                throw new InvalidOperationException("Shader model is not supported by the " + BackendDisplayName + " backend.");
            }
        }

        /// <summary>
        /// Checks whether a shader stage is supported by the backend.
        /// </summary>
        /// <param name="stage">Shader stage to validate.</param>
        /// <returns>True when the stage is supported.</returns>
        bool IsStageSupported(ShaderStage stage) {
            IReadOnlyList<ShaderStage> stages = BackendCapabilities.SupportedStages;
            for (int i = 0; i < stages.Count; i++) {
                if (stages[i] == stage) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a shader model falls inside the backend's supported range.
        /// </summary>
        /// <param name="shaderModel">Shader model to validate.</param>
        /// <returns>True when the shader model is supported.</returns>
        bool IsShaderModelSupported(ShaderModel shaderModel) {
            int minComparison = CompareShaderModel(shaderModel, BackendCapabilities.MinimumShaderModel);
            int maxComparison = CompareShaderModel(shaderModel, BackendCapabilities.MaximumShaderModel);
            return minComparison >= 0 && maxComparison <= 0;
        }

        /// <summary>
        /// Compares two shader model versions by major then minor component.
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
