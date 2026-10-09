namespace helengine.video {
    /// <summary>
    /// An arrangement preset resolved for one output frame: the frame orientation that picked the geometry, the
    /// caption-free safe frame the regions were mapped into and every region with its frame rectangle and pixel size.
    /// </summary>
    public sealed class VideoResolvedArrangement {
        /// <summary>
        /// Creates one resolved arrangement.
        /// </summary>
        /// <param name="preset">Preset id.</param>
        /// <param name="orientation">portrait, square or landscape.</param>
        /// <param name="safeTop">Top of the caption-free safe frame as a fraction of the frame height.</param>
        /// <param name="safeBottom">Bottom of the caption-free safe frame as a fraction of the frame height.</param>
        /// <param name="regions">Resolved regions from back to front.</param>
        public VideoResolvedArrangement(string preset, string orientation, double safeTop, double safeBottom, IReadOnlyList<VideoArrangementRegion> regions) {
            Preset = preset;
            Orientation = orientation;
            SafeTop = safeTop;
            SafeBottom = safeBottom;
            Regions = regions;
        }

        /// <summary>
        /// Gets the preset id.
        /// </summary>
        public string Preset { get; }

        /// <summary>
        /// Gets the frame orientation the geometry was chosen for: portrait, square or landscape.
        /// </summary>
        public string Orientation { get; }

        /// <summary>
        /// Gets the top of the caption-free safe frame as a fraction of the frame height.
        /// </summary>
        public double SafeTop { get; }

        /// <summary>
        /// Gets the bottom of the caption-free safe frame as a fraction of the frame height.
        /// </summary>
        public double SafeBottom { get; }

        /// <summary>
        /// Gets the resolved regions from back to front.
        /// </summary>
        public IReadOnlyList<VideoArrangementRegion> Regions { get; }

        /// <summary>
        /// Finds a resolved region by name.
        /// </summary>
        /// <param name="name">Region name.</param>
        /// <returns>Resolved region.</returns>
        /// <exception cref="InvalidDataException">The arrangement has no region with that name.</exception>
        public VideoArrangementRegion Find(string name) {
            VideoArrangementRegion region = Regions.FirstOrDefault(item => item.Name == name);
            if (region == null) {
                throw new InvalidDataException($"Arrangement '{Preset}' has no region '{name}'.");
            }
            return region;
        }
    }
}
