namespace helengine.video {
    /// <summary>
    /// Built-in definition of one arrangement region: its name, role, planner description and the rectangle it takes on
    /// portrait, square and landscape frames. Rectangles are normalized to the caption-free safe frame (x across the frame
    /// width, y from the safe frame's top to its bottom above the captions), except for a region that fills the whole frame
    /// (a background, or the single region of <c>full</c>).
    /// </summary>
    public sealed class VideoArrangementPresetRegion {
        /// <summary>
        /// Creates one region definition.
        /// </summary>
        /// <param name="name">Region name layers and overlays claim, such as <c>main</c>.</param>
        /// <param name="role">picture, graphic or background.</param>
        /// <param name="description">Planner-facing explanation, including orientation notes.</param>
        /// <param name="portrait">Rectangle on frames taller than wide.</param>
        /// <param name="square">Rectangle on frames close to square.</param>
        /// <param name="landscape">Rectangle on frames wider than tall.</param>
        /// <param name="fillsFrame">Whether the region is the whole frame, captions included, whatever the rectangles say.</param>
        public VideoArrangementPresetRegion(string name, string role, string description, VideoViewport portrait, VideoViewport square, VideoViewport landscape, bool fillsFrame) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Portrait = portrait ?? throw new ArgumentNullException(nameof(portrait));
            Square = square ?? throw new ArgumentNullException(nameof(square));
            Landscape = landscape ?? throw new ArgumentNullException(nameof(landscape));
            FillsFrame = fillsFrame;
        }

        /// <summary>
        /// Gets the region name layers and overlays claim.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets what the region holds: picture, graphic or background.
        /// </summary>
        public string Role { get; }

        /// <summary>
        /// Gets the planner-facing explanation of the region.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the safe-frame rectangle on portrait frames.
        /// </summary>
        public VideoViewport Portrait { get; }

        /// <summary>
        /// Gets the safe-frame rectangle on square frames.
        /// </summary>
        public VideoViewport Square { get; }

        /// <summary>
        /// Gets the safe-frame rectangle on landscape frames.
        /// </summary>
        public VideoViewport Landscape { get; }

        /// <summary>
        /// Gets whether the region is the whole frame rather than a part of the safe frame.
        /// </summary>
        public bool FillsFrame { get; }

        /// <summary>
        /// Picks the rectangle for one frame orientation.
        /// </summary>
        /// <param name="orientation">portrait, square or landscape.</param>
        /// <returns>Safe-frame rectangle.</returns>
        public VideoViewport For(string orientation) {
            return orientation switch {
                VideoArrangementPresets.Portrait => Portrait,
                VideoArrangementPresets.Square => Square,
                VideoArrangementPresets.Landscape => Landscape,
                _ => throw new ArgumentException($"Unknown frame orientation '{orientation}'.", nameof(orientation))
            };
        }
    }
}
