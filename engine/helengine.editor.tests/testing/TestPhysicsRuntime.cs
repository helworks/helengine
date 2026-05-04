using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records fixed-step simulation calls issued by the core physics host during tests.
    /// </summary>
    internal sealed class TestPhysicsRuntime : IPhysicsRuntime {
        /// <summary>
        /// Gets the number of fixed-step calls received by this runtime.
        /// </summary>
        public int StepCount { get; private set; }

        /// <summary>
        /// Gets the elapsed step length recorded during the most recent fixed-step call.
        /// </summary>
        public double LastStepSeconds { get; private set; }

        /// <summary>
        /// Records one fixed-step simulation call from the core host.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
            StepCount++;
            LastStepSeconds = stepSeconds;
        }
    }
}
