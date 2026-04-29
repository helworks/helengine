namespace helengine {
    /// <summary>
    /// Resolves the active index buffer for a model asset and exposes its width metadata.
    /// </summary>
    public class ModelAssetIndexData {
        /// <summary>
        /// Initializes a resolved model index data instance.
        /// </summary>
        /// <param name="uses32BitIndices">True when the model uses a 32-bit index buffer.</param>
        /// <param name="indexCount">Total number of active indices.</param>
        /// <param name="indices16">Resolved 16-bit index buffer when used.</param>
        /// <param name="indices32">Resolved 32-bit index buffer when used.</param>
        ModelAssetIndexData(bool uses32BitIndices, int indexCount, ushort[] indices16, uint[] indices32) {
            Uses32BitIndices = uses32BitIndices;
            IndexCount = indexCount;
            Indices16 = indices16;
            Indices32 = indices32;
        }

        /// <summary>
        /// Gets whether the resolved model uses a 32-bit index buffer.
        /// </summary>
        public bool Uses32BitIndices { get; }

        /// <summary>
        /// Gets the number of indices in the resolved active buffer.
        /// </summary>
        public int IndexCount { get; }

        /// <summary>
        /// Gets the resolved 16-bit index buffer when the model uses 16-bit indices.
        /// </summary>
        public ushort[] Indices16 { get; }

        /// <summary>
        /// Gets the resolved 32-bit index buffer when the model uses 32-bit indices.
        /// </summary>
        public uint[] Indices32 { get; }

        /// <summary>
        /// Resolves the active index buffer for a model asset and validates that exactly one index width is used.
        /// </summary>
        /// <param name="asset">Model asset to inspect.</param>
        /// <returns>Resolved index data for the supplied model asset.</returns>
        public static ModelAssetIndexData Resolve(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            bool hasIndices16 = HasIndices(asset.Indices16);
            bool hasIndices32 = HasIndices(asset.Indices32);
            if (hasIndices16 && hasIndices32) {
                throw new InvalidOperationException("Model assets cannot define both 16-bit and 32-bit index buffers.");
            } else if (hasIndices32) {
                return new ModelAssetIndexData(true, asset.Indices32.Length, null, asset.Indices32);
            } else if (hasIndices16) {
                return new ModelAssetIndexData(false, asset.Indices16.Length, asset.Indices16, null);
            }

            return new ModelAssetIndexData(false, 0, asset.Indices16, asset.Indices32);
        }

        /// <summary>
        /// Determines whether a 16-bit index buffer is populated.
        /// </summary>
        /// <param name="indices">Index buffer to inspect.</param>
        /// <returns>True when the buffer contains at least one index.</returns>
        static bool HasIndices(ushort[] indices) {
            return indices != null && indices.Length > 0;
        }

        /// <summary>
        /// Determines whether a 32-bit index buffer is populated.
        /// </summary>
        /// <param name="indices">Index buffer to inspect.</param>
        /// <returns>True when the buffer contains at least one index.</returns>
        static bool HasIndices(uint[] indices) {
            return indices != null && indices.Length > 0;
        }
    }
}
