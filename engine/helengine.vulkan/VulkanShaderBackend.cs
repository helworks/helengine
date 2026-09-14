using Silk.NET.Shaderc;
using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Compiles HLSL shaders to SPIR-V bytecode for the Vulkan runtime target.
    /// </summary>
    public class VulkanShaderBackend : ShaderBackendBase {
        /// <summary>
        /// Source path reported to shaderc when the request carries no path of its own.
        /// </summary>
        const string FallbackSourcePath = "shader.hlsl";

        /// <summary>
        /// Initializes Vulkan shader compiler capabilities.
        /// </summary>
        public VulkanShaderBackend()
            : base(ShaderCompileTarget.Vulkan, BuildCapabilities(), "Vulkan") {
        }

        /// <summary>
        /// Compiles the request through shaderc and returns the resulting SPIR-V bytecode.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes; shaderc resolves includes internally so it is unused here.</param>
        /// <param name="diagnostics">Always an empty array because shaderc reports failures as exceptions rather than warnings.</param>
        /// <returns>Compiled SPIR-V bytecode.</returns>
        protected override byte[] CompileBytecode(
            ShaderCompileRequest request,
            IShaderIncludeResolver includeResolver,
            out ShaderCompileDiagnostic[] diagnostics) {
            diagnostics = Array.Empty<ShaderCompileDiagnostic>();
            return CompileSpirv(request);
        }

        /// <summary>
        /// Builds the capability metadata describing the shader models and stages the Vulkan backend accepts.
        /// </summary>
        /// <returns>Backend capability metadata.</returns>
        static ShaderBackendCapabilities BuildCapabilities() {
            ShaderModel minModel = new ShaderModel(4, 0);
            ShaderModel maxModel = new ShaderModel(6, 7);
            ShaderStage[] stages = new ShaderStage[] {
                ShaderStage.Vertex,
                ShaderStage.Pixel
            };
            return new ShaderBackendCapabilities(minModel, maxModel, stages, false);
        }

        /// <summary>
        /// Compiles HLSL source into SPIR-V bytecode using shaderc.
        /// </summary>
        /// <param name="request">Compilation request containing source and compile options.</param>
        /// <returns>Compiled SPIR-V bytecode.</returns>
        static unsafe byte[] CompileSpirv(ShaderCompileRequest request) {
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
                    sourcePath = FallbackSourcePath;
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
        static unsafe void ConfigureCompileOptions(Shaderc shaderc, CompileOptions* options, ShaderCompileRequest request) {
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

            OptimizationLevel optimizationLevel = OptimizationLevel.Zero;
            if (request.Options.Optimize) {
                optimizationLevel = OptimizationLevel.Performance;
            }

            shaderc.CompileOptionsSetOptimizationLevel(options, optimizationLevel);

            IReadOnlyList<ShaderDefine> defines = request.Defines;
            for (int i = 0; i < defines.Count; i++) {
                ShaderDefine define = defines[i];
                string name = define.Name;
                if (name == null) {
                    name = string.Empty;
                }

                string value = define.Value;
                if (value == null) {
                    value = string.Empty;
                }

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
        static unsafe void ConfigureBindingBases(Shaderc shaderc, CompileOptions* options, ShaderBindingPolicy bindingPolicy) {
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
        static ShaderKind GetShaderKind(ShaderStage stage) {
            switch (stage) {
                case ShaderStage.Vertex:
                    return ShaderKind.VertexShader;
                case ShaderStage.Pixel:
                    return ShaderKind.FragmentShader;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported shader stage for Vulkan compilation.");
            }
        }
    }
}
