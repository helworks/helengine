namespace helengine.editor {
    /// <summary>
    /// Describes a texture importer registration with supported extensions.
    /// </summary>
    public class TextureImporterRegistration : IAssetImporterRegistration {
        /// <summary>
        /// Identifier used to reference the importer in settings.
        /// </summary>
        readonly string importerId;

        /// <summary>
        /// Importer implementation used for texture assets.
        /// </summary>
        readonly ITextureImporter importer;

        /// <summary>
        /// Supported file extensions for this importer.
        /// </summary>
        readonly string[] extensions;

        /// <summary>
        /// Initializes a new texture importer registration.
        /// </summary>
        /// <param name="importerId">Stable identifier for the importer.</param>
        /// <param name="importer">Importer implementation.</param>
        /// <param name="extensions">Supported file extensions, including leading dots.</param>
        public TextureImporterRegistration(string importerId, ITextureImporter importer, IReadOnlyList<string> extensions) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            if (importer == null) {
                throw new ArgumentNullException(nameof(importer));
            }

            if (extensions == null) {
                throw new ArgumentNullException(nameof(extensions));
            }

            if (extensions.Count == 0) {
                throw new ArgumentException("At least one extension must be provided.", nameof(extensions));
            }

            this.importerId = importerId;
            this.importer = importer;
            this.extensions = NormalizeExtensions(extensions);
        }

        /// <summary>
        /// Gets the identifier used to reference the importer in settings.
        /// </summary>
        public string ImporterId => importerId;

        /// <summary>
        /// Gets the importer implementation.
        /// </summary>
        public ITextureImporter Importer => importer;

        /// <summary>
        /// Gets the supported file extensions for this importer.
        /// </summary>
        public IReadOnlyList<string> Extensions => extensions;

        /// <summary>
        /// Registers the importer with an asset import manager.
        /// </summary>
        /// <param name="manager">Manager to register with.</param>
        public void Register(AssetImportManager manager) {
            if (manager == null) {
                throw new ArgumentNullException(nameof(manager));
            }

            manager.RegisterTextureImporter(this);
        }

        /// <summary>
        /// Normalizes extension strings to include a leading dot and lowercase text.
        /// </summary>
        /// <param name="sourceExtensions">Extensions provided by the registration.</param>
        /// <returns>Normalized extension array.</returns>
        string[] NormalizeExtensions(IReadOnlyList<string> sourceExtensions) {
            string[] normalized = new string[sourceExtensions.Count];
            for (int i = 0; i < sourceExtensions.Count; i++) {
                string extension = sourceExtensions[i];
                if (string.IsNullOrWhiteSpace(extension)) {
                    throw new ArgumentException("Extension values must be non-empty.", nameof(sourceExtensions));
                }

                if (!extension.StartsWith(".")) {
                    extension = "." + extension;
                }

                normalized[i] = extension.ToLowerInvariant();
            }

            return normalized;
        }
    }
}
