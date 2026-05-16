namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides deterministic core timing hooks for tests that need queued update and draw measurements.
    /// </summary>
    internal class TestClockDrivenCore : Core {
        /// <summary>
        /// Stores the queued measured times that should be returned to the parameterless update path.
        /// </summary>
        readonly Queue<double> MeasuredUpdateSeconds;
        /// <summary>
        /// Stores the queued draw durations that should be reported after the render manager draw call completes.
        /// </summary>
        readonly Queue<double> MeasuredDrawMilliseconds;

        /// <summary>
        /// Initializes the core with one deterministic sequence of measured update times.
        /// </summary>
        /// <param name="measuredUpdateSeconds">Queued measured update times returned by subsequent update calls.</param>
        public TestClockDrivenCore(IEnumerable<double> measuredUpdateSeconds)
            : this(new CoreInitializationOptions(), measuredUpdateSeconds) {
        }

        /// <summary>
        /// Initializes the core with one specific set of initialization options and no queued timing overrides.
        /// </summary>
        /// <param name="initializationOptions">Initialization options applied to the core under test.</param>
        public TestClockDrivenCore(CoreInitializationOptions initializationOptions)
            : this(initializationOptions, Array.Empty<double>()) {
        }

        /// <summary>
        /// Initializes the core with one specific set of initialization options and optional queued update times.
        /// </summary>
        /// <param name="initializationOptions">Initialization options applied to the core under test.</param>
        /// <param name="measuredUpdateSeconds">Queued measured update times returned by subsequent update calls.</param>
        public TestClockDrivenCore(CoreInitializationOptions initializationOptions, IEnumerable<double> measuredUpdateSeconds)
            : base(initializationOptions) {
            if (initializationOptions == null) {
                throw new ArgumentNullException(nameof(initializationOptions));
            }
            if (measuredUpdateSeconds == null) {
                throw new ArgumentNullException(nameof(measuredUpdateSeconds));
            }

            MeasuredUpdateSeconds = new Queue<double>(measuredUpdateSeconds);
            MeasuredDrawMilliseconds = new Queue<double>();
        }

        /// <summary>
        /// Queues one deterministic measured draw duration in milliseconds for subsequent draw calls.
        /// </summary>
        /// <param name="measuredDrawMilliseconds">Measured draw durations returned after each draw call.</param>
        public void QueueMeasuredDrawMilliseconds(IEnumerable<double> measuredDrawMilliseconds) {
            if (measuredDrawMilliseconds == null) {
                throw new ArgumentNullException(nameof(measuredDrawMilliseconds));
            }

            foreach (double measuredDrawMillisecondsValue in measuredDrawMilliseconds) {
                MeasuredDrawMilliseconds.Enqueue(measuredDrawMillisecondsValue);
            }
        }

        /// <summary>
        /// Returns the next queued measured time for the parameterless update path.
        /// </summary>
        /// <returns>One deterministic measured update time in seconds.</returns>
        protected override double GetCurrentMeasuredUpdateSeconds() {
            if (MeasuredUpdateSeconds.Count == 0) {
                return base.GetCurrentMeasuredUpdateSeconds();
            }

            return MeasuredUpdateSeconds.Dequeue();
        }

        /// <summary>
        /// Measures one render-manager draw and returns either a queued duration or the default stopwatch-based duration.
        /// </summary>
        /// <returns>Measured render-manager draw duration in milliseconds.</returns>
        protected override double MeasureRenderManager3DDrawMilliseconds() {
            if (MeasuredDrawMilliseconds.Count == 0) {
                return base.MeasureRenderManager3DDrawMilliseconds();
            }

            RenderManager3D.Draw();
            return MeasuredDrawMilliseconds.Dequeue();
        }
    }
}
