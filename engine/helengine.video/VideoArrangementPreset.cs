namespace helengine.video {
    /// <summary>
    /// One built-in scene arrangement: its id, the description planners read and its regions from back to front.
    /// </summary>
    public sealed class VideoArrangementPreset {
        /// <summary>
        /// Creates one arrangement preset.
        /// </summary>
        /// <param name="id">Stable preset id.</param>
        /// <param name="description">Planner-facing explanation of when to use it.</param>
        /// <param name="regions">Regions from back to front.</param>
        public VideoArrangementPreset(string id, string description, IReadOnlyList<VideoArrangementPresetRegion> regions) {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        }

        /// <summary>
        /// Gets the stable preset id edits reference.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the planner-facing explanation of when to use the arrangement.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the regions from back to front.
        /// </summary>
        public IReadOnlyList<VideoArrangementPresetRegion> Regions { get; }

        /// <summary>
        /// Finds a region definition by name.
        /// </summary>
        /// <param name="name">Region name.</param>
        /// <returns>Region definition, or null when the arrangement has no such region.</returns>
        public VideoArrangementPresetRegion Find(string name) {
            return Regions.FirstOrDefault(region => region.Name == name);
        }
    }
}
