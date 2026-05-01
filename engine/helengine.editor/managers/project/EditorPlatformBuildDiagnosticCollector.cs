using helengine.baseplatform.Builders;
using helengine.baseplatform.Reporting;

namespace helengine.editor {
    /// <summary>
    /// Collects platform-build diagnostics so the editor executor can summarize the final result.
    /// </summary>
    public sealed class EditorPlatformBuildDiagnosticCollector : IPlatformBuildDiagnosticReporter {
        /// <summary>
        /// Gets the diagnostics reported by the active platform builder.
        /// </summary>
        public List<PlatformBuildDiagnostic> Diagnostics { get; } = [];

        /// <summary>
        /// Records one platform-build diagnostic.
        /// </summary>
        /// <param name="diagnostic">Diagnostic emitted by the active platform builder.</param>
        public void Report(PlatformBuildDiagnostic diagnostic) {
            if (diagnostic == null) {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            Diagnostics.Add(diagnostic);
        }
    }
}
