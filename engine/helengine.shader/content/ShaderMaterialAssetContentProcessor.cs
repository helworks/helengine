namespace helengine {
    /// <summary>
    /// Deserializes shader-owned raw material payloads through the shader-owned serializer instead of the generic core asset serializer.
    /// </summary>
    public sealed class ShaderMaterialAssetContentProcessor : IContentProcessor<ShaderMaterialAsset> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(ShaderMaterialAsset);

        /// <summary>
        /// Reads one shader-owned raw material asset from the supplied content stream.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized payload.</param>
        /// <returns>Deserialized shader-owned raw material asset.</returns>
        public ShaderMaterialAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return ShaderMaterialAssetBinarySerializer.Deserialize(stream);
        }

        /// <summary>
        /// Reads one shader-owned raw material asset from the supplied content stream and returns it through the non-generic processor contract.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized payload.</param>
        /// <returns>Deserialized shader-owned raw material asset boxed as an object.</returns>
        public object ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
