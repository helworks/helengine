namespace helengine.editor {
    /// <summary>
    /// Stores cached shader source metadata used to validate compiled packages.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class ShaderCacheMetadata {
        /// <summary>
        /// Gets or sets the hash of the shader source contents.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string SourceHash { get; set; }

        /// <summary>
        /// Gets or sets the last write time of the shader source in UTC ticks.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public long SourceWriteTimeUtcTicks { get; set; }

        /// <summary>
        /// Gets or sets the size of the shader source in bytes.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public long SourceLengthBytes { get; set; }
    }
}
