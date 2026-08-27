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
        public const byte CurrentVersion = 5;

        /// <summary>
        /// Value kind used for serialized packaged font payloads.
        /// </summary>
        public const ushort ValueKind = 1;

        /// <summary>
        /// Gets the most recent font-deserialization stage reached by the packaged runtime loader.
        /// </summary>
        public static string LastDeserializeStage { get; private set; } = string.Empty;

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
            try {
                return Deserialize(stream, header);
            } finally {
                NativeOwnership.Delete(header);
            }
        }

        /// <summary>
        /// Deserializes a font asset after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized font asset.</returns>
        public static FontAsset Deserialize(Stream stream, [NativeNoEscape] EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            ValidateHeader(header);
            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("Font assets require an initialized core renderer before deserialization.");
            }

            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            string cookedAtlasTextureRelativePath;
            FontInfo fontInfo;
            float lineHeight;
            int atlasWidth;
            int atlasHeight;
            ulong sourceTextureRuntimeAssetId;
            ushort sourceTextureWidth;
            ushort sourceTextureHeight;
            TextureAssetColorFormat sourceTextureColorFormat;
            TextureAssetAlphaPrecision sourceTextureAlphaPrecision;
            byte[] sourceTexturePaletteColors;
            byte[] sourceTextureColors;

            cookedAtlasTextureRelativePath = reader.ReadString();
            sourceTextureRuntimeAssetId = (ulong)reader.ReadInt64();
            sourceTextureWidth = reader.ReadUInt16();
            sourceTextureHeight = reader.ReadUInt16();
            sourceTextureColorFormat = ReadTextureAssetColorFormat(reader);
            sourceTextureAlphaPrecision = ReadTextureAssetAlphaPrecision(reader);
            sourceTexturePaletteColors = reader.ReadByteArray();
            sourceTextureColors = reader.ReadByteArray();

            fontInfo = new FontInfo(
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadSingle());

            lineHeight = reader.ReadSingle();
            atlasWidth = reader.ReadInt32();
            atlasHeight = reader.ReadInt32();

            TextureAsset sourceTexture = new TextureAsset {
                RuntimeAssetId = sourceTextureRuntimeAssetId,
                Width = sourceTextureWidth,
                Height = sourceTextureHeight,
                ColorFormat = sourceTextureColorFormat,
                AlphaPrecision = sourceTextureAlphaPrecision,
                PaletteColors = sourceTexturePaletteColors,
                Colors = sourceTextureColors
            };

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

            if (sourceTexture.Width > 0 && sourceTexture.Height > 0 && sourceTexture.Colors != null && sourceTexture.Colors.Length > 0) {
                RuntimeTexture texture = Core.Instance.RenderManager2D.BuildTextureFromRaw(sourceTexture);
                return new FontAsset(fontInfo, texture, characters, lineHeight, atlasWidth, atlasHeight) {
                    SourceTextureAsset = sourceTexture,
                    CookedAtlasTextureRelativePath = cookedAtlasTextureRelativePath
                };
            }

            NativeOwnership.DisposeAndDelete(sourceTexture);
            return new FontAsset(fontInfo, null, characters, lineHeight, atlasWidth, atlasHeight) {
                CookedAtlasTextureRelativePath = cookedAtlasTextureRelativePath
            };
        }

        /// <summary>
        /// Validates that the provided header matches the packaged font format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader([NativeNoEscape] EngineBinaryHeader header) {
            if (header.FormatId != FormatId) {
                throw new InvalidOperationException($"Unsupported font binary format id '{header.FormatId}'.");
            }
            if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected font record kind '{header.RecordKind}'.");
            }
            if (header.ValueKind != ValueKind) {
                throw new InvalidOperationException($"Unexpected font value kind '{header.ValueKind}'.");
            }
            if (header.Version != CurrentVersion) {
                throw new InvalidOperationException(
                    $"Packaged font version '{header.Version}' is unsupported; version '{CurrentVersion}' is required. Regenerate the packaged font asset.");
            }
        }

        /// <summary>
        /// Reads one serialized texture color-format value from the packaged font payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the texture format byte.</param>
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
        /// <param name="reader">Source reader positioned at the texture alpha-precision byte.</param>
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

    }
}
