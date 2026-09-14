using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Physics runtime stub that deliberately implements only the fixed-step contract, so tests can
    /// prove the editor rejects runtimes that cannot own an editor scene instead of skipping them.
    /// </summary>
    internal sealed class NonBindablePhysicsRuntime : IPhysicsRuntime {
        /// <summary>
        /// Ignores fixed-step simulation calls; this stub exists only to fail scene binding.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
        }
    }
}
