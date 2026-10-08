namespace helengine.media {
    /// <summary>
    /// Describes one registered kinetic typography template at the contract boundary: what a planner may fill (slots),
    /// the typed parameters it accepts and the layouts it can arrange items in. The animation itself stays inside the
    /// engine, exactly like effect shaders.
    /// </summary>
    public sealed class MediaGraphicTemplateDescriptor {
        /// <summary>
        /// Gets or sets the stable template id edits reference, such as <c>contrast_chain</c>.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the positive template contract version edits pin.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name shown in editors.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Gets or sets the explanation planners read to decide when the template fits a line.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the values an edit fills, such as <c>items</c>, <c>separator</c> and <c>accent_item</c>.
        /// </summary>
        public List<MediaGraphicSlotDescriptor> Slots { get; set; } = [];

        /// <summary>
        /// Gets or sets the named data-only parameter descriptors.
        /// </summary>
        public Dictionary<string, MediaParameterDescriptor> Parameters { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the supported layouts, <c>vertical</c> and/or <c>horizontal</c>; edits may also ask for <c>auto</c>.
        /// </summary>
        public List<string> Layouts { get; set; } = [];
    }
}
