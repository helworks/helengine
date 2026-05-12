namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a deterministic core clock for timing tests by returning queued measured update times.
    /// </summary>
    internal class TestClockDrivenCore : Core {
        /// <summary>
        /// Stores the queued measured times that should be returned to the parameterless update path.
        /// </summary>
        readonly Queue<double> MeasuredUpdateSeconds;

        /// <summary>
        /// Initializes the core with one deterministic sequence of measured update times.
        /// </summary>
        /// <param name="measuredUpdateSeconds">Queued measured update times returned by subsequent update calls.</param>
        public TestClockDrivenCore(IEnumerable<double> measuredUpdateSeconds)
            : base(new CoreInitializationOptions()) {
            if (measuredUpdateSeconds == null) {
                throw new ArgumentNullException(nameof(measuredUpdateSeconds));
            }

            MeasuredUpdateSeconds = new Queue<double>(measuredUpdateSeconds);
        }

        /// <summary>
        /// Returns the next queued measured time for the parameterless update path.
        /// </summary>
        /// <returns>One deterministic measured update time in seconds.</returns>
        protected override double GetCurrentMeasuredUpdateSeconds() {
            if (MeasuredUpdateSeconds.Count == 0) {
                throw new InvalidOperationException("No queued measured update times remain for the timing test.");
            }

            return MeasuredUpdateSeconds.Dequeue();
        }
    }
}
