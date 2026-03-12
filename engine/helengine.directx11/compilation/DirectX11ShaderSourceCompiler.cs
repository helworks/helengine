using SharpDX.D3DCompiler;

namespace helengine.directx11 {
    /// <summary>
    /// Compiles DirectX11 shader source by loading the file contents through the content manager.
    /// </summary>
    public static class DirectX11ShaderSourceCompiler {
        /// <summary>
        /// Compiles one shader entry point from source loaded with the content manager.
        /// </summary>
        /// <param name="shaderPath">Absolute or relative shader source path.</param>
        /// <param name="entryPoint">Entry point to compile.</param>
        /// <param name="profile">Shader profile to target.</param>
        /// <returns>Compilation result that owns the shader bytecode.</returns>
        public static CompilationResult CompileFromContent(string shaderPath, string entryPoint, string profile) {
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }
            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Shader entry point must be provided.", nameof(entryPoint));
            }
            if (string.IsNullOrWhiteSpace(profile)) {
                throw new ArgumentException("Shader profile must be provided.", nameof(profile));
            }

            string fullPath = Path.GetFullPath(shaderPath);
            string shaderDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(shaderDirectory)) {
                throw new InvalidOperationException("Shader directory could not be resolved.");
            }

            ContentManager contentManager = new ContentManager(shaderDirectory);
            TextContent sourceContent = contentManager.Load<TextContent>(fullPath);
            string source = sourceContent.Text;
            ShaderFilesystemIncludeResolver includeResolver = new ShaderFilesystemIncludeResolver(shaderDirectory);
            using var include = new DirectX11ShaderIncludeAdapter(includeResolver, fullPath);
            CompilationResult result = ShaderBytecode.Compile(
                source,
                entryPoint,
                profile,
                ShaderFlags.None,
                EffectFlags.None,
                null,
                include,
                fullPath);
            if (result == null) {
                throw new InvalidOperationException("Shader compilation produced no result.");
            }

            if (result.HasErrors) {
                string message = result.Message;
                result.Dispose();
                throw new InvalidOperationException($"Shader compilation failed: {message}");
            }

            return result;
        }
    }
}
