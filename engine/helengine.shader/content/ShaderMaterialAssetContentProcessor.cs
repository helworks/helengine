namespace helengine {
    /// <summary>
    /// Deserializes shader-owned raw material payloads through the shader-owned serializer instead of the generic core asset serializer.
    /// </summary>
    public sealed class ShaderMaterialAssetContentProcessor : ShaderContentProcessorBase<ShaderMaterialAsset> {
        /// <summary>
        /// Reads one shader-owned raw material asset from the supplied content stream.
        /// </summary>
        /// <param name="stream">Content stream positioned at the serialized payload.</param>
        /// <returns>Deserialized shader-owned raw material asset.</returns>
        public override ShaderMaterialAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return ShaderMaterialAssetBinarySerializer.Deserialize(stream);
        }

    }
}
