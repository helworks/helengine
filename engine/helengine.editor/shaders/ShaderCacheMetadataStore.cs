namespace helengine.editor {
    /// <summary>
    /// Reads and writes shader cache metadata files.
    /// </summary>
    public class ShaderCacheMetadataStore {
        /// <summary>
        /// Output directory where shader cache files are stored.
        /// </summary>
        readonly string OutputDirectory;
        /// <summary>
        /// Runtime target used to scope metadata file names.
        /// </summary>
        readonly ShaderCompileTarget RuntimeTarget;

        /// <summary>
        /// Initializes a new metadata store for the specified output directory.
        /// </summary>
        /// <param name="outputDirectory">Output directory for cache data.</param>
        /// <param name="runtimeTarget">Runtime shader target used for metadata naming.</param>
        public ShaderCacheMetadataStore(string outputDirectory, ShaderCompileTarget runtimeTarget) {
            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
            }

            OutputDirectory = outputDirectory;
            RuntimeTarget = runtimeTarget;
        }

        /// <summary>
        /// Attempts to load metadata for the specified shader.
        /// </summary>
        /// <param name="shaderName">Shader name to locate.</param>
        /// <param name="metadata">Loaded metadata instance.</param>
        /// <returns>True when metadata was loaded.</returns>
        public bool TryLoad(string shaderName, out ShaderCacheMetadata metadata) {
            string path = GetMetadataPath(shaderName);
            if (!File.Exists(path)) {
                metadata = null;
                return false;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                metadata = ProtoBuf.Serializer.Deserialize<ShaderCacheMetadata>(stream);
                return metadata != null;
            }
        }

        /// <summary>
        /// Saves metadata for the specified shader.
        /// </summary>
        /// <param name="shaderName">Shader name to write.</param>
        /// <param name="metadata">Metadata to serialize.</param>
        public void Save(string shaderName, ShaderCacheMetadata metadata) {
            if (metadata == null) {
                throw new ArgumentNullException(nameof(metadata));
            }

            string path = GetMetadataPath(shaderName);
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Metadata directory could not be resolved.");
            }

            Directory.CreateDirectory(directory);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) {
                ProtoBuf.Serializer.Serialize(stream, metadata);
            }
        }

        /// <summary>
        /// Deletes metadata for the specified shader if it exists.
        /// </summary>
        /// <param name="shaderName">Shader name to delete.</param>
        public void Delete(string shaderName) {
            string path = GetMetadataPath(shaderName);
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Builds the metadata path for a shader name.
        /// </summary>
        /// <param name="shaderName">Shader name to locate.</param>
        /// <returns>Absolute metadata file path.</returns>
        string GetMetadataPath(string shaderName) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            return ShaderPackagePaths.GetMetadataPath(OutputDirectory, shaderName, RuntimeTarget);
        }
    }
}
