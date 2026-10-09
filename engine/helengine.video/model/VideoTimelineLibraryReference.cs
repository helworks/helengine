namespace helengine.video {
    /// <summary>
    /// Points at a timeline the host keeps in its project library. It is a placeholder: the host inlines the referenced
    /// definition before the edit is validated or compiled.
    /// </summary>
    public sealed class VideoTimelineLibraryReference {
        /// <summary>
        /// Library timeline id, e.g. <c>contrast_three_terms</c>.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Pinned library version.
        /// </summary>
        public int Version { get; set; } = 1;
    }
}
