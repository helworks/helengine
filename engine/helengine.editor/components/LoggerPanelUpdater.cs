namespace helengine.editor {
    /// <summary>
    /// Update component that flushes pending logger entries into the UI panel.
    /// </summary>
    public class LoggerPanelUpdater : UpdateComponent {
        /// <summary>
        /// Logger panel that receives updates.
        /// </summary>
        readonly LoggerPanel panel;

        /// <summary>
        /// Initializes a new updater for the specified panel.
        /// </summary>
        /// <param name="panel">Logger panel to update.</param>
        public LoggerPanelUpdater(LoggerPanel panel) {
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel));
        }

        /// <summary>
        /// Flushes pending log entries into the panel each frame.
        /// </summary>
        public override void Update() {
            panel.FlushPendingEntries();
        }
    }
}
