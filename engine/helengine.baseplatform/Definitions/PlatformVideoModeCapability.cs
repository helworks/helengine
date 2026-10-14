namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// The closed list of video modes a console platform offers, selected by one graphics-profile choice setting. The
    /// mode stays fixed from boot to shutdown.
    /// </summary>
    public sealed class PlatformVideoModeCapability {
        /// <summary>
        /// Initializes the capability.
        /// </summary>
        /// <param name="settingId">Graphics setting id that selects the mode (and the platform's Play setting id).</param>
        /// <param name="modes">Every mode the hardware produces, in display order.</param>
        /// <exception cref="ArgumentException">Thrown for a blank id, an empty list, a null entry or a repeated mode id.</exception>
        public PlatformVideoModeCapability(string settingId, PlatformVideoMode[] modes) {
            if (string.IsNullOrWhiteSpace(settingId)) {
                throw new ArgumentException("A video-mode capability needs the setting id that selects the mode.", nameof(settingId));
            } else if (modes == null || modes.Length == 0) {
                throw new ArgumentException("A video-mode capability needs at least one mode.", nameof(modes));
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < modes.Length; index++) {
                if (modes[index] == null) {
                    throw new ArgumentException("Video modes cannot contain null entries.", nameof(modes));
                } else if (!ids.Add(modes[index].Id)) {
                    throw new ArgumentException($"Video mode '{modes[index].Id}' is declared more than once.", nameof(modes));
                }
            }

            SettingId = settingId;
            Modes = [.. modes];
        }

        /// <summary>Gets the graphics (and Play) setting id that selects the mode.</summary>
        public string SettingId { get; }

        /// <summary>Gets every mode, in display order.</summary>
        public PlatformVideoMode[] Modes { get; }

        /// <summary>
        /// Gets the mode ids in display order, the exact allowed values of the selecting choice setting.
        /// </summary>
        /// <returns>New array of mode ids.</returns>
        public string[] GetModeIds() {
            string[] ids = new string[Modes.Length];
            for (int index = 0; index < Modes.Length; index++) {
                ids[index] = Modes[index].Id;
            }

            return ids;
        }

        /// <summary>
        /// Reports whether a mode id is declared, compared ordinally.
        /// </summary>
        /// <param name="modeId">Mode id.</param>
        /// <returns>True when declared.</returns>
        public bool Contains(string modeId) {
            return Array.Exists(Modes, mode => string.Equals(mode.Id, modeId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Returns the mode for an id.
        /// </summary>
        /// <param name="modeId">Mode id.</param>
        /// <param name="platformId">Platform named in the failure.</param>
        /// <returns>The mode.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the id is not declared.</exception>
        public PlatformVideoMode Resolve(string modeId, string platformId) {
            for (int index = 0; index < Modes.Length; index++) {
                if (string.Equals(Modes[index].Id, modeId, StringComparison.Ordinal)) {
                    return Modes[index];
                }
            }

            throw new InvalidOperationException($"Video mode '{modeId}' is not one of platform '{platformId}''s modes: {string.Join(", ", GetModeIds())}.");
        }
    }
}
