using helengine.baseplatform.Builders;
using helengine.baseplatform.Reporting;

namespace helengine.editor {
    /// <summary>
    /// Formats and forwards streamed progress updates emitted by a platform builder.
    /// </summary>
    public sealed class EditorPlatformBuildProgressReporter : IPlatformBuildProgressReporter {
        /// <summary>
        /// Receives formatted build progress lines.
        /// </summary>
        readonly Action<string> MessageWriter;

        /// <summary>
        /// Initializes a reporter that streams platform-builder progress to the active console output.
        /// </summary>
        public EditorPlatformBuildProgressReporter()
            : this(Console.WriteLine) {
        }

        /// <summary>
        /// Initializes a reporter that forwards formatted platform-builder progress to the supplied output sink.
        /// </summary>
        /// <param name="messageWriter">Output sink that receives one formatted progress line per builder update.</param>
        public EditorPlatformBuildProgressReporter(Action<string> messageWriter) {
            MessageWriter = messageWriter ?? throw new ArgumentNullException(nameof(messageWriter));
        }

        /// <summary>
        /// Formats and forwards one progress update emitted by a platform builder.
        /// </summary>
        /// <param name="update">Progress update emitted by the platform builder.</param>
        public void Report(PlatformBuildProgressUpdate update) {
            if (update == null) {
                throw new ArgumentNullException(nameof(update));
            }

            MessageWriter($"[build] {update.StageName} {update.CompletedCount}/{update.TotalCount} {update.CurrentItemIdentity}: {update.Message}");
        }
    }
}
