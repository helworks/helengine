using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// View-model of the Play Settings dialog for one platform. It holds the declarations, the effective values, the stored
    /// values the declarations reject (so the dialog highlights them), validates edits and produces the document to save:
    /// the common section plus this platform's section, with every other platform's section untouched.
    /// A stored id the declarations no longer know (which makes Play fail) does not stop the dialog from opening: it is
    /// reported as a stored issue and the saved document leaves it out, so the dialog is always a way to repair the file.
    /// Stored values are never coerced: a rejected value stays in the edit values until the user picks another one.
    /// </summary>
    public sealed class EditorPlaySettingsDialogModel {
        /// <summary>Stored document the dialog was opened on; never modified.</summary>
        readonly EditorPlaySettingsDocument Document;

        /// <summary>Current common values (stored, default, or applied edits).</summary>
        Dictionary<string, string> CommonValues;

        /// <summary>Current platform values (stored, default, or applied edits).</summary>
        Dictionary<string, string> PlatformValues;

        /// <summary>
        /// Issues blocking a save: the stored issues until edits are applied, then the issues of the latest applied edits.
        /// </summary>
        IReadOnlyList<EditorPlaySettingIssue> EditIssues;

        /// <summary>
        /// Opens the model for one platform.
        /// </summary>
        /// <param name="platformId">Platform whose Play settings are edited.</param>
        /// <param name="platformDisplayName">Platform name shown as the platform section title.</param>
        /// <param name="capability">Play capability of the platform.</param>
        /// <param name="document">Stored Play settings.</param>
        /// <param name="filePath">File the document came from.</param>
        /// <exception cref="ArgumentException">Thrown when the platform id, display name or file path is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the capability or document is null.</exception>
        public EditorPlaySettingsDialogModel(string platformId, string platformDisplayName, PlatformPlayerCapability capability, EditorPlaySettingsDocument document, string filePath) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("The platform id is required.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(platformDisplayName)) {
                throw new ArgumentException("The platform display name is required.", nameof(platformDisplayName));
            } else if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            } else if (document == null) {
                throw new ArgumentNullException(nameof(document));
            } else if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("The Play settings file path is required.", nameof(filePath));
            }

            List<EditorPlaySettingIssue> storedIssues = new List<EditorPlaySettingIssue>();
            EditorPlaySettingsDocument declaredDocument = document.Clone();
            RemoveUndeclaredCommonSettings(declaredDocument, storedIssues);
            if (declaredDocument.PlatformValues.TryGetValue(platformId, out Dictionary<string, string> storedPlatformValues)) {
                RemoveUndeclaredPlatformSettings(storedPlatformValues, platformId, capability, storedIssues);
            }

            EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(declaredDocument, filePath, platformId, capability);
            storedIssues.AddRange(evaluation.Issues);
            Document = document;
            PlatformId = platformId;
            PlatformDisplayName = platformDisplayName;
            FilePath = filePath;
            CommonDefinitions = PlayerCommonPlaySettings.CreateDefinitions();
            PlatformDefinitions = capability.PlaySettings;
            CommonValues = new Dictionary<string, string>(evaluation.CommonValues, StringComparer.Ordinal);
            PlatformValues = new Dictionary<string, string>(evaluation.PlatformValues, StringComparer.Ordinal);
            StoredIssues = storedIssues;
            EditIssues = storedIssues;
        }

        /// <summary>Gets the platform whose Play settings are edited.</summary>
        public string PlatformId { get; }

        /// <summary>Gets the platform name shown as the platform section title.</summary>
        public string PlatformDisplayName { get; }

        /// <summary>Gets the Play settings file the document came from and is saved to.</summary>
        public string FilePath { get; }

        /// <summary>Gets the common Play setting declarations.</summary>
        public PlatformSettingDefinition[] CommonDefinitions { get; }

        /// <summary>Gets the platform's Play setting declarations (may be empty).</summary>
        public PlatformSettingDefinition[] PlatformDefinitions { get; }

        /// <summary>
        /// Gets the stored entries rejected when the dialog opened: ids the declarations no longer know (the saved document
        /// leaves them out) and values the declarations reject (kept in the edit values until the user picks another).
        /// </summary>
        public IReadOnlyList<EditorPlaySettingIssue> StoredIssues { get; }

        /// <summary>
        /// Reports whether a stored entry was rejected when the dialog opened, so its row is highlighted.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>True when the stored entry was rejected.</returns>
        public bool IsStoredValueInvalid(EditorPlaySettingScope scope, string settingId) {
            for (int index = 0; index < StoredIssues.Count; index++) {
                if (StoredIssues[index].Scope == scope && string.Equals(StoredIssues[index].SettingId, settingId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the declared default of one setting, so the dialog can reset a rejected stored value to it.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>Declared default value.</returns>
        /// <exception cref="ArgumentException">Thrown when the section does not declare the id.</exception>
        public string GetDefaultValue(EditorPlaySettingScope scope, string settingId) {
            PlatformSettingDefinition[] definitions = scope == EditorPlaySettingScope.Common ? CommonDefinitions : PlatformDefinitions;
            for (int index = 0; index < definitions.Length; index++) {
                if (string.Equals(definitions[index].SettingId, settingId, StringComparison.Ordinal)) {
                    return definitions[index].DefaultValue;
                }
            }

            throw new ArgumentException($"Play setting '{settingId}' is not declared in the {scope} section.", nameof(settingId));
        }

        /// <summary>Creates a private copy of the current common values for the dialog's controls to edit.</summary>
        /// <returns>Editable copy.</returns>
        public Dictionary<string, string> CreateCommonEditValues() {
            return new Dictionary<string, string>(CommonValues, StringComparer.Ordinal);
        }

        /// <summary>Creates a private copy of the current platform values for the dialog's controls to edit.</summary>
        /// <returns>Editable copy.</returns>
        public Dictionary<string, string> CreatePlatformEditValues() {
            return new Dictionary<string, string>(PlatformValues, StringComparer.Ordinal);
        }

        /// <summary>
        /// Applies the dialog's edited values and validates them against the declarations.
        /// </summary>
        /// <param name="commonEdits">Edited common values.</param>
        /// <param name="platformEdits">Edited platform values.</param>
        /// <returns>Rejected values; empty when every value is accepted.</returns>
        /// <exception cref="ArgumentException">Thrown when an edit names an undeclared setting.</exception>
        public IReadOnlyList<EditorPlaySettingIssue> ApplyEdits(IReadOnlyDictionary<string, string> commonEdits, IReadOnlyDictionary<string, string> platformEdits) {
            if (commonEdits == null) {
                throw new ArgumentNullException(nameof(commonEdits));
            } else if (platformEdits == null) {
                throw new ArgumentNullException(nameof(platformEdits));
            }

            List<EditorPlaySettingIssue> issues = new List<EditorPlaySettingIssue>();
            Dictionary<string, string> common = ValidateSection(CommonDefinitions, commonEdits, EditorPlaySettingScope.Common, issues);
            Dictionary<string, string> platform = ValidateSection(PlatformDefinitions, platformEdits, EditorPlaySettingScope.Platform, issues);
            CommonValues = common;
            PlatformValues = platform;
            EditIssues = issues;
            return issues;
        }

        /// <summary>
        /// Creates the document to save: a copy of the stored document with the common section and this platform's
        /// section replaced by the current values. Stored ids the declarations no longer know are therefore left out.
        /// </summary>
        /// <returns>Document to save.</returns>
        /// <exception cref="InvalidOperationException">Thrown while any current value is rejected, or before edits were applied to stored issues.</exception>
        public EditorPlaySettingsDocument CreateSavedDocument() {
            if (EditIssues.Count > 0) {
                throw new InvalidOperationException("Play settings cannot be saved while a value is invalid: " + EditIssues[0].Describe() + ".");
            }

            EditorPlaySettingsDocument saved = Document.Clone();
            saved.CommonValues.Clear();
            foreach (KeyValuePair<string, string> entry in CommonValues) {
                saved.CommonValues.Add(entry.Key, entry.Value);
            }
            saved.PlatformValues[PlatformId] = new Dictionary<string, string>(PlatformValues, StringComparer.Ordinal);
            return saved;
        }

        /// <summary>
        /// Saves <see cref="CreateSavedDocument"/> through the store. A file system failure (for example the replace being
        /// denied) is rethrown naming the Play settings file, so the dialog's status line tells the user which file failed.
        /// </summary>
        /// <param name="store">Store of the file this model was opened on.</param>
        /// <returns>The document that was saved.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the store is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the store writes a different file than this model was opened on.</exception>
        /// <exception cref="InvalidOperationException">Thrown while a value is invalid, or when the file cannot be written.</exception>
        public EditorPlaySettingsDocument Save(EditorPlaySettingsStore store) {
            if (store == null) {
                throw new ArgumentNullException(nameof(store));
            } else if (!string.Equals(Path.GetFullPath(store.FilePath), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"The Play settings store writes '{store.FilePath}', but the dialog was opened on '{FilePath}'.", nameof(store));
            }

            EditorPlaySettingsDocument saved = CreateSavedDocument();
            try {
                store.Save(saved);
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) {
                throw new InvalidOperationException($"Play settings could not be saved to '{FilePath}': {exception.Message}", exception);
            }

            return saved;
        }

        /// <summary>
        /// Moves stored common ids the engine does not declare out of the document and into the stored issues.
        /// </summary>
        /// <param name="document">Working copy of the stored document.</param>
        /// <param name="issues">Receives one issue per undeclared id.</param>
        static void RemoveUndeclaredCommonSettings(EditorPlaySettingsDocument document, List<EditorPlaySettingIssue> issues) {
            List<string> undeclaredIds = new List<string>();
            foreach (string settingId in document.CommonValues.Keys) {
                if (!PlayerCommonPlaySettings.IsCommonId(settingId)) {
                    undeclaredIds.Add(settingId);
                }
            }
            for (int index = 0; index < undeclaredIds.Count; index++) {
                string settingId = undeclaredIds[index];
                issues.Add(new EditorPlaySettingIssue(EditorPlaySettingScope.Common, settingId, document.CommonValues[settingId] ?? string.Empty, "no longer a common Play setting, Save removes it"));
                document.CommonValues.Remove(settingId);
            }
        }

        /// <summary>
        /// Moves stored platform ids the platform does not declare out of the section and into the stored issues, so the
        /// evaluator (which treats them as a broken file) never sees them.
        /// </summary>
        /// <param name="storedPlatformValues">Working copy of the platform's stored section.</param>
        /// <param name="platformId">Platform being edited.</param>
        /// <param name="capability">Play capability of the platform.</param>
        /// <param name="issues">Receives one issue per undeclared id.</param>
        static void RemoveUndeclaredPlatformSettings(Dictionary<string, string> storedPlatformValues, string platformId, PlatformPlayerCapability capability, List<EditorPlaySettingIssue> issues) {
            List<string> undeclaredIds = new List<string>();
            foreach (string settingId in storedPlatformValues.Keys) {
                if (capability.FindPlaySetting(settingId) == null) {
                    undeclaredIds.Add(settingId);
                }
            }
            for (int index = 0; index < undeclaredIds.Count; index++) {
                string settingId = undeclaredIds[index];
                issues.Add(new EditorPlaySettingIssue(EditorPlaySettingScope.Platform, settingId, storedPlatformValues[settingId] ?? string.Empty, $"platform '{platformId}' no longer declares it, Save removes it"));
                storedPlatformValues.Remove(settingId);
            }
        }

        /// <summary>
        /// Validates one edited section: every edit must name a declared setting, and every declared setting must have an
        /// accepted value.
        /// </summary>
        /// <param name="definitions">Declarations of the section.</param>
        /// <param name="edits">Edited values.</param>
        /// <param name="scope">Section scope.</param>
        /// <param name="issues">Receives rejected values.</param>
        /// <returns>The section values.</returns>
        /// <exception cref="ArgumentException">Thrown when an edit names an undeclared setting.</exception>
        static Dictionary<string, string> ValidateSection(
            PlatformSettingDefinition[] definitions,
            IReadOnlyDictionary<string, string> edits,
            EditorPlaySettingScope scope,
            List<EditorPlaySettingIssue> issues) {
            foreach (string settingId in edits.Keys) {
                if (!Array.Exists(definitions, definition => string.Equals(definition.SettingId, settingId, StringComparison.Ordinal))) {
                    throw new ArgumentException($"Play setting '{settingId}' is not declared in the {scope} section.", nameof(edits));
                }
            }

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Length; index++) {
                PlatformSettingDefinition definition = definitions[index];
                edits.TryGetValue(definition.SettingId, out string value);
                values.Add(definition.SettingId, value ?? string.Empty);
                if (!EditorPlaySettingValueValidator.TryValidate(definition, value, out string reason)) {
                    issues.Add(new EditorPlaySettingIssue(scope, definition.SettingId, value ?? string.Empty, reason));
                }
            }

            return values;
        }
    }
}
