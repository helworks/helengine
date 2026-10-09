namespace helengine.timeline {
    /// <summary>
    /// Value channels whose meaning, and therefore valid range, is fixed engine-wide. Other channel names are allowed and
    /// only need finite values; their receivers define what they mean.
    /// </summary>
    public static class TimelineKnownChannels {
        /// <summary>
        /// Visibility from transparent (0) to opaque (1).
        /// </summary>
        public const string Opacity = "opacity";

        /// <summary>
        /// Progressive reveal (typing, wipe, strike bar growth) from hidden (0) to complete (1).
        /// </summary>
        public const string Reveal = "reveal";

        /// <summary>
        /// Light or effect intensity; never negative.
        /// </summary>
        public const string Intensity = "intensity";

        /// <summary>
        /// Light or effect range in world units; never negative.
        /// </summary>
        public const string Range = "range";

        /// <summary>
        /// Camera vertical field of view in degrees, between 1 and 179.
        /// </summary>
        public const string FieldOfView = "fov";

        /// <summary>
        /// Looks up the valid inclusive range of a known channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <param name="minimum">Smallest valid value when the channel is known.</param>
        /// <param name="maximum">Largest valid value when the channel is known; positive infinity when unbounded.</param>
        /// <returns>True when the channel has a fixed range.</returns>
        public static bool TryGetRange(string channel, out double minimum, out double maximum) {
            if (channel == Opacity || channel == Reveal) {
                minimum = 0;
                maximum = 1;
                return true;
            } else if (channel == Intensity || channel == Range) {
                minimum = 0;
                maximum = double.PositiveInfinity;
                return true;
            } else if (channel == FieldOfView) {
                minimum = 1;
                maximum = 179;
                return true;
            }
            minimum = double.NegativeInfinity;
            maximum = double.PositiveInfinity;
            return false;
        }
    }
}
