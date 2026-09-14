using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scene-bindable physics runtime stub that records the scene roots handed to it so tests can
    /// assert that editor scene loads reach the runtime with the exact hierarchy they published.
    /// </summary>
    internal sealed class RecordingSceneBindablePhysicsRuntime : ISceneBindablePhysicsRuntime {
        /// <summary>
        /// Gets the scene roots recorded by the most recent bind call.
        /// </summary>
        public IReadOnlyList<Entity> LastBoundRootEntities { get; private set; }

        /// <summary>
        /// Gets the number of bodies registered in the stub scene. The stub never registers bodies.
        /// </summary>
        public int RegisteredBodyCount => 0;

        /// <summary>
        /// Ignores fixed-step simulation calls; this stub only observes scene binding.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
        }

        /// <summary>
        /// Records the scene roots bound by production code.
        /// </summary>
        /// <param name="rootEntities">Scene roots handed to the runtime.</param>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            LastBoundRootEntities = rootEntities;
        }
    }
}
