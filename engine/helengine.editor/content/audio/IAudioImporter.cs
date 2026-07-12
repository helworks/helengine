namespace helengine.editor {
    /// <summary>
    /// Provides a contract for importing authored audio sources from arbitrary streams.
    /// </summary>
    public interface IAudioImporter {
        /// <summary>
        /// Imports one audio source from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing authored audio bytes.</param>
        /// <returns>Imported audio metadata and decoded PCM payload.</returns>
        ImportedAudioSource ImportAudio(Stream stream);
    }
}
