using System.Diagnostics;

namespace helengine.player {
    /// <summary>
    /// Process entry point for the desktop Play player launched by the editor.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Parses the editor-supplied command line and runs the player until its window closes.
        /// Any unhandled failure is reported on stderr and turned into a non-zero exit code, because
        /// this is the process boundary the editor observes. A monotonic stopwatch starts on entry and times the player's
        /// first frame for the editor's Play startup summary; it excludes the .NET runtime startup before Main.
        /// </summary>
        /// <param name="args">Command-line arguments in the format documented by <see cref="PlayerLaunchOptions"/>.</param>
        /// <returns>Zero when the player exits normally; one when startup or the frame loop failed.</returns>
        [STAThread]
        public static int Main(string[] args) {
            Stopwatch startupStopwatch = Stopwatch.StartNew();
            try {
                PlayerLaunchOptions options = PlayerLaunchOptions.Parse(args);
                PlayerHost.Run(options, startupStopwatch);
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }
        }
    }
}
