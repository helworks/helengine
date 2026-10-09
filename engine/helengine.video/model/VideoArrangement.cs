namespace helengine.video {
    /// <summary>
    /// How a scene divides its frame: a built-in arrangement preset (see <see cref="VideoArrangementPresets"/>) whose
    /// named regions the scene's layers and overlays are assigned to. A scene without an arrangement behaves as
    /// <c>full</c>.
    /// </summary>
    public sealed class VideoArrangement {
        /// <summary>
        /// Arrangement preset id: full, stack, split, take_with_graphic, take_behind_graphic or graphic_only.
        /// </summary>
        public string Preset { get; set; } = "";

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
