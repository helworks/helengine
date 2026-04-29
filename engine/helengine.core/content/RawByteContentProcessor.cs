namespace helengine {
    /// <summary>
    /// Reads raw file bytes into <see cref="RawByteContent"/> instances.
    /// </summary>
    public class RawByteContentProcessor : IContentProcessor<RawByteContent> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(RawByteContent);

        /// <summary>
        /// Reads all bytes from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing raw file bytes.</param>
        /// <returns>Loaded raw byte content.</returns>
        public RawByteContent Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using MemoryStream memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return new RawByteContent {
                Bytes = memoryStream.ToArray()
            };
        }

        /// <summary>
        /// Reads all bytes from the supplied stream and returns them boxed as an object.
        /// </summary>
        /// <param name="stream">Stream containing raw file bytes.</param>
        /// <returns>Loaded raw byte content boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
