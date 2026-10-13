using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Minimal process launcher for the editor-to-player argument contract test: records the launch request instead of
    /// starting a process, and hands back a process that stays running.
    /// </summary>
    public sealed class ContractPlayProcessLauncher : IEditorPlayProcessLauncher {
        /// <summary>
        /// Gets the request of the most recent launch, or null before any launch.
        /// </summary>
        public EditorPlayLaunchRequest LastRequest { get; private set; }

        /// <summary>
        /// Records the launch request and returns a running stand-in process.
        /// </summary>
        /// <param name="request">Launch request built by the session service.</param>
        /// <returns>A stand-in process that never exits on its own.</returns>
        public IEditorPlayProcess Start(EditorPlayLaunchRequest request) {
            LastRequest = request;
            return new ContractPlayProcess();
        }
    }
}
