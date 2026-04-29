namespace helengine {
    /// <summary>
    /// Index mapping for a 3D render list entry.
    /// </summary>
    public struct Index3D {
        /// <summary>
        /// Render pipeline variant placeholder for compatibility (always zero for flat lists).
        /// </summary>
        public int Variant;

        /// <summary>
        /// Render order bucket placeholder for compatibility (always zero for flat lists).
        /// </summary>
        public int Bucket;

        /// <summary>
        /// State bin placeholder for compatibility (always zero for flat lists).
        /// </summary>
        public int Bin;

        /// <summary>
        /// Position inside the flat list.
        /// </summary>
        public int Pos;

        /// <summary>
        /// Initializes a new 3D index.
        /// </summary>
        /// <param name="variant">Variant placeholder.</param>
        /// <param name="bucket">Bucket placeholder.</param>
        /// <param name="bin">Bin placeholder.</param>
        /// <param name="pos">Position inside the list.</param>
        public Index3D(int variant, int bucket, int bin, int pos) {
            Variant = variant;
            Bucket = bucket;
            Bin = bin;
            Pos = pos;
        }
    }
}
