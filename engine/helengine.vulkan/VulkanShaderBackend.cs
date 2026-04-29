using Silk.NET.Shaderc;
using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Compiles HLSL shaders to SPIR-V bytecode for the Vulkan runtime target.
    /// </summary>
    public class VulkanShaderBackend : IShaderBackend {
        /// <summary>
        /// Default variant name used when a compile request does not specify one.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Capability metadata describing supported shader models and stages.
        /// </summary>
        readonly ShaderBackendCapabilities CapabilitiesData;

        /// <summary>
        /// Initializes Vulkan shader compiler capabilities.
        /// </summary>
        public VulkanShaderBackend() {
            ShaderModel minModel = new ShaderModel(4, 0);
            ShaderModel maxModel = new ShaderModel(6, 7);
            ShaderStage[] stages = new[] {
                ShaderStage.Vertex,
                ShaderStage.Pixel
            };
            CapabilitiesData = new ShaderBackendCapabilities(minModel, maxModel, stages, false);
        }

        /// <summary>
        /// Gets the backend target this compiler emits.
        /// </summary>
        public ShaderCompileTarget Target {
            get {
                return ShaderCompileTarget.Vulkan;
            }
        }

        /// <summary>
        /// Gets the capabilities supported by the backend.
        /// </summary>
        public ShaderBackendCapabilities Capabilities {
            get {
                return CapabilitiesData;
            }
        }

        /// <summary>
        /// Compiles the provided shader request into SPIR-V bytecode.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <returns>Compilation result containing program metadata and SPIR-V bytes.</returns>
        public ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (includeResolver == null) {
                throw new ArgumentNullException(nameof(includeResolver));
            }

            if (request.Target != ShaderCompileTarget.Vulkan) {
                throw new InvalidOperationException("VulkanShaderBackend only supports Vulkan targets.");
            }

            ValidateRequest(request);

            byte[] bytecode = CompileSpirv(request);
            ShaderProgramDefinition programDefinition = BuildProgramDefinition(request);
            ShaderCompiledBinary binary = new ShaderCompiledBinary(
                request.Target,
                request.Stage,
                request.EntryPoint,
                request.Variant,
                bytecode);
            ShaderCompileDiagnostic[] diagnostics = Array.Empty<ShaderCompileDiagnostic>();
            return new ShaderCompileResult(request, programDefinition, binary, diagnostics, true);
        }

        /// <summary>
        /// Validates target stage and shader model compatibility.
        /// </summary>
        /// <param name="request">Compilation request to validate.</param>
        void ValidateRequest(ShaderCompileRequest request) {
            if (!IsStageSupported(request.Stage)) {
                throw new InvalidOperationException("Shader stage is not supported by the Vulkan backend.");
            }

            if (!IsShaderModelSupported(request.ShaderModel)) {
                throw new InvalidOperationException("Shader model is not supported by the Vulkan backend.");
            }
        }

        /// <summary>
        /// Checks whether a shader stage is supported by this backend.
        /// </summary>
        /// <param name="stage">Shader stage to validate.</param>
        /// <returns>True when the stage is supported.</returns>
        bool IsStageSupported(ShaderStage stage) {
            IReadOnlyList<ShaderStage> stages = CapabilitiesData.SupportedStages;
            for (int i = 0; i < stages.Count; i++) {
                if (stages[i] == stage) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a shader model is supported by this backend.
        /// </summary>
        /// <param name="shaderModel">Shader model to validate.</param>
        /// <returns>True when the shader model is supported.</returns>
        bool IsShaderModelSupported(ShaderModel shaderModel) {
            int minComparison = CompareShaderModel(shaderModel, CapabilitiesData.MinimumShaderModel);
            int maxComparison = CompareShaderModel(shaderModel, CapabilitiesData.MaximumShaderModel);
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

        /// <summary>
        /// Compiles HLSL source into SPIR-V bytecode using shaderc.
        /// </summary>
        /// <param name="request">Compilation request containing source and compile options.</param>
        /// <returns>Compiled SPIR-V bytecode.</returns>
        unsafe byte[] CompileSpirv(ShaderCompileRequest request) {
            Shaderc shaderc = Shaderc.GetApi();

            Compiler* compiler = shaderc.CompilerInitialize();
            if (compiler == null) {
                throw new InvalidOperationException("Failed to initialize Shaderc compiler.");
            }

            CompileOptions* options = shaderc.CompileOptionsInitialize();
            if (options == null) {
                shaderc.CompilerRelease(compiler);
                throw new InvalidOperationException("Failed to initialize Shaderc compile options.");
            }

            try {
                ConfigureCompileOptions(shaderc, options, request);

                ShaderKind kind = GetShaderKind(request.Stage);
                string sourcePath = request.Source.Path;
                if (string.IsNullOrWhiteSpace(sourcePath)) {
                    sourcePath = "shader.hlsl";
                }

                CompilationResult* result = shaderc.CompileIntoSpv(
                    compiler,
                    request.Source.Source,
                    (nuint)request.Source.Source.Length,
                    kind,
                    sourcePath,
                    request.EntryPoint,
                    options);

                if (result == null) {
                    throw new InvalidOperationException("Shader compilation returned no result.");
                }

                try {
                    CompilationStatus status = shaderc.ResultGetCompilationStatus(result);
                    if (status != CompilationStatus.Success) {
                        string errorMessage = shaderc.ResultGetErrorMessageS(result);
                        throw new InvalidOperationException($"Shader compilation failed: {errorMessage}");
                    }

                    nuint byteLength = shaderc.ResultGetLength(result);
                    if (byteLength == 0) {
                        throw new InvalidOperationException("Shader compilation produced no output.");
                    }

                    byte* byteData = shaderc.ResultGetBytes(result);
                    byte[] spirv = new byte[(int)byteLength];
                    Marshal.Copy((IntPtr)byteData, spirv, 0, (int)byteLength);
                    return spirv;
                } finally {
                    shaderc.ResultRelease(result);
                }
            } finally {
                shaderc.CompileOptionsRelease(options);
                shaderc.CompilerRelease(compiler);
            }
        }

        /// <summary>
        /// Configures shaderc options from compile request settings.
        /// </summary>
        /// <param name="shaderc">Shaderc API entry point.</param>
        /// <param name="options">Compile options to configure.</param>
        /// <param name="request">Compile request containing defines and flags.</param>
        unsafe void ConfigureCompileOptions(Shaderc shaderc, CompileOptions* options, ShaderCompileRequest request) {
            shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan12);
            shaderc.CompileOptionsSetSourceLanguage(options, SourceLanguage.Hlsl);
            shaderc.CompileOptionsSetHlslIoMapping(options, true);
            shaderc.CompileOptionsSetAutoMapLocations(options, true);
            shaderc.CompileOptionsSetAutoBindUniforms(options, true);
            shaderc.CompileOptionsSetPreserveBindings(options, true);
            ConfigureBindingBases(shaderc, options, request.Options.BindingPolicy);

            if (request.Options.GenerateDebugInfo) {
                shaderc.CompileOptionsSetGenerateDebugInfo(options);
            }

            OptimizationLevel optimizationLevel = request.Options.Optimize
                ? OptimizationLevel.Performance
                : OptimizationLevel.Zero;
            shaderc.CompileOptionsSetOptimizationLevel(options, optimizationLevel);

            IReadOnlyList<ShaderDefine> defines = request.Defines;
            for (int i = 0; i < defines.Count; i++) {
                ShaderDefine define = defines[i];
                string name = define.Name ?? string.Empty;
                string value = define.Value ?? string.Empty;
                shaderc.CompileOptionsAddMacroDefinition(
                    options,
                    name,
                    (nuint)name.Length,
                    value,
                    (nuint)value.Length);
            }
        }

        /// <summary>
        /// Configures Vulkan descriptor binding bases so plain HLSL register declarations map directly to the engine's unified binding slots.
        /// </summary>
        /// <param name="shaderc">Shaderc API entry point.</param>
        /// <param name="options">Compile options to configure.</param>
        /// <param name="bindingPolicy">Binding policy that defines the engine's unified binding slots.</param>
        unsafe void ConfigureBindingBases(Shaderc shaderc, CompileOptions* options, ShaderBindingPolicy bindingPolicy) {
            if (bindingPolicy == null) {
                throw new ArgumentNullException(nameof(bindingPolicy));
            }

            shaderc.CompileOptionsSetBindingBase(
                options,
                UniformKind.Texture,
                (uint)bindingPolicy.GetSlot(ShaderResourceType.Texture2D, 0));
            shaderc.CompileOptionsSetBindingBase(
                options,
                UniformKind.Sampler,
                (uint)bindingPolicy.GetSlot(ShaderResourceType.Sampler, 0));
        }

        /// <summary>
        /// Maps engine shader stages to shaderc stage kinds.
        /// </summary>
        /// <param name="stage">Engine shader stage.</param>
        /// <returns>Shaderc stage kind.</returns>
        ShaderKind GetShaderKind(ShaderStage stage) {
            switch (stage) {
                case ShaderStage.Vertex:
                    return ShaderKind.VertexShader;
                case ShaderStage.Pixel:
                    return ShaderKind.FragmentShader;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported shader stage for Vulkan compilation.");
            }
        }

        /// <summary>
        /// Builds the shader program metadata used by package serialization.
        /// </summary>
        /// <param name="request">Compile request that produced the bytecode.</param>
        /// <returns>Program definition with variant metadata.</returns>
        ShaderProgramDefinition BuildProgramDefinition(ShaderCompileRequest request) {
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
        /// Builds compile-time variant metadata from the request.
        /// </summary>
        /// <param name="request">Compile request to describe.</param>
        /// <returns>Array containing a single variant description.</returns>
        ShaderVariant[] BuildVariants(ShaderCompileRequest request) {
            string variantName = string.IsNullOrWhiteSpace(request.Variant) ? DefaultVariantName : request.Variant;
            string[] defineList = BuildVariantDefines(request.Defines);
            return new[] { new ShaderVariant(variantName, defineList) };
        }

        /// <summary>
        /// Converts define entries into stable "NAME=VALUE" strings.
        /// </summary>
        /// <param name="defines">Define list to convert.</param>
        /// <returns>String array used in variant metadata.</returns>
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
    }
}
