namespace helengine.editor {
    /// <summary>
    /// Quantizes RGBA32 texture assets into palette-backed indexed payloads using one shared alpha-aware ranking strategy.
    /// </summary>
    public sealed class TextureAssetIndexedQuantizer {
        /// <summary>
        /// Converts one RGBA32 texture asset into a palette-backed payload using the requested indexed format.
        /// </summary>
        /// <param name="asset">Texture asset to quantize.</param>
        /// <param name="paletteCapacity">Maximum palette size supported by the target format.</param>
        /// <param name="targetFormat">Indexed texture format to produce.</param>
        /// <param name="alphaPrecision">Alpha precision to store in palette entries.</param>
        /// <returns>Palette-backed texture payload.</returns>
        public TextureAsset Quantize(TextureAsset asset, int paletteCapacity, TextureAssetColorFormat targetFormat, TextureAssetAlphaPrecision alphaPrecision) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (paletteCapacity < 1) {
                throw new ArgumentOutOfRangeException(nameof(paletteCapacity), "Palette capacity must be greater than zero.");
            }

            Dictionary<uint, double> histogram = BuildHistogram(asset, alphaPrecision);
            List<uint> rankedColors = RankHistogram(histogram);
            byte[] palette = BuildPalette(rankedColors, paletteCapacity);
            byte[] indices = BuildIndexPayload(asset, palette, targetFormat, alphaPrecision);

            return new TextureAsset {
                Id = asset.Id,
                RuntimeAssetId = asset.RuntimeAssetId,
                Width = asset.Width,
                Height = asset.Height,
                ColorFormat = targetFormat,
                AlphaPrecision = alphaPrecision,
                Colors = indices,
                PaletteColors = palette
            };
        }

        /// <summary>
        /// Builds one weighted color histogram where semi-transparent edge colors receive higher priority than fully opaque colors.
        /// </summary>
        /// <param name="asset">Texture asset whose colors should be counted.</param>
        /// <param name="alphaPrecision">Alpha precision to apply before colors are counted.</param>
        /// <returns>Weighted histogram keyed by packed RGBA values.</returns>
        Dictionary<uint, double> BuildHistogram(TextureAsset asset, TextureAssetAlphaPrecision alphaPrecision) {
            Dictionary<uint, double> histogram = new Dictionary<uint, double>();
            for (int colorIndex = 0; colorIndex < asset.Colors.Length; colorIndex += 4) {
                byte alpha = QuantizeAlpha(asset.Colors[colorIndex + 3], alphaPrecision);
                uint key = PackPaletteKey(asset.Colors[colorIndex], asset.Colors[colorIndex + 1], asset.Colors[colorIndex + 2], alpha);
                double weight = alpha > 0 && alpha < byte.MaxValue ? 8d : 1d;
                if (histogram.TryGetValue(key, out double existingWeight)) {
                    histogram[key] = existingWeight + weight;
                } else {
                    histogram.Add(key, weight);
                }
            }

            return histogram;
        }

        /// <summary>
        /// Orders packed colors by quantization priority so the palette keeps the most important entries first.
        /// </summary>
        /// <param name="histogram">Weighted histogram keyed by packed RGBA values.</param>
        /// <returns>Ranked packed color values.</returns>
        List<uint> RankHistogram(Dictionary<uint, double> histogram) {
            return histogram
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Select(pair => pair.Key)
                .ToList();
        }

        /// <summary>
        /// Builds one fixed-size RGBA palette from the ranked packed colors.
        /// </summary>
        /// <param name="rankedColors">Packed colors ordered by importance.</param>
        /// <param name="paletteCapacity">Maximum number of palette entries supported by the target format.</param>
        /// <returns>Fixed-size RGBA palette bytes.</returns>
        byte[] BuildPalette(List<uint> rankedColors, int paletteCapacity) {
            byte[] palette = new byte[paletteCapacity * 4];
            int paletteEntries = Math.Min(rankedColors.Count, paletteCapacity);
            for (int paletteIndex = 0; paletteIndex < paletteEntries; paletteIndex++) {
                uint packedColor = rankedColors[paletteIndex];
                int colorIndex = paletteIndex * 4;
                palette[colorIndex] = (byte)(packedColor & 0xFF);
                palette[colorIndex + 1] = (byte)((packedColor >> 8) & 0xFF);
                palette[colorIndex + 2] = (byte)((packedColor >> 16) & 0xFF);
                palette[colorIndex + 3] = (byte)((packedColor >> 24) & 0xFF);
            }

            return palette;
        }

        /// <summary>
        /// Builds one indexed texel payload by mapping each source pixel to its closest palette entry.
        /// </summary>
        /// <param name="asset">Source texture asset whose texels should be mapped.</param>
        /// <param name="palette">Palette bytes that should receive texel lookups.</param>
        /// <param name="targetFormat">Indexed texture format that determines payload packing.</param>
        /// <param name="alphaPrecision">Alpha precision to apply before palette lookup.</param>
        /// <returns>Indexed texel payload.</returns>
        byte[] BuildIndexPayload(TextureAsset asset, byte[] palette, TextureAssetColorFormat targetFormat, TextureAssetAlphaPrecision alphaPrecision) {
            int pixelCount = asset.Width * asset.Height;
            byte[] indices = targetFormat == TextureAssetColorFormat.Indexed4
                ? new byte[(pixelCount + 1) / 2]
                : new byte[pixelCount];
            int paletteEntries = palette.Length / 4;
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++) {
                int sourceIndex = pixelIndex * 4;
                byte alpha = QuantizeAlpha(asset.Colors[sourceIndex + 3], alphaPrecision);
                int paletteIndex = FindClosestPaletteIndex(
                    palette,
                    paletteEntries,
                    asset.Colors[sourceIndex],
                    asset.Colors[sourceIndex + 1],
                    asset.Colors[sourceIndex + 2],
                    alpha);
                WritePackedIndex(indices, pixelIndex, paletteIndex, targetFormat);
            }

            return indices;
        }

        /// <summary>
        /// Finds the palette entry whose color distance best matches one source texel, with alpha weighted more heavily for UI edges.
        /// </summary>
        /// <param name="palette">Palette bytes to search.</param>
        /// <param name="paletteEntries">Number of valid palette entries in the palette.</param>
        /// <param name="red">Source red channel.</param>
        /// <param name="green">Source green channel.</param>
        /// <param name="blue">Source blue channel.</param>
        /// <param name="alpha">Source alpha channel.</param>
        /// <returns>Palette index of the closest entry.</returns>
        int FindClosestPaletteIndex(byte[] palette, int paletteEntries, byte red, byte green, byte blue, byte alpha) {
            double bestDistance = double.MaxValue;
            int bestIndex = 0;
            for (int paletteIndex = 0; paletteIndex < paletteEntries; paletteIndex++) {
                int colorIndex = paletteIndex * 4;
                double redDistance = red - palette[colorIndex];
                double greenDistance = green - palette[colorIndex + 1];
                double blueDistance = blue - palette[colorIndex + 2];
                double alphaDistance = (alpha - palette[colorIndex + 3]) * 4d;
                double distance = (redDistance * redDistance)
                    + (greenDistance * greenDistance)
                    + (blueDistance * blueDistance)
                    + (alphaDistance * alphaDistance);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    bestIndex = paletteIndex;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Writes one palette index into the indexed texel payload for the requested target format.
        /// </summary>
        /// <param name="payload">Indexed texel payload being assembled.</param>
        /// <param name="pixelIndex">Pixel index whose palette reference should be written.</param>
        /// <param name="paletteIndex">Palette entry index to write.</param>
        /// <param name="targetFormat">Indexed texture format that determines payload packing.</param>
        void WritePackedIndex(byte[] payload, int pixelIndex, int paletteIndex, TextureAssetColorFormat targetFormat) {
            if (targetFormat == TextureAssetColorFormat.Indexed8) {
                payload[pixelIndex] = (byte)paletteIndex;
                return;
            } else if (targetFormat != TextureAssetColorFormat.Indexed4) {
                throw new InvalidOperationException($"Unsupported indexed texture format '{targetFormat}'.");
            }

            int targetIndex = pixelIndex / 2;
            if ((pixelIndex & 1) == 0) {
                payload[targetIndex] = (byte)((payload[targetIndex] & 0xF0) | (paletteIndex & 0x0F));
            } else {
                payload[targetIndex] = (byte)((payload[targetIndex] & 0x0F) | ((paletteIndex & 0x0F) << 4));
            }
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
        /// Packs one RGBA texel into a 32-bit key used for histogram and palette assembly.
        /// </summary>
        /// <param name="red">8-bit red channel.</param>
        /// <param name="green">8-bit green channel.</param>
        /// <param name="blue">8-bit blue channel.</param>
        /// <param name="alpha">8-bit alpha channel.</param>
        /// <returns>Packed 32-bit color key.</returns>
        uint PackPaletteKey(byte red, byte green, byte blue, byte alpha) {
            return red
                | ((uint)green << 8)
                | ((uint)blue << 16)
                | ((uint)alpha << 24);
        }
    }
}
