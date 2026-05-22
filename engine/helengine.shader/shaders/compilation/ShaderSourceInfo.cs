namespace helengine {
    /// <summary>
    /// Contains the source text and identifying path for a shader program.
    /// </summary>
    public class ShaderSourceInfo {
        /// <summary>
        /// Initializes a new shader source description.
        /// </summary>
        /// <param name="path">Absolute or project-relative path used for include resolution.</param>
        /// <param name="source">HLSL source text.</param>
        public ShaderSourceInfo(string path, string source) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Shader path must be provided.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("Shader source must be provided.", nameof(source));
            }

            Path = path;
            Source = source;
        }

        /// <summary>
        /// Gets the path used to resolve includes and diagnostics.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the HLSL source text.
        /// </summary>
        public string Source { get; }
    }
}
