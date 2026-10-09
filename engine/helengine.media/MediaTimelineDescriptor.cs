namespace helengine.media {
    /// <summary>
    /// Describes, at the contract boundary, the overlay timelines edits may author (<c>overlays[].timeline</c>): the
    /// timeline format, the slot kinds a video can bind, the track kinds and value channels it compiles, the curves and
    /// the default element sizes, plus where the full contract is documented. Planners build their timeline schema from it.
    /// </summary>
    public sealed class MediaTimelineDescriptor {
        /// <summary>
        /// Gets or sets the timeline JSON format id, such as <c>helengine.timeline.v1</c>.
        /// </summary>
        public string Format { get; set; } = "";

        /// <summary>
        /// Gets or sets the slot kinds a video binds: <c>text</c>, <c>media</c> and <c>rect</c>.
        /// </summary>
        public List<string> SlotKinds { get; set; } = [];

        /// <summary>
        /// Gets or sets the track kinds a video compiles (other kinds are rejected).
        /// </summary>
        public List<string> TrackKinds { get; set; } = [];

        /// <summary>
        /// Gets or sets the value channels a video compiles, such as <c>opacity</c> and <c>reveal</c> (rect slots only).
        /// </summary>
        public List<string> ValueChannels { get; set; } = [];

        /// <summary>
        /// Gets or sets the catalog curves keyframes may use.
        /// </summary>
        public List<string> Curves { get; set; } = [];

        /// <summary>
        /// Gets or sets the default font size of a bound text, as a fraction of the overlay box height.
        /// </summary>
        public double DefaultTextSize { get; set; }

        /// <summary>
        /// Gets or sets the default height of a bound image or video, as a fraction of the overlay box height.
        /// </summary>
        public double DefaultMediaSize { get; set; }

        /// <summary>
        /// Gets or sets the repository document that specifies overlay timelines.
        /// </summary>
        public string Doc { get; set; } = "";
    }
}
