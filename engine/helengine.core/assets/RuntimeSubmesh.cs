namespace helengine {
    /// <summary>
    /// Represents one runtime draw range within a GPU-resident model resource.
    /// </summary>
    public sealed class RuntimeSubmesh {
        /// <summary>
        /// Gets or sets the stable material slot name associated with the draw range.
        /// </summary>
        public string MaterialSlotName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the starting index or vertex offset for the draw range.
        /// </summary>
        public int IndexStart { get; set; }

        /// <summary>
        /// Gets or sets the number of indices or vertices contained by the draw range.
        /// </summary>
        public int IndexCount { get; set; }
    }
}
