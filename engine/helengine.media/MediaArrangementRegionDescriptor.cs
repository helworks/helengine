namespace helengine.media {
    /// <summary>
    /// Describes one region of a scene arrangement: the name layers and overlays use to claim it, the role of what
    /// belongs in it and where it sits on tall, square and wide frames.
    /// </summary>
    public sealed class MediaArrangementRegionDescriptor {
        /// <summary>
        /// Gets or sets the region name used by <c>layers[].region</c> and <c>overlays[].region</c>, such as <c>main</c>.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets what the region is meant to hold: <c>picture</c> (an image or video layer), <c>graphic</c> (a
        /// graphic or text overlay) or <c>background</c> (a full-frame take drawn behind everything else).
        /// </summary>
        public string Role { get; set; } = "";

        /// <summary>
        /// Gets or sets the explanation of the region, including how it moves between portrait and landscape frames.
        /// </summary>
        public string Description { get; set; } = "";
    }
}
