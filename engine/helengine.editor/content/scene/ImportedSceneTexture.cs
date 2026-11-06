namespace helengine.editor {
    /// <summary>
    /// One texture image a scene material refers to: either encoded image bytes embedded in the source file (for example inside a .glb)
    /// or an external image file next to the source.
    /// </summary>
    public sealed class ImportedSceneTexture {
        /// <summary>
        /// Creates the immutable ImportedSceneTexture; exactly one of <paramref name="encodedBytes"/> and <paramref name="externalRelativePath"/> must be provided.
        /// </summary>
        /// <param name="name">File-name-safe base name of the image, without extension.</param>
        /// <param name="formatHint">Lowercase image format without a dot: "png" or "jpg".</param>
        /// <param name="encodedBytes">Encoded image bytes for an embedded texture, empty for an external one.</param>
        /// <param name="externalRelativePath">Forward-slash path of an external image, resolved against the folder of the source file; empty for an embedded texture.</param>
        public ImportedSceneTexture(string name, string formatHint, byte[] encodedBytes, string externalRelativePath) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Texture name must be provided.", nameof(name));
            } else if (string.IsNullOrWhiteSpace(formatHint)) {
                throw new ArgumentException("Texture format hint must be provided.", nameof(formatHint));
            }

            Name = name;
            FormatHint = formatHint;
            EncodedBytes = encodedBytes ?? throw new ArgumentNullException(nameof(encodedBytes));
            ExternalRelativePath = externalRelativePath ?? throw new ArgumentNullException(nameof(externalRelativePath));
            if ((EncodedBytes.Length == 0) == (ExternalRelativePath.Length == 0)) {
                throw new ArgumentException($"Texture '{name}' must be either embedded (encoded bytes) or external (relative path), not both or neither.");
            }
        }

        /// <summary>
        /// Gets the file-name-safe base name of the image, without extension.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the lowercase image format without a dot: "png" or "jpg".
        /// </summary>
        public string FormatHint { get; }

        /// <summary>
        /// Gets the encoded image bytes of an embedded texture; empty for an external texture.
        /// </summary>
        public byte[] EncodedBytes { get; }

        /// <summary>
        /// Gets the forward-slash path of an external image, resolved against the folder of the source file; empty for an embedded texture.
        /// </summary>
        public string ExternalRelativePath { get; }

        /// <summary>
        /// Gets a value indicating whether the image bytes live inside the source file.
        /// </summary>
        public bool IsEmbedded => EncodedBytes.Length > 0;
    }
}
