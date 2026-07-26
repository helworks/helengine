using helengine.baseplatform.Reporting;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies platform-builder progress updates are formatted and forwarded to the active build output sink.
    /// </summary>
    public sealed class EditorPlatformBuildProgressReporterTests {
        /// <summary>
        /// Ensures a platform progress update is written with its stage, completed item count, identity, and message.
        /// </summary>
        [Fact]
        public void Report_WhenBuilderEmitsProgress_ForwardsFormattedUpdateToWriter() {
            List<string> messages = new List<string>();
            EditorPlatformBuildProgressReporter reporter = new EditorPlatformBuildProgressReporter(messages.Add);

            reporter.Report(new PlatformBuildProgressUpdate(
                "Package",
                "cube_test",
                2,
                5,
                "Writing native package."));

            Assert.Equal("[build] Package 2/5 cube_test: Writing native package.", Assert.Single(messages));
        }
    }
}
