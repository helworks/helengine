namespace helengine.timeline {
    /// <summary>
    /// Platform profile settings of <see cref="TimelineCooker"/>.
    /// </summary>
    public sealed class TimelineCookOptions {
        /// <summary>
        /// Gets or sets the ticks per second of the target (for example 60, or 50 for PAL consoles). Between 1 and 1000.
        /// </summary>
        public int TickRate { get; set; } = 60;

        /// <summary>
        /// Gets or sets whether curves stay native or are linearized.
        /// </summary>
        public TimelineCurveMode CurveMode { get; set; } = TimelineCurveMode.Native;

        /// <summary>
        /// Gets or sets the largest error, in value units, accepted when curves or cross-fades are replaced by straight
        /// segments. Checked at every tick for linearized curves.
        /// </summary>
        public double Tolerance { get; set; } = 0.001;

        /// <summary>
        /// Rejects settings the cooker cannot use.
        /// </summary>
        public void Validate() {
            if (TickRate < 1 || TickRate > 1000) {
                throw new ArgumentOutOfRangeException(nameof(TickRate), $"The tick rate must be between 1 and 1000, not {TickRate}.");
            } else if (!(Tolerance > 0) || double.IsInfinity(Tolerance)) {
                throw new ArgumentOutOfRangeException(nameof(Tolerance), "The tolerance must be a positive number.");
            } else if (CurveMode != TimelineCurveMode.Native && CurveMode != TimelineCurveMode.Linearized) {
                throw new ArgumentOutOfRangeException(nameof(CurveMode), $"Unknown curve mode {CurveMode}.");
            }
        }
    }
}
