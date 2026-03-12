namespace helengine {
    /// <summary>
    /// Deserializes protobuf payloads into a specific type.
    /// </summary>
    /// <typeparam name="T">Type expected from the serialized data.</typeparam>
    public class ProtoBufContentProcessor<T> : IContentProcessor<T> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(T);

        /// <summary>
        /// Reads a protobuf payload from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing protobuf data.</param>
        /// <returns>Deserialized value.</returns>
        public T Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return ProtoBuf.Serializer.Deserialize<T>(stream);
        }

        /// <summary>
        /// Reads a protobuf payload from the supplied stream and boxes the result.
        /// </summary>
        /// <param name="stream">Stream containing protobuf data.</param>
        /// <returns>Deserialized value boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
