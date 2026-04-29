namespace helengine {
    /// <summary>
    /// Stores metadata for a content processor together with the file extensions it can handle.
    /// </summary>
    public class ContentProcessorRegistration {
        /// <summary>
        /// Stable identifier used to address the processor explicitly.
        /// </summary>
        readonly string ProcessorIdValue;
        /// <summary>
        /// Processor instance responsible for parsing content streams.
        /// </summary>
        readonly IContentProcessor ProcessorValue;
        /// <summary>
        /// Normalized file extensions supported by the processor.
        /// </summary>
        readonly string[] ExtensionsValue;

        /// <summary>
        /// Initializes a new processor registration.
        /// </summary>
        /// <param name="processorId">Stable identifier used to select the processor.</param>
        /// <param name="processor">Processor instance that parses the content.</param>
        /// <param name="extensions">Optional supported file extensions, including or omitting the leading dot.</param>
        public ContentProcessorRegistration(string processorId, IContentProcessor processor, IReadOnlyList<string> extensions) {
            if (string.IsNullOrWhiteSpace(processorId)) {
                throw new ArgumentException("Processor id must be provided.", nameof(processorId));
            }
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            ProcessorIdValue = processorId;
            ProcessorValue = processor;
            ExtensionsValue = extensions == null ? Array.Empty<string>() : NormalizeExtensions(extensions);
        }

        /// <summary>
        /// Gets the stable identifier used to select the processor explicitly.
        /// </summary>
        public string ProcessorId => ProcessorIdValue;
        /// <summary>
        /// Gets the processor instance responsible for parsing the content.
        /// </summary>
        public IContentProcessor Processor => ProcessorValue;
        /// <summary>
        /// Gets the output type produced by the processor.
        /// </summary>
        public Type OutputType => ProcessorValue.OutputType;
        /// <summary>
        /// Gets the normalized file extensions supported by the processor.
        /// </summary>
        public IReadOnlyList<string> Extensions => ExtensionsValue;

        /// <summary>
        /// Normalizes extensions to lowercase values with a leading dot.
        /// </summary>
        /// <param name="sourceExtensions">Extensions provided by the caller.</param>
        /// <returns>Normalized extension array.</returns>
        string[] NormalizeExtensions(IReadOnlyList<string> sourceExtensions) {
            string[] normalized = new string[sourceExtensions.Count];
            for (int extensionIndex = 0; extensionIndex < sourceExtensions.Count; extensionIndex++) {
                string extension = sourceExtensions[extensionIndex];
                if (string.IsNullOrWhiteSpace(extension)) {
                    throw new ArgumentException("Extension values must be non-empty.", nameof(sourceExtensions));
                }

                normalized[extensionIndex] = NormalizeExtension(extension);
            }

            return normalized;
        }

        /// <summary>
        /// Normalizes one extension to lowercase text with a leading dot, preserving the wildcard token.
        /// </summary>
        /// <param name="extension">Extension value to normalize.</param>
        /// <returns>Normalized extension.</returns>
        string NormalizeExtension(string extension) {
            if (string.Equals(extension, "*", StringComparison.Ordinal)) {
                return extension;
            }

            if (!extension.StartsWith(".")) {
                extension = "." + extension;
            }

            return extension.ToLowerInvariant();
        }
    }
}
