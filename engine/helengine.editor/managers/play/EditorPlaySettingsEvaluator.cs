using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Evaluates the stored Play settings for one platform against the current declarations (engine common settings plus
    /// the platform's <see cref="PlatformPlayerCapability.PlaySettings"/>). Invalid values become issues so the dialog can
    /// highlight them; an id the platform does not declare is a broken file and throws.
    /// </summary>
    public static class EditorPlaySettingsEvaluator {
        /// <summary>
        /// Evaluates the stored settings for one platform.
        /// </summary>
        /// <param name="document">Stored Play settings.</param>
        /// <param name="filePath">File the document came from, named in messages.</param>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="capability">Play capability of that platform.</param>
        /// <returns>Effective values and issues.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the platform section names an undeclared setting.</exception>
        public static EditorPlaySettingsEvaluation Evaluate(EditorPlaySettingsDocument document, string filePath, string platformId, PlatformPlayerCapability capability) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            } else if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("The Play settings file path is required.", nameof(filePath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("The platform id is required.", nameof(platformId));
            } else if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            }

            List<EditorPlaySettingIssue> issues = new List<EditorPlaySettingIssue>();
            Dictionary<string, string> commonValues = ResolveSection(PlayerCommonPlaySettings.CreateDefinitions(), document.CommonValues, EditorPlaySettingScope.Common, issues);
            if (!document.PlatformValues.TryGetValue(platformId, out Dictionary<string, string> storedPlatformValues)) {
                storedPlatformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            foreach (string settingId in storedPlatformValues.Keys) {
                if (capability.FindPlaySetting(settingId) == null) {
                    throw new InvalidOperationException($"Play settings file '{filePath}' names setting '{settingId}' for platform '{platformId}', which the platform does not declare. Open Play Settings... in the Play platform selector and Save to remove it.");
                }
            }

            Dictionary<string, string> platformValues = ResolveSection(capability.PlaySettings, storedPlatformValues, EditorPlaySettingScope.Platform, issues);
            return new EditorPlaySettingsEvaluation(filePath, platformId, commonValues, platformValues, issues);
        }

        /// <summary>
        /// Resolves one section: the stored value when present (validated), otherwise the declared default.
        /// </summary>
        /// <param name="definitions">Declarations of the section.</param>
        /// <param name="stored">Stored values of the section.</param>
        /// <param name="scope">Section scope, recorded on issues.</param>
        /// <param name="issues">Receives rejected stored values.</param>
        /// <returns>Effective values keyed by id.</returns>
        static Dictionary<string, string> ResolveSection(
            PlatformSettingDefinition[] definitions,
            Dictionary<string, string> stored,
            EditorPlaySettingScope scope,
            List<EditorPlaySettingIssue> issues) {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Length; index++) {
                PlatformSettingDefinition definition = definitions[index];
                if (stored.TryGetValue(definition.SettingId, out string storedValue)) {
                    values.Add(definition.SettingId, storedValue);
                    if (!EditorPlaySettingValueValidator.TryValidate(definition, storedValue, out string reason)) {
                        issues.Add(new EditorPlaySettingIssue(scope, definition.SettingId, storedValue ?? string.Empty, reason));
                    }
                } else {
                    values.Add(definition.SettingId, definition.DefaultValue);
                }
            }

            return values;
        }
    }
}
