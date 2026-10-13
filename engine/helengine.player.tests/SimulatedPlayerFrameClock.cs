namespace helengine.player.tests {
    /// <summary>
    /// Simulated player frame clock: time only moves when a test advances it or when the pacer sleeps or spins, so
    /// pacing math is verified deterministically without real waits.
    /// </summary>
    public sealed class SimulatedPlayerFrameClock : IPlayerFrameClock {
        /// <summary>
        /// Simulated seconds one <see cref="Spin"/> call consumes.
        /// </summary>
        public const double SpinStepSeconds = 0.00005d;

        /// <summary>
        /// Initializes the clock at time zero.
        /// </summary>
        /// <param name="sleepOvershootSeconds">Extra simulated seconds every sleep takes beyond the requested time.</param>
        public SimulatedPlayerFrameClock(double sleepOvershootSeconds) {
            SleepOvershootSeconds = sleepOvershootSeconds;
            SleepRequests = new List<int>();
        }

        /// <summary>
        /// Gets the extra simulated seconds every sleep takes beyond the requested time.
        /// </summary>
        public double SleepOvershootSeconds { get; }

        /// <summary>
        /// Gets the current simulated time in seconds.
        /// </summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>
        /// Gets every sleep the pacer requested, in milliseconds, in order.
        /// </summary>
        public List<int> SleepRequests { get; }

        /// <summary>
        /// Gets the number of spin calls the pacer made.
        /// </summary>
        public int SpinCount { get; private set; }

        /// <summary>
        /// Advances simulated time, standing in for the cost of the work a frame does.
        /// </summary>
        /// <param name="seconds">Seconds to advance.</param>
        public void Advance(double seconds) {
            ElapsedSeconds += seconds;
        }

        /// <summary>
        /// Records the sleep and advances time by the requested duration plus the configured overshoot.
        /// </summary>
        /// <param name="milliseconds">Requested sleep in milliseconds.</param>
        public void Sleep(int milliseconds) {
            SleepRequests.Add(milliseconds);
            ElapsedSeconds += milliseconds / 1000d + SleepOvershootSeconds;
        }

        /// <summary>
        /// Records the spin and advances time by <see cref="SpinStepSeconds"/>.
        /// </summary>
        public void Spin() {
            SpinCount++;
            ElapsedSeconds += SpinStepSeconds;
        }
    }
}
