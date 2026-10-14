namespace helengine.player {
    /// <summary>
    /// Paces the player frame loop to a fixed target frame rate (the selected platform's rate, e.g. 60 FPS for PS2)
    /// instead of the monitor refresh rate. Frame deadlines advance on a fixed cadence from <see cref="Start"/>, so
    /// rounding in individual waits does not accumulate into drift. Each wait sleeps for most of the remaining time
    /// and spins through the last <see cref="SpinThresholdSeconds"/> (plus any sub-millisecond remainder), because
    /// sleeps are only millisecond-accurate and may overshoot.
    /// When a frame overruns its slot by a full frame or more the cadence restarts from the current time instead of
    /// running a burst of unpaced catch-up frames.
    /// </summary>
    public sealed class PlayerFramePacer : IPlayerFrameLimiter {
        /// <summary>
        /// Remaining time, in seconds, below which the pacer stops sleeping and spins until the deadline.
        /// </summary>
        public const double SpinThresholdSeconds = 0.002d;

        /// <summary>
        /// Clock that supplies time and the sleep/spin primitives.
        /// </summary>
        readonly IPlayerFrameClock Clock;

        /// <summary>
        /// Whether <see cref="Start"/> has set the first frame deadline.
        /// </summary>
        bool Started;

        /// <summary>
        /// Initializes a pacer for one frame loop.
        /// </summary>
        /// <param name="targetFrameRate">Positive, finite frames per second the loop is paced to.</param>
        /// <param name="clock">Clock supplying time, sleeps and spins.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame rate is not positive and finite.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the clock is null.</exception>
        public PlayerFramePacer(double targetFrameRate, IPlayerFrameClock clock) {
            if (!(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate), targetFrameRate, "The player frame pacer requires a positive, finite target frame rate.");
            }

            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            FrameSeconds = 1d / targetFrameRate;
        }

        /// <summary>
        /// Gets the duration of one frame slot in seconds.
        /// </summary>
        public double FrameSeconds { get; }

        /// <summary>
        /// Gets the clock time, in seconds, at which the current frame slot ends.
        /// </summary>
        public double NextFrameDeadlineSeconds { get; private set; }

        /// <summary>
        /// Begins pacing: the first frame slot ends one frame after the current clock time.
        /// </summary>
        public void Start() {
            NextFrameDeadlineSeconds = Clock.ElapsedSeconds + FrameSeconds;
            Started = true;
        }

        /// <summary>
        /// Waits until the current frame slot ends, then advances the deadline to the next slot. Returns immediately
        /// when the frame already overran its slot.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Start"/> was not called first.</exception>
        public void WaitForNextFrame() {
            if (!Started) {
                throw new InvalidOperationException("Start the player frame pacer before waiting for a frame.");
            }

            double remainingSeconds = NextFrameDeadlineSeconds - Clock.ElapsedSeconds;
            while (remainingSeconds > 0d) {
                int sleepMilliseconds = (int)((remainingSeconds - SpinThresholdSeconds) * 1000d);
                if (sleepMilliseconds >= 1) {
                    Clock.Sleep(sleepMilliseconds);
                } else {
                    Clock.Spin();
                }

                remainingSeconds = NextFrameDeadlineSeconds - Clock.ElapsedSeconds;
            }

            double nowSeconds = Clock.ElapsedSeconds;
            NextFrameDeadlineSeconds += FrameSeconds;
            if (NextFrameDeadlineSeconds <= nowSeconds) {
                NextFrameDeadlineSeconds = nowSeconds + FrameSeconds;
            }
        }
    }
}
