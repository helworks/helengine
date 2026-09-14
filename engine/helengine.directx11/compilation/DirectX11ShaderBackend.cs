using SharpDX.D3DCompiler;
using SharpDX.Direct3D;

namespace helengine.directx11 {
    /// <summary>
    /// Compiles HLSL shaders for Direct3D 11 using the FXC toolchain.
    /// </summary>
    public class DirectX11ShaderBackend : ShaderBackendBase {
        /// <summary>
        /// Initializes a new Direct3D 11 shader backend.
        /// </summary>
        public DirectX11ShaderBackend()
            : base(ShaderCompileTarget.DirectX11, BuildCapabilities(), "DirectX11") {
        }

        /// <summary>
        /// Compiles the request through FXC and returns the resulting Direct3D 11 bytecode.
        /// </summary>
        /// <param name="request">Shader compilation request.</param>
        /// <param name="includeResolver">Resolver used for shader includes.</param>
        /// <param name="diagnostics">Warnings reported by FXC, or an empty array when the compiler stayed silent.</param>
        /// <returns>Compiled Direct3D 11 bytecode.</returns>
        protected override byte[] CompileBytecode(
            ShaderCompileRequest request,
            IShaderIncludeResolver includeResolver,
            out ShaderCompileDiagnostic[] diagnostics) {
            ShaderFlags flags = BuildShaderFlags(request.Options);
            ShaderMacro[] macros = BuildMacros(request.Defines);
            string profile = request.ShaderModel.GetProfile(request.Stage);

            using (DirectX11ShaderIncludeAdapter include = new DirectX11ShaderIncludeAdapter(includeResolver, request.Source.Path))
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

                diagnostics = BuildDiagnostics(compilation.Message, request.Source.Path);
                return compilation.Bytecode.Data;
            }
        }

        /// <summary>
        /// Builds the capability metadata describing the shader models and stages FXC accepts for Direct3D 11.
        /// </summary>
        /// <returns>Backend capability metadata.</returns>
        static ShaderBackendCapabilities BuildCapabilities() {
            ShaderModel minModel = new ShaderModel(4, 0);
            ShaderModel maxModel = new ShaderModel(5, 0);
            ShaderStage[] stages = new ShaderStage[] {
                ShaderStage.Vertex,
                ShaderStage.Pixel,
                ShaderStage.Geometry,
                ShaderStage.Hull,
                ShaderStage.Domain,
                ShaderStage.Compute
            };
            return new ShaderBackendCapabilities(minModel, maxModel, stages, false);
        }

        /// <summary>
        /// Builds shader macros for the compiler from the define list.
        /// </summary>
        /// <param name="defines">Define list to convert.</param>
        /// <returns>Array of shader macros.</returns>
        static ShaderMacro[] BuildMacros(IReadOnlyList<ShaderDefine> defines) {
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
        static ShaderFlags BuildShaderFlags(ShaderCompileOptions options) {
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
        static ShaderCompileDiagnostic[] BuildDiagnostics(string message, string sourcePath) {
            if (string.IsNullOrWhiteSpace(message)) {
                return Array.Empty<ShaderCompileDiagnostic>();
            }

            ShaderCompileDiagnostic diagnostic = new ShaderCompileDiagnostic(
                ShaderDiagnosticSeverity.Warning,
                message.Trim(),
                sourcePath,
                0,
                0);
            return new ShaderCompileDiagnostic[] { diagnostic };
        }
    }
}
