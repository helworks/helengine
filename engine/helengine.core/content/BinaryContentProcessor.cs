namespace helengine {
    /// <summary>
    /// Deserializes a custom binary payload into a specific type using a supplied reader delegate.
    /// </summary>
    /// <typeparam name="T">Type expected from the serialized data.</typeparam>
    public class BinaryContentProcessor<T> : IContentProcessor<T> {
        /// <summary>
        /// Reader delegate used to deserialize the target value from a stream.
        /// </summary>
        readonly Func<Stream, T> Reader;

        /// <summary>
        /// Initializes a new binary content processor with the supplied reader delegate.
        /// </summary>
        /// <param name="reader">Delegate responsible for reading the payload.</param>
        public BinaryContentProcessor(Func<Stream, T> reader) {
            Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(T);

        /// <summary>
        /// Reads the binary payload from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing serialized data.</param>
        /// <returns>Deserialized value.</returns>
        public T Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Reader(stream);
        }

        /// <summary>
        /// Reads the binary payload from the supplied stream and boxes the result.
        /// </summary>
        /// <param name="stream">Stream containing serialized data.</param>
        /// <returns>Deserialized value boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
