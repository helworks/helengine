using System.Globalization;

namespace helengine.editor {
    /// <summary>Parses loading controls atomically so invalid edits cannot partially change saved runtime configuration.</summary>
    public static class EditorRuntimeLoadingSettingsEditService {
        /// <summary>Applies valid user defaults and platform overrides; blank platform values inherit and a screen dash explicitly disables the screen.</summary>
        public static bool TryApply(EditorBuildConfigDocument document, EditorBuildPlatformConfigDocument platform,
            string defaultBudget, string platformBudget, string defaultScreen, string platformScreen) {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (platform == null) { throw new ArgumentNullException(nameof(platform)); }
            if (!TryParseBudget(defaultBudget, false, out double globalValue)
                || !TryParseBudget(platformBudget, true, out double platformValue)) { return false; }
            string overrideScreen = platformScreen?.Trim() ?? string.Empty;
            document.SceneLoadingBudgetMilliseconds = globalValue;
            document.LoadingScreenSceneId = defaultScreen?.Trim() ?? string.Empty;
            platform.SceneLoadingBudgetMilliseconds = platformValue;
            platform.OverridesLoadingScreenSceneId = overrideScreen.Length > 0;
            platform.LoadingScreenSceneId = overrideScreen == "-" ? string.Empty : overrideScreen;
            return true;
        }

        /// <summary>Accepts finite positive budgets, with blank or zero reserved for an inherited platform value.</summary>
        public static bool TryParseBudget(string text, bool allowsInheritance, out double value) {
            value = 0d;
            if (string.IsNullOrWhiteSpace(text)) { return allowsInheritance; }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { return false; }
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && (allowsInheritance || value > 0d);
        }
    }
}
