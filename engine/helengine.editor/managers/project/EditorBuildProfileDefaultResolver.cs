using helengine.baseplatform.Definitions;

namespace helengine.editor;

/// <summary>
/// Resolves build-profile-bound defaults while preserving explicit user overrides across debug and release build-mode switches.
/// </summary>
static class EditorBuildProfileDefaultResolver {
    /// <summary>
    /// Resolves the effective build profile for the current build mode.
    /// </summary>
    /// <param name="selectionModel">Platform selection metadata that owns the available build profiles.</param>
    /// <param name="selectedBuildProfileId">Currently selected build-profile identifier persisted by the editor.</param>
    /// <param name="debugBuild">True when the current build targets the debug flavor.</param>
    /// <returns>Resolved build profile for the current build mode, or null when the platform exposes no build profiles.</returns>
    public static PlatformBuildProfileDefinition ResolveBuildProfile(
        EditorPlatformBuildSelectionModel selectionModel,
        string selectedBuildProfileId,
        bool debugBuild) {
        if (selectionModel == null) {
            throw new ArgumentNullException(nameof(selectionModel));
        }

        PlatformBuildProfileDefinition canonicalDebugProfile = selectionModel.TryResolveBuildProfileExact("debug");
        PlatformBuildProfileDefinition canonicalReleaseProfile = selectionModel.TryResolveBuildProfileExact("release");
        if (canonicalDebugProfile != null && canonicalReleaseProfile != null) {
            if (!string.IsNullOrWhiteSpace(selectedBuildProfileId)
                && !string.Equals(selectedBuildProfileId, canonicalDebugProfile.ProfileId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(selectedBuildProfileId, canonicalReleaseProfile.ProfileId, StringComparison.OrdinalIgnoreCase)) {
                PlatformBuildProfileDefinition explicitlySelectedProfile = selectionModel.TryResolveBuildProfileExact(selectedBuildProfileId);
                if (explicitlySelectedProfile != null) {
                    return explicitlySelectedProfile;
                }
            }

            return debugBuild ? canonicalDebugProfile : canonicalReleaseProfile;
        }

        PlatformBuildProfileDefinition explicitlySelectedProfileWithoutCanonicalPair = selectionModel.TryResolveBuildProfileExact(selectedBuildProfileId);
        if (explicitlySelectedProfileWithoutCanonicalPair != null) {
            return explicitlySelectedProfileWithoutCanonicalPair;
        }

        PlatformBuildProfileDefinition buildModeProfile = selectionModel.TryResolveBuildProfileExact(debugBuild ? "debug" : "release");
        if (buildModeProfile != null) {
            return buildModeProfile;
        }

        return selectionModel.ResolveBuildProfile(string.Empty);
    }

    /// <summary>
    /// Synchronizes one selected dependent profile identifier with a newly resolved build-profile default while preserving explicit user overrides.
    /// </summary>
    /// <param name="selectedProfileId">Currently selected dependent profile identifier.</param>
    /// <param name="previousDefaultProfileId">Dependent profile identifier previously inherited from the old build profile.</param>
    /// <param name="currentDefaultProfileId">Dependent profile identifier inherited from the newly resolved build profile.</param>
    public static void SynchronizeBoundProfileSelection(
        ref string selectedProfileId,
        string previousDefaultProfileId,
        string currentDefaultProfileId) {
        if (string.IsNullOrWhiteSpace(currentDefaultProfileId)) {
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedProfileId)
            || string.Equals(selectedProfileId, previousDefaultProfileId, StringComparison.OrdinalIgnoreCase)) {
            selectedProfileId = currentDefaultProfileId;
        }
    }

    /// <summary>
    /// Creates the effective selected codegen option map for one build by applying build-profile-specific defaults only when the current values still match their previous implicit defaults.
    /// </summary>
    /// <param name="selectedCodegenOptionValues">Persisted selected codegen option values captured by the editor.</param>
    /// <param name="codegenProfile">Resolved codegen profile whose shared setting defaults remain the final fallback.</param>
    /// <param name="previousBuildProfile">Previously selected build profile whose inherited defaults may still be reflected in the persisted values.</param>
    /// <param name="currentBuildProfile">Currently resolved build profile whose inherited defaults should now apply.</param>
    /// <returns>Effective codegen option values for the current build.</returns>
    public static Dictionary<string, string> CreateEffectiveCodegenOptionValues(
        IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
        PlatformCodegenProfileDefinition codegenProfile,
        PlatformBuildProfileDefinition previousBuildProfile,
        PlatformBuildProfileDefinition currentBuildProfile) {
        Dictionary<string, string> effectiveValues = selectedCodegenOptionValues == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(selectedCodegenOptionValues, StringComparer.OrdinalIgnoreCase);

        if (codegenProfile?.Settings == null) {
            return effectiveValues;
        }

        for (int index = 0; index < codegenProfile.Settings.Length; index++) {
            PlatformSettingDefinition setting = codegenProfile.Settings[index];
            string currentDefaultValue = ResolveCodegenSettingDefaultValue(currentBuildProfile, codegenProfile, setting.SettingId);
            string previousDefaultValue = ResolveCodegenSettingDefaultValue(previousBuildProfile, codegenProfile, setting.SettingId);
            if (!effectiveValues.TryGetValue(setting.SettingId, out string existingValue)
                || string.IsNullOrWhiteSpace(existingValue)
                || string.Equals(existingValue, previousDefaultValue, StringComparison.OrdinalIgnoreCase)) {
                effectiveValues[setting.SettingId] = currentDefaultValue;
            }
        }

        return effectiveValues;
    }

    /// <summary>
    /// Resolves one effective codegen-setting default value by honoring build-profile-specific overrides before the shared codegen profile default.
    /// </summary>
    /// <param name="buildProfile">Resolved build profile that may override shared codegen defaults.</param>
    /// <param name="codegenProfile">Resolved codegen profile that owns the shared setting definitions.</param>
    /// <param name="settingId">Stable codegen-setting identifier to resolve.</param>
    /// <returns>Effective default value for the requested setting, or an empty string when the setting is unavailable.</returns>
    public static string ResolveCodegenSettingDefaultValue(
        PlatformBuildProfileDefinition buildProfile,
        PlatformCodegenProfileDefinition codegenProfile,
        string settingId) {
        if (buildProfile?.CodegenSettingDefaultValues != null
            && !string.IsNullOrWhiteSpace(settingId)
            && buildProfile.CodegenSettingDefaultValues.TryGetValue(settingId, out string buildProfileOverrideValue)
            && !string.IsNullOrWhiteSpace(buildProfileOverrideValue)) {
            return buildProfileOverrideValue;
        }

        if (codegenProfile?.Settings == null || string.IsNullOrWhiteSpace(settingId)) {
            return string.Empty;
        }

        for (int index = 0; index < codegenProfile.Settings.Length; index++) {
            PlatformSettingDefinition setting = codegenProfile.Settings[index];
            if (string.Equals(setting.SettingId, settingId, StringComparison.Ordinal)) {
                return setting.DefaultValue ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
