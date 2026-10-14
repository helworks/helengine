using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Minimal Play preparation scheduler for the editor-to-player argument contract test: it runs the preparation
    /// synchronously, so the launch arguments exist as soon as the test drains the editor's main-thread queue.
    /// </summary>
    public sealed class ContractPlayPreparationScheduler : IEditorPlayPreparationScheduler {
        /// <summary>
        /// Runs the preparation work immediately on the calling thread.
        /// </summary>
        /// <param name="work">Preparation work to run.</param>
        public void Start(Action work) {
            work();
        }
    }
}
