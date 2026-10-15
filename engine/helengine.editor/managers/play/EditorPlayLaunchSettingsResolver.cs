using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.editor {
    /// <summary>
    /// Turns the Play settings captured at press time into the fully resolved settings the player receives, once the cook
    /// has reported the project's graphics option values and shadow policy (both also captured at press time). On a platform
    /// with video modes, the resolved video-mode setting selects the mode, and <c>frame-rate: platform</c> paces at the
    /// mode's player frame rate instead of the capability's.
    /// </summary>
    public static class EditorPlayLaunchSettingsResolver {
        /// <summary>
        /// Resolves the player settings for one Play.
        /// </summary>
        /// <param name="snapshot">Settings captured when Play was pressed.</param>
        /// <param name="cookResult">Successful cook outcome.</param>
        /// <returns>Resolved player settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a setting follows the project but the project has no acceptable value.</exception>
        /// <exception cref="ArgumentException">Thrown when the cook did not succeed; its message is carried in the exception.</exception>
        public static PlayerPlaySettings Resolve(EditorPlaySettingsSnapshot snapshot, EditorPlayCookResult cookResult) {
            if (snapshot == null) {
                throw new ArgumentNullException(nameof(snapshot));
            } else if (cookResult == null) {
                throw new ArgumentNullException(nameof(cookResult));
            } else if (!cookResult.Succeeded) {
                throw new ArgumentException($"Play settings resolve only against a successful cook, but the cook failed: {cookResult.Message}", nameof(cookResult));
            }

            Dictionary<string, string> platformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            PlatformSettingDefinition[] declarations = snapshot.Capability.PlaySettings;
            for (int index = 0; index < declarations.Length; index++) {
                PlatformSettingDefinition declaration = declarations[index];
                string value = snapshot.PlatformValues[declaration.SettingId];
                if (string.Equals(value, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal)) {
                    value = ResolveProjectValue(snapshot.PlatformId, declaration, cookResult.GraphicsOptionValues);
                }
                platformValues.Add(declaration.SettingId, value);
            }

            PlatformVideoMode videoMode = null;
            if (snapshot.HasVideoModes) {
                if (!platformValues.TryGetValue(snapshot.VideoModeCapability.SettingId, out string videoModeId)) {
                    throw new InvalidOperationException($"Platform '{snapshot.PlatformId}' declares video modes through setting '{snapshot.VideoModeCapability.SettingId}', but its Play capability declares no such Play setting.");
                }
                videoMode = snapshot.VideoModeCapability.Resolve(videoModeId, snapshot.PlatformId);
            }

            bool unlimited = string.Equals(snapshot.CommonValues[PlayerCommonPlaySettings.FrameRateId], "unlimited", StringComparison.Ordinal);
            double targetFrameRate = unlimited ? 0d : (videoMode != null ? videoMode.PlayerFrameRate : snapshot.Capability.TargetFrameRate);
            bool playShadows = string.Equals(snapshot.CommonValues[PlayerCommonPlaySettings.ShadowsId], "true", StringComparison.Ordinal);
            return new PlayerPlaySettings(
                PlayerWindowScaleText.Parse(snapshot.CommonValues[PlayerCommonPlaySettings.WindowScaleId]),
                PlayerWindowMonitorText.Parse(snapshot.CommonValues[PlayerCommonPlaySettings.WindowMonitorId]),
                snapshot.EditorProcessId,
                snapshot.EffectiveWaitForDebugger,
                playShadows && cookResult.ShadowsEnabled,
                unlimited,
                targetFrameRate,
                platformValues,
                videoMode);
        }

        /// <summary>
        /// Resolves a <c>project</c> value to the project graphics option with the same id and checks the Play setting accepts it.
        /// </summary>
        /// <param name="platformId">Platform being played, named in messages.</param>
        /// <param name="declaration">Play setting declaration.</param>
        /// <param name="graphicsOptionValues">Project graphics option values captured at press time.</param>
        /// <returns>The concrete project value.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the project has no value, sets the <c>project</c> sentinel itself, or selects a value the Play setting
        /// rejects (the message carries the validator's reason, which lists the allowed values for a choice).
        /// </exception>
        static string ResolveProjectValue(string platformId, PlatformSettingDefinition declaration, IReadOnlyDictionary<string, string> graphicsOptionValues) {
            string settingsFile = "settings/platform." + platformId + ".json";
            if (!graphicsOptionValues.TryGetValue(declaration.SettingId, out string projectValue) || string.IsNullOrWhiteSpace(projectValue)) {
                throw new InvalidOperationException($"Play setting '{declaration.SettingId}' follows the project, but the selected graphics settings in {settingsFile} have no '{declaration.SettingId}' value.");
            } else if (string.Equals(projectValue, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Play setting '{declaration.SettingId}' follows the project, but {settingsFile} also sets '{declaration.SettingId}' to '{PlayerCommonPlaySettings.ProjectValue}', which is not a concrete value; select a concrete '{declaration.SettingId}' in the project's graphics settings.");
            } else if (!EditorPlaySettingValueValidator.TryValidate(declaration, projectValue, out string reason)) {
                throw new InvalidOperationException($"Play setting '{declaration.SettingId}' follows the project, but {settingsFile} selects '{projectValue}', which Play does not accept ({reason}).");
            }

            return projectValue;
        }
    }
}
