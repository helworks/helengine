namespace helengine.editor {
    /// <summary>
    /// Publishes the source-audio extensions supported by the editor host.
    /// </summary>
    public static class AudioImportFormatCatalog {
        /// <summary>
        /// Waveform-audio extensions supported by the Windows audio importer.
        /// </summary>
        public static readonly IReadOnlyList<string> WaveExtensions = [".wav", ".wave"];

        /// <summary>
        /// MPEG audio layer III extensions supported by the Windows audio importer.
        /// </summary>
        public static readonly IReadOnlyList<string> Mp3Extensions = [".mp3"];

        /// <summary>
        /// Combined source-audio extensions supported by the default editor host.
        /// </summary>
        public static readonly IReadOnlyList<string> SourceExtensions = [
            ".wav",
            ".wave",
            ".mp3"
        ];
    }
}
