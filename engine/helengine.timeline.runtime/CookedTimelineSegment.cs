namespace helengine.timeline.runtime {
    /// <summary>
    /// One interpolation span of a cooked transform or value track: from <see cref="StartTick"/> to
    /// <see cref="EndTick"/> the value moves from <see cref="From"/> to <see cref="To"/> along the catalog curve
    /// <see cref="CurveCode"/>. After its end, and until the next segment starts, the track holds <see cref="To"/>.
    /// </summary>
    public sealed class CookedTimelineSegment {
        /// <summary>
        /// Gets or sets the first tick of the span.
        /// </summary>
        public int StartTick { get; set; }

        /// <summary>
        /// Gets or sets the tick at which the span reaches <see cref="To"/>; equal to <see cref="StartTick"/> for a step.
        /// </summary>
        public int EndTick { get; set; }

        /// <summary>
        /// Gets or sets the value at <see cref="StartTick"/>.
        /// </summary>
        public float From { get; set; }

        /// <summary>
        /// Gets or sets the value at <see cref="EndTick"/> and the value held after it.
        /// </summary>
        public float To { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="CurveCatalog"/> byte code of the easing curve (0 is linear; linearized cooks use
        /// only 0).
        /// </summary>
        public byte CurveCode { get; set; }

        /// <summary>
        /// Evaluates the segment. Ticks at or after <see cref="EndTick"/> return <see cref="To"/>; ticks at or before
        /// <see cref="StartTick"/> return <see cref="From"/>.
        /// </summary>
        /// <param name="tick">Timeline tick to sample.</param>
        /// <returns>The interpolated value.</returns>
        public double Evaluate(int tick) {
            if (tick >= EndTick) {
                return To;
            } else if (tick <= StartTick) {
                return From;
            }

            double progress = (double)(tick - StartTick) / (double)(EndTick - StartTick);
            double eased = CurveCatalog.Evaluate(CurveCode, progress);
            return From + ((double)To - From) * eased;
        }
    }
}
