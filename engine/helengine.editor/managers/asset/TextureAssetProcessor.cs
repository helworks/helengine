namespace helengine.editor {
    /// <summary>
    /// Applies editor-authored texture processor settings to imported texture assets before they are cached.
    /// </summary>
    public sealed class TextureAssetProcessor {
        /// <summary>
        /// Shared indexed texture quantizer used whenever one indexed output format is requested.
        /// </summary>
        readonly TextureAssetIndexedQuantizer IndexedQuantizer;

        /// <summary>
        /// Initializes a new texture asset processor instance.
        /// </summary>
        public TextureAssetProcessor() {
            IndexedQuantizer = new TextureAssetIndexedQuantizer();
        }

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

            if (settings.UsesIndexedColorFormat()) {
                settings.ResolveIndexingMethod();
            }

            return ConvertColorFormat(processedAsset, settings);
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
        /// <param name="settings">Texture processor settings that describe the requested output format.</param>
        /// <returns>Converted texture asset payload.</returns>
        TextureAsset ConvertColorFormat(TextureAsset asset, TextureAssetProcessorSettings settings) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (asset.ColorFormat != TextureAssetColorFormat.Rgba32) {
                throw new InvalidOperationException($"Texture asset processor can only convert from '{TextureAssetColorFormat.Rgba32}'.");
            }

            TextureAssetColorFormat targetFormat = settings.ColorFormat;
            TextureAssetAlphaPrecision alphaPrecision = settings.AlphaPrecision;
            if (targetFormat == TextureAssetColorFormat.Rgba32) {
                return ApplyAlphaPrecision(asset, alphaPrecision);
            } else if (targetFormat == TextureAssetColorFormat.Rgba4444) {
                return ConvertToRgba4444(asset, alphaPrecision);
            } else if (targetFormat == TextureAssetColorFormat.Indexed4) {
                return IndexedQuantizer.Quantize(asset, 16, TextureAssetColorFormat.Indexed4, alphaPrecision);
            } else if (targetFormat == TextureAssetColorFormat.Indexed8) {
                return IndexedQuantizer.Quantize(asset, 256, TextureAssetColorFormat.Indexed8, alphaPrecision);
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
