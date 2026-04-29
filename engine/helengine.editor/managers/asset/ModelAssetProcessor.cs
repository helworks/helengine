namespace helengine.editor {
    /// <summary>
    /// Applies editor-side processor settings to imported model assets before they are cached.
    /// </summary>
    public class ModelAssetProcessor {
        /// <summary>
        /// Applies one model processor settings object to one imported model asset instance.
        /// </summary>
        /// <param name="asset">Imported model asset to mutate.</param>
        /// <param name="settings">Processor settings to apply.</param>
        public void Apply(ModelAsset asset, ModelAssetProcessorSettings settings) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!settings.FlipWinding) {
                return;
            }

            FlipTriangleWinding(asset.Indices16);
            FlipTriangleWinding(asset.Indices32);
        }

        /// <summary>
        /// Reverses one 16-bit triangle index buffer in-place by swapping the second and third indices of each triangle.
        /// </summary>
        /// <param name="indices">Index buffer to mutate.</param>
        void FlipTriangleWinding(ushort[] indices) {
            if (indices == null) {
                return;
            }

            for (int i = 0; i + 2 < indices.Length; i += 3) {
                ushort swap = indices[i + 1];
                indices[i + 1] = indices[i + 2];
                indices[i + 2] = swap;
            }
        }

        /// <summary>
        /// Reverses one 32-bit triangle index buffer in-place by swapping the second and third indices of each triangle.
        /// </summary>
        /// <param name="indices">Index buffer to mutate.</param>
        void FlipTriangleWinding(uint[] indices) {
            if (indices == null) {
                return;
            }

            for (int i = 0; i + 2 < indices.Length; i += 3) {
                uint swap = indices[i + 1];
                indices[i + 1] = indices[i + 2];
                indices[i + 2] = swap;
            }
        }
    }
}
