namespace helengine.player {
    /// <summary>
    /// Time source and waiting primitives the player frame pacer uses. Separating them from the pacer lets the
    /// scheduling math be verified with a simulated clock instead of real sleeps.
    /// </summary>
    public interface IPlayerFrameClock {
        /// <summary>
        /// Gets the monotonic time, in seconds, elapsed since the clock started.
        /// </summary>
        double ElapsedSeconds { get; }

        /// <summary>
        /// Blocks the calling thread for roughly the requested number of milliseconds; the real wait may overshoot.
        /// </summary>
        /// <param name="milliseconds">Positive number of milliseconds to sleep.</param>
        void Sleep(int milliseconds);

        /// <summary>
        /// Busy-waits for a very short moment, used for the final sub-millisecond approach to a frame deadline where
        /// a sleep would be too coarse.
        /// </summary>
        void Spin();
    }
}
