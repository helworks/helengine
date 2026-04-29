namespace helengine {
    /// <summary>
    /// Writes shader module packages to disk using the engine's HELE asset serialization.
    /// </summary>
    public class ShaderModulePackageWriter {
        /// <summary>
        /// Writes a shader module package to disk.
        /// </summary>
        /// <param name="packagePath">Destination file path for the package.</param>
        /// <param name="definition">Shader module definition to serialize.</param>
        /// <param name="target">Target backend to include.</param>
        public void Write(string packagePath, ShaderModuleDefinition definition, ShaderCompileTarget target) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                throw new ArgumentException("Package path must be provided.", nameof(packagePath));
            }

            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            string directory = Path.GetDirectoryName(packagePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Package directory could not be resolved.");
            }

            Directory.CreateDirectory(directory);
            ShaderAsset asset = ShaderAsset.FromDefinition(definition, target);
            using (FileStream stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }
        }
    }
}
