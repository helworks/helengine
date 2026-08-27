namespace helengine.editor {
    /// <summary>
    /// Collects automatic asset repair diagnostics for one authoring session.
    /// </summary>
    public sealed class EditorAssetRepairReport {
        /// <summary>
        /// Creates an empty report for a new authoring session.
        /// </summary>
        public EditorAssetRepairReport() {
        }

        /// <summary>
        /// Returns the current human-readable summary.
        /// </summary>
        /// <returns>An empty summary until a repair is recorded by the repair service.</returns>
        public string CreateSummary() {
            return string.Empty;
        }
    }
}
