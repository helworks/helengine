namespace helengine.editor {

    /// <summary>
    /// Stores the validated metadata needed to reuse one immutable cooked payload.
    /// </summary>
    public sealed class EditorCookArtifactReceipt {
        /// <summary>
        /// Initializes an artifact receipt.
        /// </summary>
        /// <param name="contentHash">Content hash.</param>
        /// <param name="byteLength">Payload length.</param>
        /// <param name="storePath">Relative store path.</param>
        public EditorCookArtifactReceipt(string contentHash, long byteLength, string storePath) {
            ContentHash = string.IsNullOrWhiteSpace(contentHash) ? throw new ArgumentException("Content hash must be provided.", nameof(contentHash)) : contentHash;
            ByteLength = byteLength < 0 ? throw new ArgumentOutOfRangeException(nameof(byteLength)) : byteLength;
            StorePath = string.IsNullOrWhiteSpace(storePath) ? throw new ArgumentException("Store path must be provided.", nameof(storePath)) : storePath;
        }

        /// <summary>Gets the payload content hash.</summary>
        public string ContentHash { get; }
        /// <summary>Gets the payload length.</summary>
        public long ByteLength { get; }
        /// <summary>Gets the relative store path.</summary>
        public string StorePath { get; }
    }
}
