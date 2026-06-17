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
        public const byte CurrentVersion = 5;

        /// <summary>
        /// First packaged font version that stored source-texture runtime ids.
        /// </summary>
        const byte RuntimeTextureIdVersion = 2;

        /// <summary>
        /// First packaged font version that stored explicit texture color formats.
        /// </summary>
        const byte TextureColorFormatVersion = 3;

        /// <summary>
        /// First packaged font version that stored texture alpha precision and palette payloads.
        /// </summary>
        const byte PaletteTextureMetadataVersion = 4;

        /// <summary>
        /// First packaged font version that stored an external cooked atlas texture path.
        /// </summary>
        const byte ExternalCookedAtlasPathVersion = 5;

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
            if (asset.SourceTextureAsset == null && string.IsNullOrWhiteSpace(asset.CookedAtlasTextureRelativePath)) {
                throw new InvalidOperationException("Packaged font assets must include raw atlas texture data or an external cooked atlas texture path.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                FormatId,
                (ushort)RecordKind,
                1);

            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);

            TextureAsset sourceTextureAsset = asset.SourceTextureAsset;
            if (sourceTextureAsset != null && sourceTextureAsset.RuntimeAssetId == 0ul && !string.IsNullOrWhiteSpace(sourceTextureAsset.Id)) {
                sourceTextureAsset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(sourceTextureAsset.Id);
            }

            writer.WriteString(asset.CookedAtlasTextureRelativePath ?? string.Empty);

            if (sourceTextureAsset == null) {
                writer.WriteInt64(0L);
                writer.WriteUInt16(0);
                writer.WriteUInt16(0);
                writer.WriteByte((byte)TextureAssetColorFormat.Rgba32);
                writer.WriteByte((byte)TextureAssetAlphaPrecision.Opaque);
                writer.WriteByteArray(Array.Empty<byte>());
                writer.WriteByteArray(Array.Empty<byte>());
            } else {
                writer.WriteInt64(unchecked((long)sourceTextureAsset.RuntimeAssetId));
                writer.WriteUInt16(sourceTextureAsset.Width);
                writer.WriteUInt16(sourceTextureAsset.Height);
                writer.WriteByte((byte)sourceTextureAsset.ColorFormat);
                writer.WriteByte((byte)sourceTextureAsset.AlphaPrecision);
                writer.WriteByteArray(sourceTextureAsset.PaletteColors);
                writer.WriteByteArray(sourceTextureAsset.Colors);
            }
            writer.WriteString(asset.FontInfo.Name);
            writer.WriteInt32(asset.FontInfo.LineSpacing);
            writer.WriteSingle(asset.FontInfo.SpaceWidth);
            writer.WriteSingle(asset.LineHeight);
            writer.WriteInt32(asset.AtlasWidth);
            writer.WriteInt32(asset.AtlasHeight);

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
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

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

            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            string cookedAtlasTextureRelativePath = string.Empty;
            TextureAsset sourceTexture = new TextureAsset();
            FontInfo fontInfo;
            float lineHeight;
            int atlasWidth;
            int atlasHeight;

            if (header.Version >= ExternalCookedAtlasPathVersion) {
                cookedAtlasTextureRelativePath = reader.ReadString();
                ReadSourceTexture(reader, header, sourceTexture);
                fontInfo = new FontInfo(
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadSingle());
                lineHeight = reader.ReadSingle();
                atlasWidth = reader.ReadInt32();
                atlasHeight = reader.ReadInt32();
            } else {
                fontInfo = new FontInfo(
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadSingle());
                lineHeight = reader.ReadSingle();
                atlasWidth = reader.ReadInt32();
                atlasHeight = reader.ReadInt32();
                ReadSourceTexture(reader, header, sourceTexture);
            }

            int characterCount = reader.ReadInt32();
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>(characterCount);
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

            TextureAsset storedSourceTextureAsset = null;
            if (sourceTexture.Width > 0 && sourceTexture.Height > 0 && sourceTexture.Colors != null && sourceTexture.Colors.Length > 0) {
                storedSourceTextureAsset = sourceTexture;
            }

            return new FontAsset(fontInfo, null, characters, lineHeight, atlasWidth, atlasHeight) {
                SourceTextureAsset = storedSourceTextureAsset,
                CookedAtlasTextureRelativePath = cookedAtlasTextureRelativePath
            };
        }

        /// <summary>
        /// Validates one packaged font header before payload deserialization continues.
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

        /// <summary>
        /// Reads one serialized source-atlas payload from the packaged font stream.
        /// </summary>
        /// <param name="reader">Source reader positioned at the source-atlas payload.</param>
        /// <param name="header">Packaged font header that controls versioned texture metadata.</param>
        /// <param name="sourceTexture">Texture asset instance that receives the decoded payload.</param>
        static void ReadSourceTexture(EngineBinaryReader reader, EngineBinaryHeader header, TextureAsset sourceTexture) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }
            if (sourceTexture == null) {
                throw new ArgumentNullException(nameof(sourceTexture));
            }

            sourceTexture.RuntimeAssetId = header.Version >= RuntimeTextureIdVersion
                ? (ulong)reader.ReadInt64()
                : 0ul;
            sourceTexture.Width = reader.ReadUInt16();
            sourceTexture.Height = reader.ReadUInt16();
            sourceTexture.ColorFormat = header.Version >= TextureColorFormatVersion
                ? ReadTextureAssetColorFormat(reader)
                : TextureAssetColorFormat.Rgba32;
            sourceTexture.AlphaPrecision = header.Version >= PaletteTextureMetadataVersion
                ? ReadTextureAssetAlphaPrecision(reader)
                : GetDefaultTextureAssetAlphaPrecision(sourceTexture.ColorFormat);
            sourceTexture.PaletteColors = header.Version >= PaletteTextureMetadataVersion
                ? reader.ReadByteArray()
                : Array.Empty<byte>();
            sourceTexture.Colors = reader.ReadByteArray();
        }

        /// <summary>
        /// Reads one serialized texture color format from the packaged font payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the serialized color-format byte.</param>
        /// <returns>Decoded texture color format.</returns>
        static TextureAssetColorFormat ReadTextureAssetColorFormat(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte serializedValue = reader.ReadByte();
            if (serializedValue == (byte)TextureAssetColorFormat.Rgba32) {
                return TextureAssetColorFormat.Rgba32;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Rgba4444) {
                return TextureAssetColorFormat.Rgba4444;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Indexed4) {
                return TextureAssetColorFormat.Indexed4;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Indexed8) {
                return TextureAssetColorFormat.Indexed8;
            } else if (serializedValue == (byte)TextureAssetColorFormat.GxRgb5A3) {
                return TextureAssetColorFormat.GxRgb5A3;
            }

            throw new InvalidOperationException($"Unsupported texture color format '{serializedValue}'.");
        }

        /// <summary>
        /// Reads one serialized texture alpha-precision value from the packaged font payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the serialized alpha-precision byte.</param>
        /// <returns>Decoded texture alpha precision.</returns>
        static TextureAssetAlphaPrecision ReadTextureAssetAlphaPrecision(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte serializedValue = reader.ReadByte();
            if (serializedValue == (byte)TextureAssetAlphaPrecision.Opaque) {
                return TextureAssetAlphaPrecision.Opaque;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.Binary) {
                return TextureAssetAlphaPrecision.Binary;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A4) {
                return TextureAssetAlphaPrecision.A4;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A8) {
                return TextureAssetAlphaPrecision.A8;
            }

            throw new InvalidOperationException($"Unsupported texture alpha precision '{serializedValue}'.");
        }

        /// <summary>
        /// Resolves the fallback alpha precision used by older packaged font payloads that predate explicit alpha metadata.
        /// </summary>
        /// <param name="colorFormat">Texture color format decoded from the payload.</param>
        /// <returns>Default alpha precision implied by the color format.</returns>
        static TextureAssetAlphaPrecision GetDefaultTextureAssetAlphaPrecision(TextureAssetColorFormat colorFormat) {
            if (colorFormat == TextureAssetColorFormat.Rgba4444 || colorFormat == TextureAssetColorFormat.Indexed4) {
                return TextureAssetAlphaPrecision.A4;
            }

            return TextureAssetAlphaPrecision.Opaque;
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
