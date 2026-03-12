namespace helengine.editor {
    /// <summary>
    /// Identifies the concrete value stored in a shader cache metadata payload.
    /// </summary>
    public enum ShaderCacheMetadataBinaryValueKind : ushort {
        /// <summary>
        /// The payload stores a <see cref="ShaderCacheMetadata"/> instance.
        /// </summary>
        ShaderCacheMetadata = 1
    }
}
