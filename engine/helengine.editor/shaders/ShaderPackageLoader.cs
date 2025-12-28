namespace helengine.editor {
    /// <summary>
    /// Loads serialized shader packages for use by the editor.
    /// </summary>
    public class ShaderPackageLoader {
        /// <summary>
        /// Stores the package reader used for deserialization.
        /// </summary>
        readonly ShaderModulePackageReader reader;

        /// <summary>
        /// Initializes a new shader package loader.
        /// </summary>
        /// <param name="reader">Package reader used for deserialization.</param>
        public ShaderPackageLoader(ShaderModulePackageReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            this.reader = reader;
        }

        /// <summary>
        /// Loads a shader package from disk.
        /// </summary>
        /// <param name="packagePath">Package file path to load.</param>
        /// <returns>Loaded shader package handle.</returns>
        public ShaderPackageHandle Load(string packagePath) {
            ShaderModulePackage package = reader.Read(packagePath);
            return new ShaderPackageHandle(package, packagePath);
        }
    }
}
