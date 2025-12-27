namespace helengine {
    /// <summary>
    /// Captures all inputs required to compile a single shader entry point.
    /// </summary>
    public class ShaderCompileRequest {
        /// <summary>
        /// Initializes a new shader compile request.
        /// </summary>
        /// <param name="source">Shader source information.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="stage">Pipeline stage for the entry point.</param>
        /// <param name="target">Backend target to compile for.</param>
        /// <param name="shaderModel">Shader model to compile against.</param>
        /// <param name="variant">Variant name for this compilation.</param>
        /// <param name="defines">Preprocessor defines to apply.</param>
        /// <param name="options">Shared compilation options.</param>
        public ShaderCompileRequest(
            ShaderSourceInfo source,
            string entryPoint,
            ShaderStage stage,
            ShaderCompileTarget target,
            ShaderModel shaderModel,
            string variant,
            IReadOnlyList<ShaderDefine> defines,
            ShaderCompileOptions options) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Entry point must be provided.", nameof(entryPoint));
            }

            if (shaderModel == null) {
                throw new ArgumentNullException(nameof(shaderModel));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant name must be provided.", nameof(variant));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            Source = source;
            EntryPoint = entryPoint;
            Stage = stage;
            Target = target;
            ShaderModel = shaderModel;
            Variant = variant;
            Defines = defines;
            Options = options;
        }

        /// <summary>
        /// Gets the shader source information.
        /// </summary>
        public ShaderSourceInfo Source { get; }

        /// <summary>
        /// Gets the entry point function name.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the pipeline stage for the entry point.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the compilation target backend.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the shader model version to compile against.
        /// </summary>
        public ShaderModel ShaderModel { get; }

        /// <summary>
        /// Gets the variant name for this compilation.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the preprocessor defines applied during compilation.
        /// </summary>
        public IReadOnlyList<ShaderDefine> Defines { get; }

        /// <summary>
        /// Gets the shared compilation options.
        /// </summary>
        public ShaderCompileOptions Options { get; }
    }
}
