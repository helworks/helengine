namespace helengine.editor {
    /// <summary>
    /// Captures a docking preview result including target area and insertion direction.
    /// </summary>
    public readonly struct DockHint {
        /// <summary>
        /// Initializes a new instance of the <see cref="DockHint"/> struct.
        /// </summary>
        /// <param name="direction">Direction to insert relative to the target.</param>
        /// <param name="anchor">Docked entity whose area is being targeted, or null when docking into an empty layout.</param>
        /// <param name="position">Top-left position of the preview highlight.</param>
        /// <param name="size">Size of the preview highlight.</param>
        /// <param name="splitFraction">Fraction of the target area allocated to the inserted panel.</param>
        public DockHint(
            DockInsertDirection direction,
            DockableEntity? anchor,
            float3 position,
            int2 size,
            float splitFraction = 0.5f) {
            Direction = direction;
            Anchor = anchor;
            Position = position;
            Size = size;
            SplitFraction = splitFraction;
        }

        /// <summary>
        /// Gets the insertion direction relative to the target.
        /// </summary>
        public DockInsertDirection Direction { get; }

        /// <summary>
        /// Gets the currently docked entity whose area is the target for insertion.
        /// </summary>
        public DockableEntity? Anchor { get; }

        /// <summary>
        /// Gets the top-left position of the preview highlight.
        /// </summary>
        public float3 Position { get; }

        /// <summary>
        /// Gets the size of the preview highlight.
        /// </summary>
        public int2 Size { get; }

        /// <summary>
        /// Gets the fraction of the target area that will be allocated to the incoming panel.
        /// </summary>
        public float SplitFraction { get; }
    }
}
