using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Checks one Play setting value against its declaration without throwing: a boolean must be the text <c>true</c> or
    /// <c>false</c>, a choice one of its allowed values (ordinal), and a required text non-blank.
    /// </summary>
    public static class EditorPlaySettingValueValidator {
        /// <summary>
        /// Reports whether a value is acceptable for a declaration.
        /// </summary>
        /// <param name="definition">Setting declaration.</param>
        /// <param name="value">Value to check.</param>
        /// <param name="reason">Why the value is rejected; empty when accepted.</param>
        /// <returns>True when the value is acceptable.</returns>
        public static bool TryValidate(PlatformSettingDefinition definition, string value, out string reason) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (value == null) {
                reason = "no value";
                return false;
            } else if (definition.SettingKind == PlatformSettingKind.Boolean
                && !string.Equals(value, "true", StringComparison.Ordinal)
                && !string.Equals(value, "false", StringComparison.Ordinal)) {
                reason = "must be true or false";
                return false;
            } else if (definition.SettingKind == PlatformSettingKind.Choice && Array.IndexOf(definition.AllowedValues, value) < 0) {
                reason = "not one of " + string.Join(", ", definition.AllowedValues);
                return false;
            } else if (definition.SettingKind == PlatformSettingKind.Text && definition.Required && string.IsNullOrWhiteSpace(value)) {
                reason = "a value is required";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
