namespace helengine {
    /// <summary>
    /// Deserializes protobuf-backed asset files into a specific asset type.
    /// </summary>
    /// <typeparam name="TAsset">Asset type expected from the serialized data.</typeparam>
    public class AssetContentProcessor<TAsset> : IContentProcessor<TAsset> where TAsset : Asset {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(TAsset);

        /// <summary>
        /// Reads a serialized asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing serialized asset data.</param>
        /// <returns>Deserialized asset instance.</returns>
        public TAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            Asset asset = AssetSerializer.Deserialize(stream);
            if (asset is TAsset typedAsset) {
                return typedAsset;
            }

            throw new InvalidOperationException($"Serialized asset did not contain '{typeof(TAsset).Name}'.");
        }

        /// <summary>
        /// Reads a serialized asset from the supplied stream and boxes the result.
        /// </summary>
        /// <param name="stream">Stream containing serialized asset data.</param>
        /// <returns>Deserialized asset instance boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
