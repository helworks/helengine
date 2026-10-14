namespace helengine.editor {
    /// <summary>
    /// The raw contents of <c>user_settings/play.json</c>: stored common Play setting values and one section of stored values
    /// per platform id. Values are text (<c>true</c>/<c>false</c> for booleans). Keys absent here fall back to the declared
    /// defaults; validation against declarations happens in <see cref="EditorPlaySettingsEvaluator"/>.
    /// </summary>
    public sealed class EditorPlaySettingsDocument {
        /// <summary>
        /// Initializes an empty document (no stored values).
        /// </summary>
        public EditorPlaySettingsDocument() {
            CommonValues = new Dictionary<string, string>(StringComparer.Ordinal);
            PlatformValues = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        }

        /// <summary>Gets the stored common Play setting values keyed by setting id.</summary>
        public Dictionary<string, string> CommonValues { get; }

        /// <summary>Gets the stored platform Play setting values, keyed by platform id and then by setting id.</summary>
        public Dictionary<string, Dictionary<string, string>> PlatformValues { get; }

        /// <summary>
        /// Creates a deep copy, so a dialog can edit values without changing the loaded document.
        /// </summary>
        /// <returns>Independent copy of this document.</returns>
        public EditorPlaySettingsDocument Clone() {
            EditorPlaySettingsDocument copy = new EditorPlaySettingsDocument();
            foreach (KeyValuePair<string, string> entry in CommonValues) {
                copy.CommonValues.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<string, Dictionary<string, string>> section in PlatformValues) {
                copy.PlatformValues.Add(section.Key, new Dictionary<string, string>(section.Value, StringComparer.Ordinal));
            }

            return copy;
        }
    }
}
