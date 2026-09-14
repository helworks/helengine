using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes and deserializes the payload body of one editor asset type, so the editor asset format owns a single registry entry per asset type instead of one arm in each of three parallel dispatch chains.
    /// </summary>
    public interface IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind this serializer stamps into the HELE header and answers to when a stored payload is read back.
        /// </summary>
        EditorAssetBinaryValueKind ValueKind { get; }

        /// <summary>
        /// Reports whether this serializer owns the supplied asset instance, using the same runtime type test the hand-written dispatch chain applied so that derived asset types keep resolving to the base type that published their layout.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when this serializer writes the asset's payload.</returns>
        bool Handles(Asset asset);

        /// <summary>
        /// Rejects asset state that cannot produce a deterministic payload, before the header is written and any byte reaches the stream.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        void Validate(Asset asset);

        /// <summary>
        /// Writes the asset's payload body immediately after the shared HELE header.
        /// </summary>
        /// <param name="writer">Destination writer positioned after the header.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        void Write(EngineBinaryWriter writer, Asset asset);

        /// <summary>
        /// Reads one asset payload body produced by <see cref="Write"/>.
        /// </summary>
        /// <param name="reader">Source reader positioned after the header.</param>
        /// <returns>Deserialized asset instance.</returns>
        Asset Read(EngineBinaryReader reader);
    }
}
