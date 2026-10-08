using helengine.directx11;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// Compiles effect pass shaders for Direct3D 11 at shader model 5.0. Includes resolve first next to the including
    /// file and then against the application directory, so built-in and project shaders alike can
    /// <c>#include "shaders/common/VfxCommon.hlsli"</c> from the engine. Compiled bytecode is cached per file and entry.
    /// </summary>
    public sealed class DirectX11EffectShaderCompiler {
        /// <summary>
        /// Shared compile service with the DirectX 11 backend registered.
        /// </summary>
        readonly ShaderCompileService Service;

        /// <summary>
        /// Initializes a compiler rooted at the application directory.
        /// </summary>
        public DirectX11EffectShaderCompiler() {
            Service = new ShaderCompileService(new ShaderFilesystemIncludeResolver(AppContext.BaseDirectory), new ShaderMemoryCompileCache(), new ShaderSourceHasher());
            Service.RegisterBackend(new DirectX11ShaderBackend());
        }

        /// <summary>
        /// Compiles one entry point of a shader file.
        /// </summary>
        /// <param name="absolutePath">Absolute HLSL file path.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <returns>Compiled bytecode.</returns>
        public byte[] Compile(string absolutePath, string entryPoint, ShaderStage stage) {
            if (!File.Exists(absolutePath)) {
                throw new FileNotFoundException("Effect shader does not exist.", absolutePath);
            }
            ShaderCompileOptions options = new ShaderCompileOptions(ShaderBindingPolicies.Default, generateDebugInfo: false, optimize: true, treatWarningsAsErrors: false);
            ShaderCompileResult result = Service.CompileFromFile(absolutePath, absolutePath + ":" + entryPoint, entryPoint, stage, ShaderCompileTarget.DirectX11, new ShaderModel(5, 0), "default", Array.Empty<ShaderDefine>(), options);
            if (!result.Success) {
                string details = string.Join(Environment.NewLine, result.Diagnostics?.Select(diagnostic => $"{diagnostic.FilePath}({diagnostic.Line},{diagnostic.Column}): {diagnostic.Message}") ?? Array.Empty<string>());
                throw new InvalidOperationException($"Failed to compile '{entryPoint}' in '{absolutePath}'.{Environment.NewLine}{details}".TrimEnd());
            }
            return result.Binary.Bytecode;
        }
    }
}
