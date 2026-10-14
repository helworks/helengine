namespace helengine.editor {
    /// <summary>
    /// One stored Play setting value that the current declarations do not accept. The dialog highlights it, and Play refuses
    /// to start while any issue exists.
    /// </summary>
    public sealed class EditorPlaySettingIssue {
        /// <summary>
        /// Initializes one issue.
        /// </summary>
        /// <param name="scope">Section the value belongs to.</param>
        /// <param name="settingId">Setting id.</param>
        /// <param name="storedValue">Stored value that was rejected.</param>
        /// <param name="reason">Why the value was rejected.</param>
        public EditorPlaySettingIssue(EditorPlaySettingScope scope, string settingId, string storedValue, string reason) {
            Scope = scope;
            SettingId = settingId ?? throw new ArgumentNullException(nameof(settingId));
            StoredValue = storedValue ?? throw new ArgumentNullException(nameof(storedValue));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        /// <summary>Gets the section the value belongs to.</summary>
        public EditorPlaySettingScope Scope { get; }

        /// <summary>Gets the setting id.</summary>
        public string SettingId { get; }

        /// <summary>Gets the stored value that was rejected.</summary>
        public string StoredValue { get; }

        /// <summary>Gets why the value was rejected.</summary>
        public string Reason { get; }

        /// <summary>
        /// Describes the issue in one clause, for example <c>'window-scale' = '5x' (not one of 1x, 2x, 3x, 4x, fit)</c>.
        /// </summary>
        /// <returns>Readable description.</returns>
        public string Describe() {
            return $"'{SettingId}' = '{StoredValue}' ({Reason})";
        }
    }
}
