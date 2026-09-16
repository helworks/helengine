using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Records how long each editor boot phase takes and writes it to the session log, so slow
    /// startups can be attributed to a phase without attaching a profiler.
    /// </summary>
    public static class EditorBootTimeline {
        /// <summary>
        /// Stopwatch started when the host begins initializing the editor.
        /// </summary>
        static readonly Stopwatch Clock = new Stopwatch();

        /// <summary>
        /// Elapsed milliseconds at the previous mark.
        /// </summary>
        static long lastMarkMilliseconds;

        /// <summary>
        /// Starts or restarts the boot clock.
        /// </summary>
        public static void Start() {
            Clock.Restart();
            lastMarkMilliseconds = 0;
        }

        /// <summary>
        /// Logs the completion of one boot phase with its own duration and the total elapsed boot time.
        /// </summary>
        /// <param name="phase">Short phase name, such as "asset identity index".</param>
        public static void Mark(string phase) {
            if (!Clock.IsRunning) {
                Start();
            }

            long now = Clock.ElapsedMilliseconds;
            long delta = now - lastMarkMilliseconds;
            lastMarkMilliseconds = now;
            Logger.WriteLine($"Boot: {phase} took {delta} ms (total {now} ms).");
        }
    }
}
