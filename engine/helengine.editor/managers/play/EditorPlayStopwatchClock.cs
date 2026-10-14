using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Production Play clock backed by the high-resolution <see cref="Stopwatch"/> timestamp, which never jumps when the
    /// wall clock is adjusted.
    /// </summary>
    public sealed class EditorPlayStopwatchClock : IEditorPlayClock {
        /// <summary>
        /// Gets the current high-resolution timestamp converted to a time span.
        /// </summary>
        public TimeSpan Now {
            get {
                return TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
            }
        }
    }
}
