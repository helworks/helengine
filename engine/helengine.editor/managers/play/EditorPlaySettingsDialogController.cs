using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Host-testable logic behind the Play Settings dialog. It owns the values the model validates and saves, kept apart
    /// from the dictionaries the shared setting rows bind to: those rows replace an unrecognised choice with the first
    /// allowed value and write checkbox states as <c>True</c>/<c>False</c>, so letting them feed the model would silently
    /// coerce a stored value. Here a value only changes through an explicit user edit or Reset Invalid, booleans are
    /// stored as <c>true</c>/<c>false</c>, and every edit or reset re-checks the values so flags and the status line follow
    /// the latest validation instead of the open-time stored issues.
    /// </summary>
    public sealed class EditorPlaySettingsDialogController {
        /// <summary>Common values the model validates and saves; changed only by user edits and resets.</summary>
        readonly Dictionary<string, string> CommonValues;

        /// <summary>Platform values the model validates and saves; changed only by user edits and resets.</summary>
        readonly Dictionary<string, string> PlatformValues;

        /// <summary>
        /// Stored ids the declarations no longer know. They have no row; the status line lists them and Save removes them.
        /// </summary>
        readonly List<EditorPlaySettingIssue> UndeclaredIssues;

        /// <summary>
        /// Rejected values of declared settings: the stored ones until the first edit or reset, then those of the latest
        /// validation.
        /// </summary>
        IReadOnlyList<EditorPlaySettingIssue> ValueIssues;

        /// <summary>Whether the user edited or reset any value since the dialog opened.</summary>
        bool HasEdits;

        /// <summary>Message of the latest failed save; empty when the latest save did not fail on the file.</summary>
        string SaveFailure;

        /// <summary>
        /// Opens the controller over one dialog model.
        /// </summary>
        /// <param name="model">Model of the platform's Play settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public EditorPlaySettingsDialogController(EditorPlaySettingsDialogModel model) {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            CommonValues = model.CreateCommonEditValues();
            PlatformValues = model.CreatePlatformEditValues();
            UndeclaredIssues = new List<EditorPlaySettingIssue>();
            List<EditorPlaySettingIssue> storedValueIssues = new List<EditorPlaySettingIssue>();
            for (int index = 0; index < model.StoredIssues.Count; index++) {
                EditorPlaySettingIssue issue = model.StoredIssues[index];
                if (FindDefinition(issue.Scope, issue.SettingId) == null) {
                    UndeclaredIssues.Add(issue);
                } else {
                    storedValueIssues.Add(issue);
                }
            }

            ValueIssues = storedValueIssues.AsReadOnly();
            SaveFailure = string.Empty;
        }

        /// <summary>Gets the model whose values this controller edits.</summary>
        public EditorPlaySettingsDialogModel Model { get; }

        /// <summary>
        /// Gets the status line: a failed save naming play.json, the rejected values (stored ones until the first edit or
        /// reset, then the latest validation) and the stored ids Save removes. Empty when there is nothing to report.
        /// </summary>
        public string StatusText {
            get {
                List<string> parts = new List<string>();
                if (SaveFailure.Length > 0) {
                    parts.Add(SaveFailure);
                }
                if (ValueIssues.Count > 0) {
                    string prefix = HasEdits ? "Invalid values" : "Stored values are no longer allowed";
                    parts.Add(prefix + ": " + DescribeIssues(ValueIssues) + ". Pick a value or Reset Invalid, then Save.");
                }
                if (UndeclaredIssues.Count > 0) {
                    parts.Add("Stored settings that are no longer declared: " + DescribeIssues(UndeclaredIssues) + ".");
                }

                return string.Join(" ", parts);
            }
        }

        /// <summary>
        /// Gets the value the model sees for one declared setting (a rejected stored value until the user changes it).
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>Current value.</returns>
        /// <exception cref="ArgumentException">Thrown when the section does not declare the id.</exception>
        public string GetValue(EditorPlaySettingScope scope, string settingId) {
            return GetDeclaredValues(scope, settingId)[settingId];
        }

        /// <summary>
        /// Creates a private copy of one section's values for the shared setting rows to bind to. The rows may rewrite
        /// that copy; it never reaches the model.
        /// </summary>
        /// <param name="scope">Section to copy.</param>
        /// <returns>Copy for the rows.</returns>
        public Dictionary<string, string> CreateBindingValues(EditorPlaySettingScope scope) {
            return new Dictionary<string, string>(scope == EditorPlaySettingScope.Common ? CommonValues : PlatformValues, StringComparer.Ordinal);
        }

        /// <summary>
        /// Reports whether a row's current value is rejected, so its label is highlighted.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>True when the row's value is rejected.</returns>
        public bool IsRowFlagged(EditorPlaySettingScope scope, string settingId) {
            for (int index = 0; index < ValueIssues.Count; index++) {
                if (ValueIssues[index].Scope == scope && string.Equals(ValueIssues[index].SettingId, settingId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies a value the user picked or typed for one declared setting, then re-checks every value.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <param name="value">New value.</param>
        /// <exception cref="ArgumentException">Thrown when the section does not declare the id.</exception>
        public void SetValue(EditorPlaySettingScope scope, string settingId, string value) {
            GetDeclaredValues(scope, settingId)[settingId] = value ?? string.Empty;
            HasEdits = true;
            Revalidate();
        }

        /// <summary>
        /// Applies a checkbox state in the store's boolean format (<c>true</c>/<c>false</c>), then re-checks every value.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <param name="isChecked">New checkbox state.</param>
        /// <exception cref="ArgumentException">Thrown when the section does not declare the id.</exception>
        public void SetBooleanValue(EditorPlaySettingScope scope, string settingId, bool isChecked) {
            SetValue(scope, settingId, isChecked ? "true" : "false");
        }

        /// <summary>
        /// Puts every row whose value is rejected back to its declared default, then re-checks every value. Does nothing
        /// when no row is rejected.
        /// </summary>
        public void ResetInvalidToDefaults() {
            if (ValueIssues.Count == 0) {
                return;
            }

            for (int index = 0; index < ValueIssues.Count; index++) {
                EditorPlaySettingIssue issue = ValueIssues[index];
                GetDeclaredValues(issue.Scope, issue.SettingId)[issue.SettingId] = Model.GetDefaultValue(issue.Scope, issue.SettingId);
            }

            HasEdits = true;
            Revalidate();
        }

        /// <summary>
        /// Validates the current values and, when every value is accepted, saves them through the model, which removes
        /// stored ids the declarations no longer know. A rejected value or a file that cannot be written leaves the file
        /// untouched and is described in <see cref="StatusText"/>; the latter names play.json.
        /// </summary>
        /// <param name="store">Store of the file the model was opened on.</param>
        /// <returns>True when the settings were saved.</returns>
        /// <exception cref="ArgumentException">Thrown when the store writes a different file than the model was opened on.</exception>
        public bool TrySave(EditorPlaySettingsStore store) {
            HasEdits = true;
            Revalidate();
            if (ValueIssues.Count > 0) {
                return false;
            }

            try {
                Model.Save(store);
            } catch (InvalidOperationException exception) {
                SaveFailure = exception.Message;
                return false;
            }

            UndeclaredIssues.Clear();
            return true;
        }

        /// <summary>
        /// Re-checks every current value through the model and forgets an earlier save failure.
        /// </summary>
        void Revalidate() {
            SaveFailure = string.Empty;
            ValueIssues = Model.ApplyEdits(CommonValues, PlatformValues);
        }

        /// <summary>
        /// Gets the value section that declares one setting.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>The section's values.</returns>
        /// <exception cref="ArgumentException">Thrown when the section does not declare the id.</exception>
        Dictionary<string, string> GetDeclaredValues(EditorPlaySettingScope scope, string settingId) {
            if (FindDefinition(scope, settingId) == null) {
                throw new ArgumentException($"Play setting '{settingId}' is not declared in the {scope} section.", nameof(settingId));
            }

            return scope == EditorPlaySettingScope.Common ? CommonValues : PlatformValues;
        }

        /// <summary>
        /// Finds the declaration of one setting in its section.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>The declaration, or null when the section does not declare the id.</returns>
        PlatformSettingDefinition FindDefinition(EditorPlaySettingScope scope, string settingId) {
            PlatformSettingDefinition[] definitions = scope == EditorPlaySettingScope.Common ? Model.CommonDefinitions : Model.PlatformDefinitions;
            for (int index = 0; index < definitions.Length; index++) {
                if (string.Equals(definitions[index].SettingId, settingId, StringComparison.Ordinal)) {
                    return definitions[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Joins issue descriptions for the status line.
        /// </summary>
        /// <param name="issues">Issues to describe.</param>
        /// <returns>Descriptions separated by semicolons.</returns>
        static string DescribeIssues(IReadOnlyList<EditorPlaySettingIssue> issues) {
            List<string> descriptions = new List<string>();
            for (int index = 0; index < issues.Count; index++) {
                descriptions.Add(issues[index].Describe());
            }

            return string.Join("; ", descriptions);
        }
    }
}
