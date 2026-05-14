namespace helengine {
    /// <summary>
    /// Deserializes packaged font assets used by player builds.
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
        public const byte CurrentVersion = 2;

        /// <summary>
        /// Gets the most recent font-deserialization stage reached by the packaged runtime loader.
        /// </summary>
        public static string LastDeserializeStage { get; private set; }

        /// <summary>
        /// Deserializes a font asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the packaged font.</param>
        /// <returns>Deserialized font asset.</returns>
        public static FontAsset Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            LastDeserializeStage = "ReadHeader";
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            return Deserialize(stream, header);
        }

        /// <summary>
        /// Deserializes a font asset after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized font asset.</returns>
        public static FontAsset Deserialize(Stream stream, EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            LastDeserializeStage = "ValidateHeader";
            ValidateHeader(header);
            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("Font assets require an initialized core renderer before deserialization.");
            }

            LastDeserializeStage = "CreateReader";
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            LastDeserializeStage = "ReadFontInfo";
            FontInfo fontInfo = new FontInfo(
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadSingle());

            LastDeserializeStage = "ReadAtlasMetrics";
            float lineHeight = reader.ReadSingle();
            int atlasWidth = reader.ReadInt32();
            int atlasHeight = reader.ReadInt32();

            LastDeserializeStage = "ReadSourceTextureHeader";
            TextureAsset sourceTexture = new TextureAsset();
            sourceTexture.RuntimeAssetId = header.Version >= CurrentVersion
                ? (ulong)reader.ReadInt64()
                : 0ul;
            sourceTexture.Width = reader.ReadUInt16();
            sourceTexture.Height = reader.ReadUInt16();
            LastDeserializeStage = "ReadSourceTextureColors";
            sourceTexture.Colors = reader.ReadByteArray();

            LastDeserializeStage = "ReadCharacterCount";
            int characterCount = reader.ReadInt32();
            LastDeserializeStage = "AllocateCharacterDictionary";
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>(characterCount);
            LastDeserializeStage = "ReadCharacters";
            for (int index = 0; index < characterCount; index++) {
                char character = (char)reader.ReadUInt16();
                FontChar fontChar = new FontChar(
                    reader.ReadFloat4(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle());
                characters.Add(character, fontChar);
            }

            LastDeserializeStage = "BuildRuntimeTexture";
            RuntimeTexture texture = Core.Instance.RenderManager2D.BuildTextureFromRaw(sourceTexture);
            LastDeserializeStage = "ConstructFontAsset";
            FontAsset asset = new FontAsset(fontInfo, texture, characters, lineHeight, atlasWidth, atlasHeight) {
                SourceTextureAsset = sourceTexture
            };
            LastDeserializeStage = "Complete";
            return asset;
        }

        /// <summary>
        /// Validates that the provided header matches the packaged font format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header.FormatId != FormatId) {
                throw new InvalidOperationException($"Unsupported font binary format id '{header.FormatId}'.");
            }
            if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected font record kind '{header.RecordKind}'.");
            }
            if (header.Version < 1 || header.Version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported font binary version '{header.Version}'.");
            }
        }
    }
}
