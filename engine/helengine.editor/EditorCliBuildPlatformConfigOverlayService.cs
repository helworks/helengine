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
        /// Replaces local build, graphics and codegen selections with their canonical shared platform settings for headless CLI builds.
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
            if (sharedPlatformSettings.Graphics != null) {
                platformConfig.SelectedGraphicsProfileId = sharedPlatformSettings.Graphics.SelectedGraphicsProfileId ?? string.Empty;
                platformConfig.SelectedGraphicsOptionValues = new Dictionary<string, string>(
                    sharedPlatformSettings.Graphics.SelectedOptionValues ?? [],
                    StringComparer.Ordinal);
            }
            if (sharedPlatformSettings.Codegen != null) {
                platformConfig.SelectedCodegenProfileId = sharedPlatformSettings.Codegen.SelectedCodegenProfileId ?? string.Empty;
                platformConfig.SelectedCodegenOptionValues = new Dictionary<string, string>(
                    sharedPlatformSettings.Codegen.SelectedOptionValues ?? [],
                    StringComparer.Ordinal);
            }
        }
        /// <summary>Applies an explicit CLI profile and aligns canonical build modes so the legacy toggle cannot reverse the request.</summary>
        /// <param name="platformConfig">Invocation-local configuration consumed by the build queue.</param>
        /// <param name="profileId">Explicitly requested profile; custom profiles retain the independent debug overlay choice.</param>
        internal static void ApplyRequestedBuildProfile(EditorBuildPlatformConfigDocument platformConfig, string profileId) {
            ArgumentNullException.ThrowIfNull(platformConfig);
            ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
            platformConfig.SelectedBuildProfileId = profileId;
            if (string.Equals(profileId, "debug", StringComparison.OrdinalIgnoreCase)) {
                platformConfig.DebugBuild = true;
            } else if (string.Equals(profileId, "release", StringComparison.OrdinalIgnoreCase)) {
                platformConfig.DebugBuild = false;
            }
        }

        /// <summary>Applies an invocation profile using the existing defaults policy before the previous selection is lost.</summary>
        /// <param name="platformConfig">Invocation-local persisted selections and inherited options.</param>
        /// <param name="profileId">Explicit CLI build profile.</param>
        /// <param name="selectionModel">Actual builder metadata used to resolve profile-bound selections and defaults.</param>
        internal static void ApplyRequestedBuildProfile(EditorBuildPlatformConfigDocument platformConfig, string profileId, EditorPlatformBuildSelectionModel selectionModel) {
            ArgumentNullException.ThrowIfNull(platformConfig);
            ArgumentNullException.ThrowIfNull(selectionModel);
            string previousBuildProfileId = platformConfig.SelectedBuildProfileId;
            ApplyRequestedBuildProfile(platformConfig, profileId);
            EditorBuildPlatformConfigDefaultsService.EnsurePlatformSelectionDefaults(platformConfig, selectionModel, previousBuildProfileId);
        }

        /// <summary>Applies invocation profile and numeric selections in one execution path shared by the CLI and regression tests.</summary>
        /// <param name="platformConfig">Invocation-local persisted platform state.</param>
        /// <param name="options">Explicit invocation settings.</param>
        /// <param name="selectionModel">Builder metadata for inherited profile defaults.</param>
        internal static void ApplyRequestedOptions(EditorBuildPlatformConfigDocument platformConfig, EditorCliBuildOptions options, EditorPlatformBuildSelectionModel selectionModel) {
            ArgumentNullException.ThrowIfNull(options);
            // Normalize stale persisted precision before profile-default validation reads it.
            ApplyNumericOptions(platformConfig, options);
            if (!string.IsNullOrWhiteSpace(options.BuildProfileId)) {
                ApplyRequestedBuildProfile(platformConfig, options.BuildProfileId, selectionModel);
                // Explicit invocation values still win when they equal the old profile's inherited defaults.
                ApplyNumericOptions(platformConfig, options);
            }
        }

        /// <summary>Applies invocation-only precision after persisted settings without rewriting their dictionaries.</summary>
        internal static void ApplyNumericOptions(EditorBuildPlatformConfigDocument platformConfig, EditorCliBuildOptions options) {
            ArgumentNullException.ThrowIfNull(platformConfig);
            ArgumentNullException.ThrowIfNull(options);
            Dictionary<string, string> selected = new(platformConfig.SelectedCodegenOptionValues ?? []);
            foreach (KeyValuePair<string, string> option in options.NumericOptionValues) {
                foreach (string key in selected.Keys.Where(key => string.Equals(key, option.Key, StringComparison.OrdinalIgnoreCase)).ToArray()) {
                    selected.Remove(key);
                }
                selected[option.Key] = option.Value;
            }
            platformConfig.SelectedCodegenOptionValues = selected;
        }
    }
}
