using helengine.baseplatform.Builders;
using helengine.baseplatform.Reporting;

namespace helengine.editor {
    /// <summary>
    /// No-op progress reporter used when the editor does not need streamed platform-build feedback.
    /// </summary>
    public sealed class EditorPlatformBuildProgressReporter : IPlatformBuildProgressReporter {
        /// <summary>
        /// Ignores one progress update.
        /// </summary>
        /// <param name="update">Progress update emitted by the platform builder.</param>
        public void Report(PlatformBuildProgressUpdate update) {
        }
    }
}
