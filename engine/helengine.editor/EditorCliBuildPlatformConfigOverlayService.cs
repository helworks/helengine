namespace helengine.editor {
    /// <summary>
    /// Applies shared per-platform profile settings onto local CLI build execution state without disturbing scene selection or output paths.
    /// </summary>
    internal static class EditorCliBuildPlatformConfigOverlayService {
        /// <summary>
        /// Finds the shared platform profile settings record for the requested platform identifier.
        /// </summary>
        /// <param name="document">Aggregated shared platform profile settings document.</param>
        /// <param name="platformId">Platform identifier whose shared settings should be located.</param>
        /// <returns>Matching shared platform profile settings when present; otherwise <c>null</c>.</returns>
        internal static EditorPlatformProfileSettingsDocument FindPlatformSettings(EditorProfileSettingsDocument document, string platformId) {
            if (document == null || document.Platforms == null || string.IsNullOrWhiteSpace(platformId)) {
                return null;
            }

            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorPlatformProfileSettingsDocument platformSettings = document.Platforms[index];
                if (platformSettings == null) {
                    continue;
                } else if (!string.Equals(platformSettings.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return platformSettings;
            }

            return null;
        }

        /// <summary>
        /// Replaces local build selections with the canonical shared platform build settings for headless CLI builds.
        /// </summary>
        /// <param name="platformConfig">Local platform configuration that should receive the shared profile selections.</param>
        /// <param name="sharedPlatformSettings">Shared platform profile settings that own the canonical build selections.</param>
        internal static void ApplySharedProfileSettings(
            EditorBuildPlatformConfigDocument platformConfig,
            EditorPlatformProfileSettingsDocument sharedPlatformSettings) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            } else if (sharedPlatformSettings == null) {
                throw new ArgumentNullException(nameof(sharedPlatformSettings));
            }

            if (sharedPlatformSettings.Build != null) {
                platformConfig.SelectedBuildProfileId = sharedPlatformSettings.Build.SelectedBuildProfileId ?? string.Empty;
                platformConfig.SelectedBuildOptionValues = new Dictionary<string, string>(
                    sharedPlatformSettings.Build.SelectedOptionValues ?? [],
                    StringComparer.Ordinal);
            }
        }
    }
}
