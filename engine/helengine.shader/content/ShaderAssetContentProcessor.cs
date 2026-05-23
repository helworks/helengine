namespace helengine {
    /// <summary>
    /// Deserializes shader asset payloads through the shader-owned asset serializer instead of the core generic asset serializer.
    /// </summary>
    public sealed class ShaderAssetContentProcessor : ShaderContentProcessorBase<ShaderAsset> {
        /// <summary>
        /// Reads one shader asset from the supplied content stream.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized shader payload.</param>
        /// <returns>Deserialized shader asset.</returns>
        public override ShaderAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return ShaderAssetBinarySerializer.Deserialize(stream);
        }

    }
}
