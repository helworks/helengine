namespace helengine {
    /// <summary>
    /// Declares one arrangement a graphic template supports and the spacing between its laid-out blocks.
    /// </summary>
    public class GraphicTemplateLayoutAsset : IDisposable {
        /// <summary>
        /// Gets or sets the stacking direction.
        /// </summary>
        public GraphicLayoutDirection Direction { get; set; } = GraphicLayoutDirection.Vertical;

        /// <summary>
        /// Gets or sets the space between consecutive blocks in ems of the item font size; vertical layouts may use a
        /// negative value to tighten the line box the text renderer reserves above and below the glyphs.
        /// </summary>
        public float Gap { get; set; }

        /// <summary>
        /// Releases nothing; a layout owns no nested allocations.
        /// </summary>
        public void Dispose() { }
    }
}
