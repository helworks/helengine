namespace helengine.media {
    /// <summary>
    /// Describes one built-in scene arrangement at the contract boundary: a named division of the frame into regions that
    /// a planner assigns a scene's picture, take and graphic layers to. The region geometry is resolved by the engine
    /// per output aspect, so planners only choose the preset and the region names.
    /// </summary>
    public sealed class MediaArrangementDescriptor {
        /// <summary>
        /// Gets or sets the stable preset id edits reference, such as <c>stack</c>.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the explanation planners read to decide when the arrangement fits a scene.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the regions of the arrangement, in drawing order from back to front.
        /// </summary>
        public List<MediaArrangementRegionDescriptor> Regions { get; set; } = [];
    }
}
