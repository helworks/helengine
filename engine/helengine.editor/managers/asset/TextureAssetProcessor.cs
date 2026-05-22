namespace helengine.editor {
    /// <summary>
    /// Applies editor-authored texture processor settings to imported texture assets before they are cached.
    /// </summary>
    public sealed class TextureAssetProcessor {
        /// <summary>
        /// Applies one texture processor settings object to one imported texture asset instance.
        /// </summary>
        /// <param name="asset">Imported texture asset to process.</param>
        /// <param name="settings">Processor settings to apply.</param>
        /// <returns>Processed texture asset.</returns>
        public TextureAsset Apply(TextureAsset asset, TextureAssetProcessorSettings settings) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (asset.Width < 1 || asset.Height < 1) {
                throw new InvalidOperationException("Texture assets must have positive dimensions.");
            } else if (asset.Colors == null) {
                throw new InvalidOperationException("Texture assets must include color data.");
            } else if (settings.MaxResolution < 0) {
                throw new InvalidOperationException("Texture max resolution cannot be negative.");
            } else if (!settings.UsesGenericColorFormat()) {
                throw new InvalidOperationException($"Texture color format id '{settings.ColorFormatId}' is platform-owned and cannot be processed by the shared generic texture processor.");
            }

            TextureAsset processedAsset = asset;
            if (settings.MaxResolution > 0 && (processedAsset.Width > settings.MaxResolution || processedAsset.Height > settings.MaxResolution)) {
                processedAsset = ResizeToMaxResolution(processedAsset, settings.MaxResolution);
            }

            if (processedAsset.ColorFormat == settings.ColorFormat && processedAsset.AlphaPrecision == settings.AlphaPrecision) {
                return processedAsset;
            }

            return ConvertColorFormat(processedAsset, settings.ColorFormat, settings.AlphaPrecision);
        }

        /// <summary>
        /// Builds one resized texture asset whose larger axis matches the supplied cap.
        /// </summary>
        /// <param name="asset">Texture asset to resize.</param>
        /// <param name="maxResolution">Maximum allowed width or height.</param>
        /// <returns>Resized texture asset.</returns>
        TextureAsset ResizeToMaxResolution(TextureAsset asset, int maxResolution) {
            double largestDimension = Math.Max(asset.Width, asset.Height);
            double scale = maxResolution / largestDimension;
            int resizedWidth = Math.Max(1, (int)Math.Round(asset.Width * scale));
            int resizedHeight = Math.Max(1, (int)Math.Round(asset.Height * scale));
            byte[] resizedColors = new byte[resizedWidth * resizedHeight * 4];

            for (int y = 0; y < resizedHeight; y++) {
                int sourceY = GetSourceCoordinate(y, resizedHeight, asset.Height);
                for (int x = 0; x < resizedWidth; x++) {
                    int sourceX = GetSourceCoordinate(x, resizedWidth, asset.Width);
                    int sourceIndex = ((sourceY * asset.Width) + sourceX) * 4;
                    int targetIndex = ((y * resizedWidth) + x) * 4;
                    Buffer.BlockCopy(asset.Colors, sourceIndex, resizedColors, targetIndex, 4);
                }
            }

            return new TextureAsset {
                Id = asset.Id,
                RuntimeAssetId = asset.RuntimeAssetId,
                Width = (ushort)resizedWidth,
                Height = (ushort)resizedHeight,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = resizedColors
            };
        }

        /// <summary>
        /// Converts one RGBA32 texture asset into the requested serialized texture format.
        /// </summary>
        /// <param name="asset">Texture asset to convert.</param>
        /// <param name="targetFormat">Serialized texture format to produce.</param>
        /// <param name="alphaPrecision">Alpha precision to store in the processed payload.</param>
        /// <returns>Converted texture asset payload.</returns>
        TextureAsset ConvertColorFormat(TextureAsset asset, TextureAssetColorFormat targetFormat, TextureAssetAlphaPrecision alphaPrecision) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (asset.ColorFormat != TextureAssetColorFormat.Rgba32) {
                throw new InvalidOperationException($"Texture asset processor can only convert from '{TextureAssetColorFormat.Rgba32}'.");
            }

            if (targetFormat == TextureAssetColorFormat.Rgba32) {
                return ApplyAlphaPrecision(asset, alphaPrecision);
            } else if (targetFormat == TextureAssetColorFormat.Rgba4444) {
                return ConvertToRgba4444(asset, alphaPrecision);
            } else if (targetFormat == TextureAssetColorFormat.Indexed4) {
                return ConvertToIndexed(asset, 16, TextureAssetColorFormat.Indexed4, alphaPrecision);
            } else if (targetFormat == TextureAssetColorFormat.Indexed8) {
                return ConvertToIndexed(asset, 256, TextureAssetColorFormat.Indexed8, alphaPrecision);
            }

            throw new InvalidOperationException($"Unsupported texture color format '{targetFormat}'.");
        }

        /// <summary>
        /// Packs one RGBA32 texture asset into RGBA4444 payload bytes.
        /// </summary>
        /// <param name="asset">Texture asset to pack.</param>
        /// <param name="alphaPrecision">Alpha precision to store in the processed payload.</param>
        /// <returns>Packed texture asset payload.</returns>
        TextureAsset ConvertToRgba4444(TextureAsset asset, TextureAssetAlphaPrecision alphaPrecision) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            int pixelCount = asset.Width * asset.Height;
            byte[] packedColors = new byte[pixelCount * 2];
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++) {
                int sourceIndex = pixelIndex * 4;
                ushort packedPixel = PackRgba4444(
                    asset.Colors[sourceIndex],
                    asset.Colors[sourceIndex + 1],
                    asset.Colors[sourceIndex + 2],
                    QuantizeAlpha(asset.Colors[sourceIndex + 3], alphaPrecision));
                int targetIndex = pixelIndex * 2;
                packedColors[targetIndex] = (byte)(packedPixel & 0xFF);
                packedColors[targetIndex + 1] = (byte)((packedPixel >> 8) & 0xFF);
            }

            return new TextureAsset {
                Id = asset.Id,
                RuntimeAssetId = asset.RuntimeAssetId,
                Width = asset.Width,
                Height = asset.Height,
                ColorFormat = TextureAssetColorFormat.Rgba4444,
                AlphaPrecision = alphaPrecision,
                Colors = packedColors
            };
        }

        /// <summary>
        /// Applies one alpha-precision policy to one RGBA32 texture payload without changing its storage format.
        /// </summary>
        /// <param name="asset">Texture asset whose alpha channel should be quantized.</param>
        /// <param name="alphaPrecision">Alpha precision to apply.</param>
        /// <returns>RGBA32 texture payload with quantized alpha values.</returns>
        TextureAsset ApplyAlphaPrecision(TextureAsset asset, TextureAssetAlphaPrecision alphaPrecision) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            byte[] processedColors = new byte[asset.Colors.Length];
            Buffer.BlockCopy(asset.Colors, 0, processedColors, 0, asset.Colors.Length);
            for (int colorIndex = 3; colorIndex < processedColors.Length; colorIndex += 4) {
                processedColors[colorIndex] = QuantizeAlpha(processedColors[colorIndex], alphaPrecision);
            }

            return new TextureAsset {
                Id = asset.Id,
                RuntimeAssetId = asset.RuntimeAssetId,
                Width = asset.Width,
                Height = asset.Height,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = alphaPrecision,
                Colors = processedColors
            };
        }

        /// <summary>
        /// Converts one RGBA32 texture asset into an indexed cooked payload with palette-backed texel storage.
        /// </summary>
        /// <param name="asset">Texture asset to convert.</param>
        /// <param name="paletteCapacity">Maximum number of palette entries allowed by the target format.</param>
        /// <param name="targetFormat">Indexed texture format to produce.</param>
        /// <param name="alphaPrecision">Alpha precision to store in the palette entries.</param>
        /// <returns>Indexed cooked texture payload.</returns>
        TextureAsset ConvertToIndexed(TextureAsset asset, int paletteCapacity, TextureAssetColorFormat targetFormat, TextureAssetAlphaPrecision alphaPrecision) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (paletteCapacity < 1) {
                throw new ArgumentOutOfRangeException(nameof(paletteCapacity), "Palette capacity must be greater than zero.");
            }

            Dictionary<uint, int> paletteIndices = new Dictionary<uint, int>();
            List<byte> paletteColors = new List<byte>(paletteCapacity * 4);
            int pixelCount = asset.Width * asset.Height;
            byte[] indexPayload = targetFormat == TextureAssetColorFormat.Indexed4
                ? new byte[(pixelCount + 1) / 2]
                : new byte[pixelCount];
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++) {
                int sourceIndex = pixelIndex * 4;
                byte alpha = QuantizeAlpha(asset.Colors[sourceIndex + 3], alphaPrecision);
                uint paletteKey = PackPaletteKey(
                    asset.Colors[sourceIndex],
                    asset.Colors[sourceIndex + 1],
                    asset.Colors[sourceIndex + 2],
                    alpha);
                int paletteIndex = GetOrAddPaletteIndex(paletteIndices, paletteColors, paletteCapacity, paletteKey);
                WritePackedIndex(indexPayload, pixelIndex, paletteIndex, targetFormat);
            }

            return new TextureAsset {
                Id = asset.Id,
                RuntimeAssetId = asset.RuntimeAssetId,
                Width = asset.Width,
                Height = asset.Height,
                ColorFormat = targetFormat,
                AlphaPrecision = alphaPrecision,
                Colors = indexPayload,
                PaletteColors = paletteColors.ToArray()
            };
        }

        /// <summary>
        /// Packs one RGBA texel into a 16-bit RGBA4444 word.
        /// </summary>
        /// <param name="red">8-bit red channel.</param>
        /// <param name="green">8-bit green channel.</param>
        /// <param name="blue">8-bit blue channel.</param>
        /// <param name="alpha">8-bit alpha channel.</param>
        /// <returns>Packed RGBA4444 texel.</returns>
        ushort PackRgba4444(byte red, byte green, byte blue, byte alpha) {
            ushort redNibble = (ushort)(red >> 4);
            ushort greenNibble = (ushort)(green >> 4);
            ushort blueNibble = (ushort)(blue >> 4);
            ushort alphaNibble = (ushort)(alpha >> 4);
            return (ushort)(redNibble | (greenNibble << 4) | (blueNibble << 8) | (alphaNibble << 12));
        }

        /// <summary>
        /// Quantizes one 8-bit alpha value to the requested storage precision.
        /// </summary>
        /// <param name="alpha">Authored 8-bit alpha value.</param>
        /// <param name="alphaPrecision">Alpha precision to apply.</param>
        /// <returns>Quantized 8-bit alpha value.</returns>
        byte QuantizeAlpha(byte alpha, TextureAssetAlphaPrecision alphaPrecision) {
            if (alphaPrecision == TextureAssetAlphaPrecision.Opaque) {
                return byte.MaxValue;
            } else if (alphaPrecision == TextureAssetAlphaPrecision.Binary) {
                return alpha >= 128 ? byte.MaxValue : (byte)0;
            } else if (alphaPrecision == TextureAssetAlphaPrecision.A4) {
                return (byte)((alpha & 0xF0) | (alpha >> 4));
            }

            return alpha;
        }

        /// <summary>
        /// Packs one RGBA texel into one palette-key word used for exact indexed-color deduplication.
        /// </summary>
        /// <param name="red">8-bit red channel.</param>
        /// <param name="green">8-bit green channel.</param>
        /// <param name="blue">8-bit blue channel.</param>
        /// <param name="alpha">8-bit alpha channel.</param>
        /// <returns>Packed 32-bit palette key.</returns>
        uint PackPaletteKey(byte red, byte green, byte blue, byte alpha) {
            return red
                | ((uint)green << 8)
                | ((uint)blue << 16)
                | ((uint)alpha << 24);
        }

        /// <summary>
        /// Resolves or appends one palette entry and returns its palette index.
        /// </summary>
        /// <param name="paletteIndices">Known palette-index map keyed by packed RGBA value.</param>
        /// <param name="paletteColors">Palette byte list being assembled.</param>
        /// <param name="paletteCapacity">Maximum allowed number of palette entries.</param>
        /// <param name="paletteKey">Packed RGBA palette key to resolve.</param>
        /// <returns>Palette index for the supplied RGBA entry.</returns>
        int GetOrAddPaletteIndex(Dictionary<uint, int> paletteIndices, List<byte> paletteColors, int paletteCapacity, uint paletteKey) {
            if (paletteIndices == null) {
                throw new ArgumentNullException(nameof(paletteIndices));
            } else if (paletteColors == null) {
                throw new ArgumentNullException(nameof(paletteColors));
            }

            int paletteIndex;
            if (paletteIndices.TryGetValue(paletteKey, out paletteIndex)) {
                return paletteIndex;
            }

            paletteIndex = paletteIndices.Count;
            if (paletteIndex >= paletteCapacity) {
                throw new InvalidOperationException($"Texture required more than {paletteCapacity} palette entries.");
            }

            paletteIndices.Add(paletteKey, paletteIndex);
            paletteColors.Add((byte)(paletteKey & 0xFF));
            paletteColors.Add((byte)((paletteKey >> 8) & 0xFF));
            paletteColors.Add((byte)((paletteKey >> 16) & 0xFF));
            paletteColors.Add((byte)((paletteKey >> 24) & 0xFF));
            return paletteIndex;
        }

        /// <summary>
        /// Writes one palette index into the indexed color payload for the requested target format.
        /// </summary>
        /// <param name="indexPayload">Indexed color payload being assembled.</param>
        /// <param name="pixelIndex">Pixel index whose palette entry should be written.</param>
        /// <param name="paletteIndex">Palette entry index to store.</param>
        /// <param name="targetFormat">Indexed payload format that determines byte packing.</param>
        void WritePackedIndex(byte[] indexPayload, int pixelIndex, int paletteIndex, TextureAssetColorFormat targetFormat) {
            if (indexPayload == null) {
                throw new ArgumentNullException(nameof(indexPayload));
            } else if (pixelIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(pixelIndex), "Pixel index cannot be negative.");
            } else if (paletteIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(paletteIndex), "Palette index cannot be negative.");
            }

            if (targetFormat == TextureAssetColorFormat.Indexed4) {
                int targetIndex = pixelIndex / 2;
                if ((pixelIndex & 1) == 0) {
                    indexPayload[targetIndex] = (byte)(paletteIndex & 0x0F);
                } else {
                    indexPayload[targetIndex] = (byte)(indexPayload[targetIndex] | ((paletteIndex & 0x0F) << 4));
                }
                return;
            } else if (targetFormat == TextureAssetColorFormat.Indexed8) {
                indexPayload[pixelIndex] = (byte)paletteIndex;
                return;
            }

            throw new InvalidOperationException($"Unsupported indexed texture format '{targetFormat}'.");
        }

        /// <summary>
        /// Maps one resized pixel coordinate back to the source texture using nearest-neighbor sampling.
        /// </summary>
        /// <param name="targetCoordinate">Coordinate on the resized axis.</param>
        /// <param name="targetSize">Size of the resized axis.</param>
        /// <param name="sourceSize">Size of the source axis.</param>
        /// <returns>Nearest source pixel coordinate.</returns>
        int GetSourceCoordinate(int targetCoordinate, int targetSize, int sourceSize) {
            if (targetSize < 1) {
                throw new ArgumentOutOfRangeException(nameof(targetSize), "Target size must be greater than zero.");
            } else if (sourceSize < 1) {
                throw new ArgumentOutOfRangeException(nameof(sourceSize), "Source size must be greater than zero.");
            }

            double ratio = (double)sourceSize / targetSize;
            int sourceCoordinate = (int)Math.Floor(targetCoordinate * ratio);
            return Math.Min(sourceSize - 1, sourceCoordinate);
        }
    }
}
