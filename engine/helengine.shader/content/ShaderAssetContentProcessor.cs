namespace helengine {
    /// <summary>
    /// Deserializes shader asset payloads through the shader-owned asset serializer instead of the core generic asset serializer.
    /// </summary>
    public sealed class ShaderAssetContentProcessor : IContentProcessor<ShaderAsset> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(ShaderAsset);

        /// <summary>
        /// Reads one shader asset from the supplied content stream.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized shader payload.</param>
        /// <returns>Deserialized shader asset.</returns>
        public ShaderAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return ShaderAssetBinarySerializer.Deserialize(stream);
        }

        /// <summary>
        /// Reads one shader asset from the supplied content stream and returns it through the non-generic processor contract.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized shader payload.</param>
        /// <returns>Deserialized shader asset boxed as an object.</returns>
        public object ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
