namespace helengine {
    /// <summary>
    /// Reads shader module packages from disk using protobuf serialization.
    /// </summary>
    public class ShaderModulePackageReader {
        /// <summary>
        /// Reads a shader module package from disk.
        /// </summary>
        /// <param name="packagePath">Package file path to read.</param>
        /// <returns>Loaded shader module package.</returns>
        public ShaderModulePackage Read(string packagePath) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                throw new ArgumentException("Package path must be provided.", nameof(packagePath));
            }

            if (!File.Exists(packagePath)) {
                throw new FileNotFoundException("Shader package was not found.", packagePath);
            }

            string directory = Path.GetDirectoryName(packagePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Package directory could not be resolved.");
            }

            ShaderAsset asset = ReadAsset(packagePath);
            ShaderCompileTarget target = ShaderTargetNames.ParseTarget(asset.TargetName);
            ShaderModuleDefinition definition = asset.BuildDefinition();
            return new ShaderModulePackage(target, directory, definition);
        }

        /// <summary>
        /// Reads a shader asset from the provided package path.
        /// </summary>
        /// <param name="packagePath">Package file path to read.</param>
        /// <returns>Deserialized shader asset.</returns>
        ShaderAsset ReadAsset(string packagePath) {
            using (FileStream stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Asset asset = AssetSerializer.Deserialize(stream);
                if (!(asset is ShaderAsset shaderAsset)) {
                    throw new InvalidOperationException("Package does not contain a shader asset.");
                }

                return shaderAsset;
            }
        }
    }
}
