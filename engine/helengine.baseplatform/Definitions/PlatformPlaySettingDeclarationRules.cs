namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// Checks one Play setting declaration so the editor can always render and validate it: a choice offers values and
    /// defaults to one of them, and a boolean defaults to the text <c>true</c> or <c>false</c>.
    /// </summary>
    public static class PlatformPlaySettingDeclarationRules {
        /// <summary>
        /// Throws when the declaration cannot be rendered or validated by the Play Settings dialog.
        /// </summary>
        /// <param name="setting">Declaration to check.</param>
        /// <param name="parameterName">Parameter reported by the exception.</param>
        /// <exception cref="ArgumentException">Thrown when the declaration is null or inconsistent.</exception>
        public static void Validate(PlatformSettingDefinition setting, string parameterName) {
            if (setting == null) {
                throw new ArgumentException("Play setting declarations cannot contain null entries.", parameterName);
            } else if (setting.SettingKind == PlatformSettingKind.Choice && setting.AllowedValues.Length == 0) {
                throw new ArgumentException($"Play setting '{setting.SettingId}' is a choice but declares no allowed values.", parameterName);
            } else if (setting.SettingKind == PlatformSettingKind.Choice && Array.IndexOf(setting.AllowedValues, setting.DefaultValue) < 0) {
                throw new ArgumentException($"Play setting '{setting.SettingId}' defaults to '{setting.DefaultValue}', which is not one of its allowed values.", parameterName);
            } else if (setting.SettingKind == PlatformSettingKind.Boolean
                && !string.Equals(setting.DefaultValue, "true", StringComparison.Ordinal)
                && !string.Equals(setting.DefaultValue, "false", StringComparison.Ordinal)) {
                throw new ArgumentException($"Play setting '{setting.SettingId}' is a boolean but defaults to '{setting.DefaultValue}' instead of true or false.", parameterName);
            }
        }
    }
}
