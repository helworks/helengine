namespace helengine.editor {
    /// <summary>
    /// Stores cached shader source metadata used to validate compiled packages.
    /// </summary>
    public class ShaderCacheMetadata {
        /// <summary>
        /// Gets or sets the hash of the shader source contents.
        /// </summary>
        public string SourceHash { get; set; }

        /// <summary>
        /// Gets or sets the last write time of the shader source in UTC ticks.
        /// </summary>
        public long SourceWriteTimeUtcTicks { get; set; }

        /// <summary>
        /// Gets or sets the size of the shader source in bytes.
        /// </summary>
        public long SourceLengthBytes { get; set; }
    }
}
