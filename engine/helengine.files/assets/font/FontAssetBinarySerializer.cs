using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes packaged font assets used by editor exports.
    /// </summary>
    public static class FontAssetBinarySerializer {
        /// <summary>
        /// Shared format identifier for packaged font payloads.
        /// </summary>
        public const ushort FormatId = 1;

        /// <summary>
        /// Record kind used for serialized packaged font payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.FontAsset;

        /// <summary>
        /// Serializer version for the current packaged font payload layout.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Payload endianness used by packaged font assets.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes one font asset to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the packaged font.</param>
        /// <param name="asset">Font asset to serialize.</param>
        public static void Serialize(Stream stream, FontAsset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (asset.FontInfo == null) {
                throw new InvalidOperationException("Packaged font assets must include font metrics.");
            }
            if (asset.SourceTextureAsset == null) {
                throw new InvalidOperationException("Packaged font assets must include raw atlas texture data.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                FormatId,
                (ushort)RecordKind,
                1);

            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);

            writer.WriteString(asset.FontInfo.Name);
            writer.WriteInt32(asset.FontInfo.LineSpacing);
            writer.WriteSingle(asset.FontInfo.SpaceWidth);
            writer.WriteSingle(asset.LineHeight);
            writer.WriteInt32(asset.AtlasWidth);
            writer.WriteInt32(asset.AtlasHeight);
            writer.WriteUInt16(asset.SourceTextureAsset.Width);
            writer.WriteUInt16(asset.SourceTextureAsset.Height);
            writer.WriteByteArray(asset.SourceTextureAsset.Colors);

            KeyValuePair<char, FontChar>[] characters = asset.Characters == null
                ? Array.Empty<KeyValuePair<char, FontChar>>()
                : SortCharactersByKey(asset.Characters);
            writer.WriteInt32(characters.Length);
            for (int index = 0; index < characters.Length; index++) {
                KeyValuePair<char, FontChar> character = characters[index];
                writer.WriteUInt16(character.Key);
                writer.WriteFloat4(character.Value.SourceRect);
                writer.WriteSingle(character.Value.OffsetY);
                writer.WriteSingle(character.Value.AdvanceWidth);
                writer.WriteSingle(character.Value.BearingX);
                writer.WriteSingle(character.Value.BearingY);
            }
        }

        /// <summary>
        /// Deserializes a font asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the packaged font.</param>
        /// <returns>Deserialized font asset.</returns>
        public static FontAsset Deserialize(Stream stream) {
            return global::helengine.FontAssetBinarySerializer.Deserialize(stream);
        }

        /// <summary>
        /// Deserializes a font asset after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized font asset.</returns>
        public static FontAsset Deserialize(Stream stream, EngineBinaryHeader header) {
            return global::helengine.FontAssetBinarySerializer.Deserialize(stream, header);
        }

        static KeyValuePair<char, FontChar>[] SortCharactersByKey(Dictionary<char, FontChar> characters) {
            KeyValuePair<char, FontChar>[] sorted = new KeyValuePair<char, FontChar>[characters.Count];
            int index = 0;

            foreach (KeyValuePair<char, FontChar> entry in characters) {
                sorted[index++] = entry;
            }

            for (int left = 1; left < sorted.Length; left++) {
                KeyValuePair<char, FontChar> current = sorted[left];
                int right = left - 1;
                while (right >= 0 && sorted[right].Key > current.Key) {
                    sorted[right + 1] = sorted[right];
                    right--;
                }

                sorted[right + 1] = current;
            }

            return sorted;
        }
    }
}
