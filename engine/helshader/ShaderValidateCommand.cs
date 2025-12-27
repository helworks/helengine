namespace helshader {
    /// <summary>
    /// Validates shader manifests without compiling outputs.
    /// </summary>
    public class ShaderValidateCommand {
        /// <summary>
        /// Runs manifest validation.
        /// </summary>
        /// <param name="options">Command options.</param>
        public void Execute(ShaderCommandOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.ManifestPath)) {
                throw new InvalidOperationException("Manifest path is required.");
            }

            string manifestPath = Path.GetFullPath(options.ManifestPath);
            ShaderManifestLoader loader = new ShaderManifestLoader();
            loader.Load(manifestPath);
        }
    }
}
