namespace helengine {
    /// <summary>
    /// Accumulates host frame time and emits fixed simulation steps for pluggable physics runtimes.
    /// </summary>
    public sealed class PhysicsFixedStepScheduler {
        /// <summary>
        /// Backing accumulator containing unconsumed frame time in seconds.
        /// </summary>
        double AccumulatedSecondsValue;

        /// <summary>
        /// Initializes a new fixed-step scheduler.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
        public PhysicsFixedStepScheduler(double stepSeconds) {
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Step size must be a finite value greater than zero.");
            }

            StepSeconds = stepSeconds;
        }

        /// <summary>
        /// Gets the configured fixed simulation step length in seconds.
        /// </summary>
        public double StepSeconds { get; }

        /// <summary>
        /// Gets the currently accumulated unconsumed frame time in seconds.
        /// </summary>
        public double AccumulatedSeconds => AccumulatedSecondsValue;

        /// <summary>
        /// Adds one host frame delta to the accumulator.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed host frame time in seconds.</param>
        public void AddElapsedSeconds(double elapsedSeconds) {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d) {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time must be finite and non-negative.");
            }

            AccumulatedSecondsValue += elapsedSeconds;
        }

        /// <summary>
        /// Resets the accumulator so the next simulation starts from a clean timing state.
        /// </summary>
        public void Reset() {
            AccumulatedSecondsValue = 0d;
        }

        /// <summary>
        /// Consumes one fixed simulation step when enough accumulated time is available.
        /// </summary>
        /// <returns>True when one simulation step should run; otherwise false.</returns>
        public bool TryConsumeStep() {
            if (AccumulatedSecondsValue < StepSeconds) {
                return false;
            }

            AccumulatedSecondsValue -= StepSeconds;
            return true;
        }
    }
}
