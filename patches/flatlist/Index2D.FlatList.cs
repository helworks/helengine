namespace helengine {
    /// <summary>
    /// Index mapping for a 2D render list entry.
    /// </summary>
    public struct Index2D {
        /// <summary>
        /// Bucket index placeholder for compatibility (always zero for flat lists).
        /// </summary>
        public int Bucket;

        /// <summary>
        /// Position inside the flat list.
        /// </summary>
        public int Pos;

        /// <summary>
        /// Initializes a new 2D index.
        /// </summary>
        /// <param name="bucket">Bucket index placeholder.</param>
        /// <param name="pos">Position inside the list.</param>
        public Index2D(int bucket, int pos) {
            Bucket = bucket;
            Pos = pos;
        }
    }
}
