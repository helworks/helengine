namespace helengine {
    /// <summary>
    /// Represents one authored model submesh range and the material slot it targets.
    /// </summary>
    public sealed class ModelSubmeshAsset {
        /// <summary>
        /// Gets or sets the stable material slot name associated with the submesh.
        /// </summary>
        public string MaterialSlotName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the starting index or vertex offset for the submesh draw range.
        /// </summary>
        public int IndexStart { get; set; }

        /// <summary>
        /// Gets or sets the number of indices or vertices contained by the submesh draw range.
        /// </summary>
        public int IndexCount { get; set; }
    }
}
