using helengine;

namespace helengine.files {
    /// <summary>
    /// Holds the leaf payload pieces every editor asset payload shares, so the per-asset-type serializers can be split out of one static class without each of them re-implementing identity and string-element handling.
    /// </summary>
    public static class EditorAssetPayloadPrimitives {
        /// <summary>
        /// Derives the runtime asset id from the authored id when the asset has not been assigned one yet, so a payload never stores a zero runtime id for an identified asset.
        /// </summary>
        /// <param name="asset">Asset whose runtime identity is about to be serialized.</param>
        public static void EnsureRuntimeAssetIdentity(Asset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.RuntimeAssetId != 0ul || string.IsNullOrWhiteSpace(asset.Id)) {
                return;
            }

            asset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(asset.Id);
        }

        /// <summary>
        /// Writes the shared editor-facing and runtime-facing identity for one top-level asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Asset whose identity should be serialized.</param>
        public static void WriteAssetIdentity(EngineBinaryWriter writer, Asset asset) {
            writer.WriteString(asset.Id);
            writer.WriteInt64(unchecked((long)asset.RuntimeAssetId));
            writer.WriteString(asset.AuthoringAssetId ?? string.Empty);
            writer.WriteArray((asset.FormerAuthoringAssetIds ?? Array.Empty<string>())
                .OrderBy(formerAssetId => formerAssetId, StringComparer.Ordinal)
                .ToArray(), WriteStringValue);
        }

        /// <summary>
        /// Reads the shared editor-facing and runtime-facing identity for one top-level asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the asset identity payload.</param>
        /// <param name="asset">Asset instance receiving the deserialized identity.</param>
        public static void ReadAssetIdentity(EngineBinaryReader reader, Asset asset) {
            asset.Id = reader.ReadString();
            asset.RuntimeAssetId = unchecked((ulong)reader.ReadInt64());
            asset.AuthoringAssetId = reader.ReadString();
            asset.FormerAuthoringAssetIds = reader.ReadArray(ReadStringValue) ?? Array.Empty<string>();
        }

        /// <summary>
        /// Writes one string value inside an array payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="value">String value to serialize.</param>
        public static void WriteStringValue(EngineBinaryWriter writer, string value) {
            writer.WriteString(value);
        }

        /// <summary>
        /// Reads one string value from an array payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Deserialized string value.</returns>
        public static string ReadStringValue(EngineBinaryReader reader) {
            return reader.ReadString();
        }
    }
}
