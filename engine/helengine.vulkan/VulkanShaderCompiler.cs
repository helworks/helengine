using Silk.NET.Shaderc;
using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Compiles GLSL source into SPIR-V bytecode for Vulkan runtime resources.
    /// </summary>
    public static unsafe class VulkanShaderCompiler {
        /// <summary>
        /// Compiles GLSL source into SPIR-V bytecode.
        /// </summary>
        /// <param name="source">GLSL source code to compile.</param>
        /// <param name="shaderKind">Shader stage kind understood by shaderc.</param>
        /// <param name="fileName">Logical file name used in diagnostics.</param>
        /// <param name="entryPoint">Shader entry point to export.</param>
        /// <returns>Compiled SPIR-V bytecode payload.</returns>
        public static byte[] CompileGlslToSpirv(string source, ShaderKind shaderKind, string fileName, string entryPoint = "main") {
            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("Shader source must be provided.", nameof(source));
            }

            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(fileName));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Shader entry point must be provided.", nameof(entryPoint));
            }

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
                shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan12);

                CompilationResult* result = shaderc.CompileIntoSpv(
                    compiler,
                    source,
                    (nuint)source.Length,
                    shaderKind,
                    fileName,
                    entryPoint,
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
    }
}
