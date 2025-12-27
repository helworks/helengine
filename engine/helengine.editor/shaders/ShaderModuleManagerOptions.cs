namespace helengine.editor {
    /// <summary>
    /// Configures shader module compilation and hot-reload behavior for the editor.
    /// </summary>
    public class ShaderModuleManagerOptions {
        /// <summary>
        /// Initializes a new options container for the shader module manager.
        /// </summary>
        /// <param name="manifestPath">Absolute path to the shader manifest.</param>
        /// <param name="shaderToolPath">Absolute path to the helshader tool.</param>
        /// <param name="buildDelayMilliseconds">Delay window used to coalesce file changes.</param>
        public ShaderModuleManagerOptions(string manifestPath, string shaderToolPath, int buildDelayMilliseconds) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Manifest path must be provided.", nameof(manifestPath));
            }

            if (string.IsNullOrWhiteSpace(shaderToolPath)) {
                throw new ArgumentException("Shader tool path must be provided.", nameof(shaderToolPath));
            }

            if (buildDelayMilliseconds < 1) {
                throw new ArgumentOutOfRangeException(nameof(buildDelayMilliseconds), "Build delay must be at least 1ms.");
            }

            ManifestPath = manifestPath;
            ShaderToolPath = shaderToolPath;
            BuildDelayMilliseconds = buildDelayMilliseconds;
        }

        /// <summary>
        /// Gets the absolute path to the shader manifest.
        /// </summary>
        public string ManifestPath { get; }

        /// <summary>
        /// Gets the absolute path to the helshader tool.
        /// </summary>
        public string ShaderToolPath { get; }

        /// <summary>
        /// Gets the delay window used to coalesce file change events.
        /// </summary>
        public int BuildDelayMilliseconds { get; }
    }
}
