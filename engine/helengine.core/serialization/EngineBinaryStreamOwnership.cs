namespace helengine {
    /// <summary>
    /// Selects whether a binary reader only borrows its source stream or assumes responsibility for disposing and deleting it.
    /// </summary>
    internal enum EngineBinaryStreamOwnership {
        /// <summary>
        /// The caller retains stream ownership and must keep it alive for the reader's lifetime.
        /// </summary>
        Borrowed,

        /// <summary>
        /// The reader owns the stream and releases it when the reader is disposed.
        /// </summary>
        Owned
    }
}
