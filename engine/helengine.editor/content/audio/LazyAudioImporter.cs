namespace helengine.editor {
    /// <summary>
    /// Defers creation of an underlying audio importer until the first import request.
    /// </summary>
    public sealed class LazyAudioImporter : IAudioImporter {
        /// <summary>
        /// Factory used to create the underlying importer on demand.
        /// </summary>
        readonly IAudioImporterFactory ImporterFactory;

        /// <summary>
        /// Lazily created importer instance.
        /// </summary>
        readonly Lazy<IAudioImporter> Importer;

        /// <summary>
        /// Initializes a new lazy wrapper around one importer factory.
        /// </summary>
        /// <param name="importerFactory">Factory used to construct the underlying importer.</param>
        public LazyAudioImporter(IAudioImporterFactory importerFactory) {
            ImporterFactory = importerFactory ?? throw new ArgumentNullException(nameof(importerFactory));
            Importer = new Lazy<IAudioImporter>(CreateImporter, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Imports one audio source from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source audio bytes.</param>
        /// <returns>Imported audio metadata and decoded PCM payload.</returns>
        public ImportedAudioSource ImportAudio(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.Value.ImportAudio(stream);
        }

        /// <summary>
        /// Creates the underlying importer and validates the factory result.
        /// </summary>
        /// <returns>Concrete audio importer instance.</returns>
        IAudioImporter CreateImporter() {
            IAudioImporter importer = ImporterFactory.CreateImporter();
            if (importer == null) {
                throw new InvalidOperationException("Audio importer factory returned null.");
            }

            return importer;
        }
    }
}
