using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.Player {
    /// <summary>
    /// The Play settings of one Play session, fully resolved by the editor (no <c>project</c> placeholders remain) and
    /// handed to the player through the <c>--play-settings</c> file.
    /// </summary>
    public sealed class PlayerPlaySettings {
        /// <summary>
        /// Initializes resolved Play settings.
        /// </summary>
        /// <param name="windowScale">Window scale relative to the platform render size.</param>
        /// <param name="windowMonitor">Monitor the window opens on.</param>
        /// <param name="editorProcessId">Process id of the editor, used to find the monitor its main window is on.</param>
        /// <param name="waitForDebugger">Whether the player waits for a debugger before starting the scene.</param>
        /// <param name="shadowsEnabled">Whether authored shadows render.</param>
        /// <param name="frameRateUnlimited">Whether the frame loop runs unpaced and without vsync.</param>
        /// <param name="targetFrameRate">Frames per second the loop is paced to; 0 exactly when unlimited.</param>
        /// <param name="platformValues">Resolved platform Play setting values keyed by setting id.</param>
        /// <param name="videoMode">Resolved console video mode, or null when the platform declares no video modes.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive process id or an inconsistent frame rate.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the platform values are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a platform value or key is blank or null, or when a paced frame rate is not the video mode's player
        /// frame rate.
        /// </exception>
        public PlayerPlaySettings(
            PlayerWindowScale windowScale,
            PlayerWindowMonitor windowMonitor,
            int editorProcessId,
            bool waitForDebugger,
            bool shadowsEnabled,
            bool frameRateUnlimited,
            double targetFrameRate,
            IReadOnlyDictionary<string, string> platformValues,
            PlatformVideoMode videoMode) {
            if (editorProcessId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(editorProcessId), editorProcessId, "The editor process id must be positive.");
            } else if (frameRateUnlimited && targetFrameRate != 0d) {
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate), targetFrameRate, "An unlimited frame rate carries no target frame rate.");
            } else if (!frameRateUnlimited && !(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate), targetFrameRate, "A paced frame rate needs a positive, finite target frame rate.");
            } else if (platformValues == null) {
                throw new ArgumentNullException(nameof(platformValues));
            } else if (videoMode != null && !frameRateUnlimited && targetFrameRate != videoMode.PlayerFrameRate) {
                throw new ArgumentException(
                    $"A paced Play with video mode '{videoMode.Id}' must pace at the mode's player frame rate {videoMode.PlayerFrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}, but the target frame rate is {targetFrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
                    nameof(targetFrameRate));
            }

            Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in platformValues) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    throw new ArgumentException("Platform Play values need non-blank ids and non-null values.", nameof(platformValues));
                }
                copy.Add(entry.Key, entry.Value);
            }

            WindowScale = windowScale;
            WindowMonitor = windowMonitor;
            EditorProcessId = editorProcessId;
            WaitForDebugger = waitForDebugger;
            ShadowsEnabled = shadowsEnabled;
            FrameRateUnlimited = frameRateUnlimited;
            TargetFrameRate = targetFrameRate;
            PlatformValues = copy;
            VideoMode = videoMode;
        }

        /// <summary>Gets the window scale relative to the platform render size.</summary>
        public PlayerWindowScale WindowScale { get; }

        /// <summary>Gets the monitor the window opens on.</summary>
        public PlayerWindowMonitor WindowMonitor { get; }

        /// <summary>Gets the editor process id used to find the editor's monitor.</summary>
        public int EditorProcessId { get; }

        /// <summary>Gets whether the player waits for a debugger before starting the scene.</summary>
        public bool WaitForDebugger { get; }

        /// <summary>Gets whether authored shadows render in this Play session.</summary>
        public bool ShadowsEnabled { get; }

        /// <summary>Gets whether the frame loop runs unpaced and without vsync.</summary>
        public bool FrameRateUnlimited { get; }

        /// <summary>Gets the paced frames per second; 0 when <see cref="FrameRateUnlimited"/>.</summary>
        public double TargetFrameRate { get; }

        /// <summary>Gets the resolved platform Play setting values keyed by setting id.</summary>
        public IReadOnlyDictionary<string, string> PlatformValues { get; }

        /// <summary>
        /// Gets the console video mode this Play reproduces (render size, frame rate and display aspect), or null when the
        /// platform declares no video modes.
        /// </summary>
        public PlatformVideoMode VideoMode { get; }

        /// <summary>Gets whether this Play reproduces a console video mode.</summary>
        public bool HasVideoMode => VideoMode != null;
    }
}
