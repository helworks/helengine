namespace helengine {
    /// <summary>
    /// Reads shader module packages from disk using the engine's HELE asset serialization.
    /// </summary>
    public class ShaderModulePackageReader {
        /// <summary>
        /// Content manager used to load serialized shader package assets.
        /// </summary>
        readonly ContentManager PackageContentManager;

        /// <summary>
        /// Initializes a new package reader rooted at the provided package directory.
        /// </summary>
        /// <param name="rootDirectory">Root directory used to resolve relative package paths.</param>
        public ShaderModulePackageReader(string rootDirectory) {
            if (string.IsNullOrWhiteSpace(rootDirectory)) {
                throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
            }

            PackageContentManager = new ContentManager(rootDirectory);
            PackageContentManager.RegisterProcessor("shader-package", new AssetContentProcessor<ShaderAsset>(), new[] { ".shader.asset" });
        }

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
            return PackageContentManager.Load<ShaderAsset>(packagePath, "shader-package");
        }
    }
}
