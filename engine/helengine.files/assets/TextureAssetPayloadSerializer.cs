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
            asset.AlphaPrecision = ReadTextureAssetAlphaPrecision(reader, asset.ColorFormat);
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
            } else if (serializedValue == (byte)TextureAssetColorFormat.Ps1Bgr555) {
                return TextureAssetColorFormat.Ps1Bgr555;
            } else if (serializedValue == (byte)TextureAssetColorFormat.XboxNative) {
                return TextureAssetColorFormat.XboxNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Xbox360Native) {
                return TextureAssetColorFormat.Xbox360Native;
            } else if (serializedValue == (byte)TextureAssetColorFormat.PspNative) {
                return TextureAssetColorFormat.PspNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Nintendo3DsNative) {
                return TextureAssetColorFormat.Nintendo3DsNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.GxNative) {
                return TextureAssetColorFormat.GxNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.NintendoDsNative) {
                return TextureAssetColorFormat.NintendoDsNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.VitaNative) {
                return TextureAssetColorFormat.VitaNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.SwitchNative) {
                return TextureAssetColorFormat.SwitchNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.WiiUNative) {
                return TextureAssetColorFormat.WiiUNative;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Rgba5551) {
                return TextureAssetColorFormat.Rgba5551;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Ia4) {
                return TextureAssetColorFormat.Ia4;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Ia8) {
                return TextureAssetColorFormat.Ia8;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Ia16) {
                return TextureAssetColorFormat.Ia16;
            } else if (serializedValue == (byte)TextureAssetColorFormat.I4) {
                return TextureAssetColorFormat.I4;
            } else if (serializedValue == (byte)TextureAssetColorFormat.I8) {
                return TextureAssetColorFormat.I8;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Yuv16) {
                return TextureAssetColorFormat.Yuv16;
            }

            throw new InvalidOperationException($"Unsupported texture color format '{serializedValue}'.");
        }

        /// <summary>
        /// Reads one serialized texture alpha-precision value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the alpha-precision byte.</param>
        /// <returns>Decoded texture alpha precision.</returns>
        /// <param name="colorFormat">Previously decoded storage format; two-bit alpha is restricted to native Xbox 360 payloads.</param>
        static TextureAssetAlphaPrecision ReadTextureAssetAlphaPrecision(EngineBinaryReader reader, TextureAssetColorFormat colorFormat) {
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
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A2 && (colorFormat == TextureAssetColorFormat.Xbox360Native || colorFormat == TextureAssetColorFormat.VitaNative || colorFormat == TextureAssetColorFormat.SwitchNative || colorFormat == TextureAssetColorFormat.WiiUNative)) {
                return TextureAssetAlphaPrecision.A2;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A3 && (colorFormat == TextureAssetColorFormat.GxNative || colorFormat == TextureAssetColorFormat.NintendoDsNative)) {
                return TextureAssetAlphaPrecision.A3;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A5 && colorFormat == TextureAssetColorFormat.NintendoDsNative) {
                return TextureAssetAlphaPrecision.A5;
            }

            throw new InvalidOperationException($"Unsupported texture alpha precision '{serializedValue}'.");
        }
    }
}
