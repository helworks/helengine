using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// The Play settings captured on the editor frame thread when Play was pressed: private copies of the effective common
    /// and platform values, the Shift+click debugger request, the platform's Play capability and video modes, and the editor
    /// process id.
    /// Editing Play settings while this Play prepares changes only the next Play.
    /// </summary>
    public sealed class EditorPlaySettingsSnapshot {
        /// <summary>
        /// Captures one snapshot.
        /// </summary>
        /// <param name="evaluation">Valid evaluation of the stored settings for the platform.</param>
        /// <param name="waitForDebuggerRequested">Whether Shift was held on the Play button.</param>
        /// <param name="capability">Play capability of the platform (read-only after the catalog loads it).</param>
        /// <param name="videoModeCapability">Video modes of the platform, or null when it declares none.</param>
        /// <param name="editorProcessId">Process id of this editor, used by the player to find the editor's monitor.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the evaluation has issues, the process id is not positive, or the capability takes its frame rate from
        /// the video mode but no video modes are given.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when the evaluation or capability is null.</exception>
        public EditorPlaySettingsSnapshot(EditorPlaySettingsEvaluation evaluation, bool waitForDebuggerRequested, PlatformPlayerCapability capability, PlatformVideoModeCapability videoModeCapability, int editorProcessId) {
            if (evaluation == null) {
                throw new ArgumentNullException(nameof(evaluation));
            } else if (!evaluation.IsValid) {
                throw new ArgumentException(evaluation.FormatFailure(), nameof(evaluation));
            } else if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            } else if (capability.FrameRateFromVideoMode && videoModeCapability == null) {
                throw new ArgumentException($"Platform '{evaluation.PlatformId}' takes its Play frame rate from the video mode, but declares no video modes.", nameof(videoModeCapability));
            } else if (editorProcessId <= 0) {
                throw new ArgumentException("The editor process id must be positive.", nameof(editorProcessId));
            }

            PlatformId = evaluation.PlatformId;
            CommonValues = new Dictionary<string, string>(evaluation.CommonValues, StringComparer.Ordinal);
            PlatformValues = new Dictionary<string, string>(evaluation.PlatformValues, StringComparer.Ordinal);
            WaitForDebuggerRequested = waitForDebuggerRequested;
            Capability = capability;
            VideoModeCapability = videoModeCapability;
            EditorProcessId = editorProcessId;
        }

        /// <summary>Gets the platform being played.</summary>
        public string PlatformId { get; }

        /// <summary>Gets the effective common values captured at press time.</summary>
        public IReadOnlyDictionary<string, string> CommonValues { get; }

        /// <summary>Gets the effective platform values captured at press time; <c>project</c> is still unresolved.</summary>
        public IReadOnlyDictionary<string, string> PlatformValues { get; }

        /// <summary>Gets whether Shift was held on the Play button.</summary>
        public bool WaitForDebuggerRequested { get; }

        /// <summary>Gets the Play capability of the platform.</summary>
        public PlatformPlayerCapability Capability { get; }

        /// <summary>Gets the video modes of the platform, or null when it declares none.</summary>
        public PlatformVideoModeCapability VideoModeCapability { get; }

        /// <summary>Gets whether the platform declares video modes, so Play resolves one and paces at its frame rate.</summary>
        public bool HasVideoModes => VideoModeCapability != null;

        /// <summary>Gets the editor process id.</summary>
        public int EditorProcessId { get; }

        /// <summary>Gets whether the player waits for a debugger: the stored setting or the Shift+click request.</summary>
        public bool EffectiveWaitForDebugger =>
            WaitForDebuggerRequested || string.Equals(CommonValues[PlayerCommonPlaySettings.WaitForDebuggerId], "true", StringComparison.Ordinal);
    }
}
