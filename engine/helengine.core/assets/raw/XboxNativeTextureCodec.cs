namespace helengine {
    /// <summary>
    /// Converts native single-level Xbox textures using a strict XTX1 header and GPU-ready payload.
    /// The 48-byte header and native channels are always little-endian, independently of the outer HELE record.
    /// Colors contains the header followed by texels; PaletteColors contains a separate native BGRA8888 palette.
    /// </summary>
    public static class XboxNativeTextureCodec {
        /// <summary>Identifies the four ASCII bytes XTX1 in little-endian word order.</summary>
        public const uint Magic = 0x31585458;
        /// <summary>Gets the version-one fixed header size before GPU texel data.</summary>
        public const int HeaderLength = 48;

        /// <summary>
        /// Encodes RGBA or another decodable generic source without changing its identity or owned buffers.
        /// P8 requires an already-quantized Indexed4 or Indexed8 source, retaining only one GPU byte per index.
        /// Swizzled and DXT storage replicate edge texels to power-of-two extents; linear rows have 64-byte pitch.
        /// </summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string formatId, TextureAssetAlphaPrecision alphaPrecision) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!XboxNativeTextureFormatCatalog.TryGetFormat(formatId, out XboxNativeTextureFormatDefinition format)) {
                throw new ArgumentException("Unknown Xbox texture format identifier.", nameof(formatId));
            }
            if (!format.SupportsAlpha(alphaPrecision)) throw new ArgumentException("Alpha policy is incompatible with the Xbox hardware format.");
            int sourcePaletteEntries = format.IsPaletted ? ValidateIndexedSource(source) : 0;
            XboxNativeTextureLayout layout = new XboxNativeTextureLayout(format, source.Width, source.Height, sourcePaletteEntries);
            byte[] colors = new byte[checked(HeaderLength + layout.TexelLength)];
            byte[] palette = null;
            WriteHeader(colors, layout);
            if (format.IsPaletted) {
                palette = EncodePalette(source.PaletteColors, layout.PaletteLength, alphaPrecision);
                for (int y = 0; y < layout.StorageHeight; y++) {
                    for (int x = 0; x < layout.StorageWidth; x++) {
                        int sourcePixel = Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1);
                        colors[HeaderLength + layout.GetTexelOffset(x, y)] = (byte)ReadIndex(source, sourcePixel);
                    }
                }
            } else {
                byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
                if (format.IsCompressed) {
                    XboxNativeTextureDxtCodec.Encode(rgba, source.Width, source.Height, layout, alphaPrecision, colors, HeaderLength);
                } else if (format.IsYuv) {
                    for (int y = 0; y < source.Height; y++) {
                        XboxNativeTexturePixelCodec.EncodeYuvRow(format.HardwareFormat, rgba, source.Width, y, colors, HeaderLength + y * layout.PitchBytes);
                    }
                } else {
                    for (int y = 0; y < layout.StorageHeight; y++) {
                        for (int x = 0; x < layout.StorageWidth; x++) {
                            int sourcePixel = Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1);
                            XboxNativeTexturePixelCodec.EncodePixel(format.HardwareFormat, rgba, sourcePixel * 4, alphaPrecision,
                                colors, HeaderLength + layout.GetTexelOffset(x, y));
                        }
                    }
                }
            }
            return new TextureAsset {
                Id = source.Id,
                RuntimeAssetId = source.RuntimeAssetId,
                AuthoringAssetId = source.AuthoringAssetId,
                FormerAuthoringAssetIds = CopyFormerAuthoringAssetIds(source.FormerAuthoringAssetIds),
                IsEngineOwned = source.IsEngineOwned,
                Width = source.Width,
                Height = source.Height,
                ColorFormat = TextureAssetColorFormat.XboxNative,
                AlphaPrecision = alphaPrecision,
                Colors = colors,
                PaletteColors = palette
            };
        }

        /// <summary>
        /// Validates the complete header, canonical extents, palette and payload lengths before returning new preview pixels.
        /// Preview depth is normalized grayscale; signed bump components outside unsigned color output clamp to zero.
        /// </summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            XboxNativeTextureLayout layout = ReadLayout(asset);
            byte[] rgba = new byte[checked(asset.Width * asset.Height * 4)];
            int code = layout.Format.HardwareFormat;
            if (layout.Format.IsCompressed) {
                XboxNativeTextureDxtCodec.Decode(asset.Colors, HeaderLength, layout, rgba);
            } else {
                for (int y = 0; y < asset.Height; y++) {
                    for (int x = 0; x < asset.Width; x++) {
                        int target = (y * asset.Width + x) * 4;
                        int offset = HeaderLength + layout.GetTexelOffset(x, y);
                        if (layout.Format.IsPaletted) {
                            int paletteOffset = asset.Colors[offset] * 4;
                            XboxNativeTexturePixelCodec.SetPixel(rgba, target, asset.PaletteColors[paletteOffset + 2],
                                asset.PaletteColors[paletteOffset + 1], asset.PaletteColors[paletteOffset], asset.PaletteColors[paletteOffset + 3]);
                        } else if (layout.Format.IsYuv) {
                            XboxNativeTexturePixelCodec.DecodeYuvPixel(code, asset.Colors, HeaderLength + y * layout.PitchBytes, x, rgba, target);
                        } else {
                            XboxNativeTexturePixelCodec.DecodePixel(code, asset.Colors, offset, rgba, target);
                        }
                    }
                }
            }
            return rgba;
        }

        /// <summary>Parses and validates version-one storage without allocating a decoded image or trusting serialized pitch values.</summary>
        [NativeOwnedReturn]
        public static XboxNativeTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (asset.ColorFormat != TextureAssetColorFormat.XboxNative || asset.Colors == null || asset.Colors.Length < HeaderLength) {
                throw new ArgumentException("A native Xbox texture requires its complete XTX1 header.");
            }
            byte[] colors = asset.Colors;
            if (XboxNativeTexturePixelCodec.ReadUInt32(colors, 0) != Magic || XboxNativeTexturePixelCodec.ReadUInt32(colors, 4) != 1
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 12) != 0) {
                throw new ArgumentException("Unsupported native Xbox texture header, version or flags.");
            }
            uint code = XboxNativeTexturePixelCodec.ReadUInt32(colors, 8);
            if (code > 255 || !XboxNativeTextureFormatCatalog.TryGetHardwareFormat((int)code, out XboxNativeTextureFormatDefinition format)) {
                throw new ArgumentException("Unknown native Xbox texture hardware format.");
            }
            if (!format.SupportsAlpha(asset.AlphaPrecision)) throw new ArgumentException("Native Xbox alpha metadata does not match the hardware format.");
            uint paletteCount = XboxNativeTexturePixelCodec.ReadUInt32(colors, 40);
            if (format.IsPaletted) {
                if (paletteCount != 32 && paletteCount != 64 && paletteCount != 128 && paletteCount != 256) {
                    throw new ArgumentException("Native Xbox P8 palette count must be 32, 64, 128 or 256.");
                }
            } else if (paletteCount != 0) {
                throw new ArgumentException("A non-indexed native Xbox texture cannot carry a palette.");
            }
            XboxNativeTextureLayout layout = new XboxNativeTextureLayout(format, asset.Width, asset.Height, (int)paletteCount);
            if (XboxNativeTexturePixelCodec.ReadUInt32(colors, 16) != (uint)asset.Width
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 20) != (uint)asset.Height
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 24) != (uint)layout.StorageWidth
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 28) != (uint)layout.StorageHeight
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 32) != (uint)layout.PitchBytes
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 36) != (uint)layout.TexelLength
                || XboxNativeTexturePixelCodec.ReadUInt32(colors, 44) != (uint)layout.PaletteLength
                || colors.Length != checked(HeaderLength + layout.TexelLength)) {
                throw new ArgumentException("Native Xbox texture dimensions, storage, pitch or payload length are malformed.");
            }
            int actualPaletteLength = asset.PaletteColors == null ? 0 : asset.PaletteColors.Length;
            if (actualPaletteLength != layout.PaletteLength) throw new ArgumentException("Native Xbox palette payload length does not match its header.");
            if (format.IsPaletted) {
                for (int index = HeaderLength; index < colors.Length; index++) {
                    if (colors[index] >= paletteCount) throw new ArgumentException("Native Xbox texture contains an index outside its palette.");
                }
            }
            return layout;
        }

        /// <summary>Writes the canonical fixed header; version one represents ordinary 2D images with one mip level.</summary>
        static void WriteHeader(byte[] colors, XboxNativeTextureLayout layout) {
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 0, Magic);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 4, 1);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 8, (uint)layout.Format.HardwareFormat);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 12, 0);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 16, (uint)layout.RealWidth);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 20, (uint)layout.RealHeight);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 24, (uint)layout.StorageWidth);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 28, (uint)layout.StorageHeight);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 32, (uint)layout.PitchBytes);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 36, (uint)layout.TexelLength);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 40, (uint)layout.PaletteEntryCount);
            XboxNativeTexturePixelCodec.WriteUInt32(colors, 44, (uint)layout.PaletteLength);
        }

        /// <summary>Validates a prequantized generic source including every palette index before native allocation.</summary>
        static int ValidateIndexedSource(TextureAsset source) {
            if (source.ColorFormat != TextureAssetColorFormat.Indexed4 && source.ColorFormat != TextureAssetColorFormat.Indexed8) {
                throw new ArgumentException("Xbox P8 encoding requires a quantized Indexed4 or Indexed8 source.");
            }
            int expected = TextureAssetPixelCodec.GetPixelByteLength(source.ColorFormat, source.Width, source.Height);
            int entries = source.PaletteColors == null ? 0 : source.PaletteColors.Length / 4;
            int maximum = source.ColorFormat == TextureAssetColorFormat.Indexed4 ? 16 : 256;
            if (source.Colors == null || source.Colors.Length != expected || entries < 1 || entries > maximum
                || source.PaletteColors.Length % 4 != 0) {
                throw new ArgumentException("Xbox P8 source has malformed indices or an invalid RGBA palette.");
            }
            for (int pixel = 0; pixel < source.Width * source.Height; pixel++) {
                if (ReadIndex(source, pixel) >= entries) throw new ArgumentException("Xbox P8 source contains an index outside its palette.");
            }
            return entries;
        }

        /// <summary>Reads legacy generic Indexed4 low-first packing or one native-sized Indexed8 index.</summary>
        static int ReadIndex(TextureAsset source, int pixel) {
            return source.ColorFormat == TextureAssetColorFormat.Indexed4 ? (source.Colors[pixel / 2] >> ((pixel % 2) * 4)) & 15 : source.Colors[pixel];
        }

        /// <summary>Copies and alpha-quantizes RGBA palette entries into padded native BGRA storage.</summary>
        [NativeOwnedReturn]
        static byte[] EncodePalette(byte[] source, int length, TextureAssetAlphaPrecision alphaPrecision) {
            byte[] palette = new byte[length];
            for (int offset = 0; offset < source.Length; offset += 4) {
                XboxNativeTexturePixelCodec.SetPixel(palette, offset, source[offset + 2], source[offset + 1], source[offset],
                    XboxNativeTexturePixelCodec.QuantizeAlpha(source[offset + 3], alphaPrecision));
            }
            return palette;
        }

        /// <summary>Copies identity history into a new array instead of assigning shared arrays to owned asset storage.</summary>
        [NativeOwnedReturn]
        static string[] CopyFormerAuthoringAssetIds(string[] identities) {
            int length = identities == null ? 0 : identities.Length;
            string[] result = new string[length];
            for (int index = 0; index < length; index++) result[index] = identities[index];
            return result;
        }
    }
}
