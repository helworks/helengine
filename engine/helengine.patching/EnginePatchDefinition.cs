namespace helengine.patching {
    /// <summary>
    /// Associates a patch manifest with its location on disk.
    /// </summary>
    public sealed class EnginePatchDefinition {
        /// <summary>
        /// Initializes a new patch definition for the provided manifest.
        /// </summary>
        /// <param name="manifest">Parsed patch manifest.</param>
        /// <param name="manifestPath">Path to the manifest file.</param>
        /// <param name="rootPath">Root folder for the patch files.</param>
        public EnginePatchDefinition(EnginePatchManifest manifest, string manifestPath, string rootPath) {
            Manifest = manifest ?? new EnginePatchManifest();
            Manifest.Normalize();
            ManifestPath = manifestPath ?? string.Empty;
            RootPath = rootPath ?? string.Empty;
        }

        /// <summary>
        /// Gets the patch identifier.
        /// </summary>
        public string Id => Manifest.Id;

        /// <summary>
        /// Gets the patch version.
        /// </summary>
        public string Version => Manifest.Version;

        /// <summary>
        /// Gets the parsed patch manifest.
        /// </summary>
        public EnginePatchManifest Manifest { get; }

        /// <summary>
        /// Gets the manifest file path.
        /// </summary>
        public string ManifestPath { get; }

        /// <summary>
        /// Gets the root folder for patch files.
        /// </summary>
        public string RootPath { get; }
    }
}
