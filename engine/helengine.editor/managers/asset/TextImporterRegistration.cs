namespace helengine.editor {
    /// <summary>
    /// Describes a text importer registration with supported extensions.
    /// </summary>
    public class TextImporterRegistration {
        /// <summary>
        /// Identifier used to reference the importer in settings.
        /// </summary>
        readonly string importerId;

        /// <summary>
        /// Importer implementation used for text assets.
        /// </summary>
        readonly ITextImporter importer;

        /// <summary>
        /// Supported file extensions for this importer.
        /// </summary>
        readonly string[] extensions;

        /// <summary>
        /// Initializes a new text importer registration.
        /// </summary>
        /// <param name="importerId">Stable identifier for the importer.</param>
        /// <param name="importer">Importer implementation.</param>
        /// <param name="extensions">Supported file extensions, including leading dots.</param>
        public TextImporterRegistration(string importerId, ITextImporter importer, IReadOnlyList<string> extensions) {
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
        public ITextImporter Importer => importer;

        /// <summary>
        /// Gets the supported file extensions for this importer.
        /// </summary>
        public IReadOnlyList<string> Extensions => extensions;

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
