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
            }

            if (settings.MaxResolution == 0 || (asset.Width <= settings.MaxResolution && asset.Height <= settings.MaxResolution)) {
                return asset;
            }

            return ResizeToMaxResolution(asset, settings.MaxResolution);
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
                Width = (ushort)resizedWidth,
                Height = (ushort)resizedHeight,
                Colors = resizedColors
            };
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
