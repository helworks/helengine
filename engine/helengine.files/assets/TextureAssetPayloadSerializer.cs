using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a texture asset for the editor asset format, including the strict decoding of the stored color-format and alpha-precision bytes.
    /// </summary>
    public sealed class TextureAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for texture asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.TextureAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a texture asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a texture asset.</returns>
        public bool Handles(Asset asset) {
            return asset is TextureAsset;
        }

        /// <summary>
        /// Performs no validation because a texture payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes a texture asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Texture asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            TextureAsset textureAsset = (TextureAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(textureAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, textureAsset);
            writer.WriteUInt16(textureAsset.Width);
            writer.WriteUInt16(textureAsset.Height);
            writer.WriteByte((byte)textureAsset.ColorFormat);
            writer.WriteByte((byte)textureAsset.AlphaPrecision);
            writer.WriteByteArray(textureAsset.PaletteColors);
            writer.WriteByteArray(textureAsset.Colors);
        }

        /// <summary>
        /// Reads a texture asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized texture asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            TextureAsset asset = new TextureAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.Width = reader.ReadUInt16();
            asset.Height = reader.ReadUInt16();
            asset.ColorFormat = ReadTextureAssetColorFormat(reader);
            asset.AlphaPrecision = ReadTextureAssetAlphaPrecision(reader);
            asset.PaletteColors = reader.ReadByteArray();
            asset.Colors = reader.ReadByteArray();
            return asset;
        }

        /// <summary>
        /// Reads one serialized texture color-format value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the format byte.</param>
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
        /// Reads one serialized texture alpha-precision value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the alpha-precision byte.</param>
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
