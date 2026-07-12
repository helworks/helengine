using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records fixed-step simulation calls issued by the core physics host during tests.
    /// </summary>
    internal sealed class TestPhysicsRuntime : ISceneBindablePhysicsRuntime {
        /// <summary>
        /// Gets the number of fixed-step calls received by this runtime.
        /// </summary>
        public int StepCount { get; private set; }

        /// <summary>
        /// Gets the elapsed step length recorded during the most recent fixed-step call.
        /// </summary>
        public double LastStepSeconds { get; private set; }
        /// <summary>
        /// Gets the most recent root entities bound to this runtime.
        /// </summary>
        public IReadOnlyList<Entity> LastBoundRootEntities { get; private set; } = Array.Empty<Entity>();

        /// <summary>
        /// Gets the number of bodies currently registered in the bound test scene.
        /// </summary>
        public int RegisteredBodyCount => 0;

        /// <summary>
        /// Records one fixed-step simulation call from the core host.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
            StepCount++;
            LastStepSeconds = stepSeconds;
        }

        /// <summary>
        /// Records the scene roots most recently bound to the runtime.
        /// </summary>
        /// <param name="rootEntities">Scene roots bound by production code.</param>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            LastBoundRootEntities = rootEntities;
        }
    }
}
