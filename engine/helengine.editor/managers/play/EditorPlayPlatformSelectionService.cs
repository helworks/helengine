namespace helengine.editor {
    /// <summary>
    /// Owns the viewport Play platform choice: which platforms can be offered, which one is selected and how the choice
    /// is persisted as `lastPlayPlatform` in `user_settings/project.json`. It is independent of the editor preview
    /// platform (`activePlatform`), which it never reads or writes.
    /// </summary>
    public sealed class EditorPlayPlatformSelectionService {
        /// <summary>
        /// Local project settings that persist the last chosen Play platform.
        /// </summary>
        readonly EditorProjectLocalSettingsService LocalSettings;

        /// <summary>
        /// Receives the one-line warning written when the saved Play platform can no longer be played.
        /// </summary>
        readonly Action<string> WarningSink;

        /// <summary>
        /// Playable platforms in the project's supported-platform order.
        /// </summary>
        readonly List<EditorPlayPlatformOption> OptionList;

        /// <summary>
        /// Currently selected option, or null when no platform is playable.
        /// </summary>
        EditorPlayPlatformOption Selected;

        /// <summary>
        /// Saved platform id that was last reported as not playable, so repeated resolutions warn only once.
        /// </summary>
        string LastWarnedPlatformId;

        /// <summary>
        /// Creates the selection service over the project's local settings.
        /// </summary>
        /// <param name="localSettings">Local settings that store `lastPlayPlatform`.</param>
        /// <param name="warningSink">Receives the warning written when the saved Play platform is not playable.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public EditorPlayPlatformSelectionService(EditorProjectLocalSettingsService localSettings, Action<string> warningSink) {
            if (localSettings == null) {
                throw new ArgumentNullException(nameof(localSettings));
            } else if (warningSink == null) {
                throw new ArgumentNullException(nameof(warningSink));
            }

            LocalSettings = localSettings;
            WarningSink = warningSink;
            OptionList = new List<EditorPlayPlatformOption>();
            LastWarnedPlatformId = string.Empty;
        }

        /// <summary>
        /// Gets the platforms the selector offers, in the project's supported-platform order.
        /// </summary>
        public IReadOnlyList<EditorPlayPlatformOption> Options => OptionList;

        /// <summary>
        /// Gets whether a playable platform is selected; when false, Play and the selector are inert.
        /// </summary>
        public bool HasSelection => Selected != null;

        /// <summary>
        /// Gets the selected Play platform.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no platform is playable.</exception>
        public EditorPlayPlatformOption SelectedOption {
            get {
                if (Selected == null) {
                    throw new InvalidOperationException("No playable platform is available for Play.");
                }

                return Selected;
            }
        }

        /// <summary>
        /// Rebuilds the option list and resolves the selection: the saved `lastPlayPlatform` when it is still playable,
        /// otherwise the first playable platform (warning once when a saved choice had to be replaced), or nothing when
        /// no platform is playable. The fallback is not written back, so the saved choice returns once it is playable.
        /// </summary>
        /// <param name="supportedPlatformIds">Project-supported platform ids in project order.</param>
        /// <param name="loadedBuilders">Builders the platform catalog already loaded; any order, may include
        /// platforms the project does not support.</param>
        /// <exception cref="ArgumentNullException">Thrown when either list is null.</exception>
        public void Refresh(IReadOnlyList<string> supportedPlatformIds, IReadOnlyList<EditorLoadedPlatformBuilder> loadedBuilders) {
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            } else if (loadedBuilders == null) {
                throw new ArgumentNullException(nameof(loadedBuilders));
            }

            OptionList.Clear();
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                EditorLoadedPlatformBuilder builder = FindPlayableBuilder(loadedBuilders, supportedPlatformIds[index]);
                if (builder != null) {
                    OptionList.Add(new EditorPlayPlatformOption(builder));
                }
            }

            string savedPlatformId = LocalSettings.LoadLastPlayPlatform();
            EditorPlayPlatformOption savedOption = FindOption(savedPlatformId);
            if (savedOption != null) {
                Selected = savedOption;
                return;
            }

            Selected = OptionList.Count > 0 ? OptionList[0] : null;
            if (savedPlatformId.Length == 0 || string.Equals(LastWarnedPlatformId, savedPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            LastWarnedPlatformId = savedPlatformId;
            if (Selected == null) {
                WarningSink($"Saved Play platform '{savedPlatformId}' is not playable and no other platform supports Play; Play is unavailable.");
            } else {
                WarningSink($"Saved Play platform '{savedPlatformId}' is not playable; Play uses '{Selected.PlatformId}' instead.");
            }
        }

        /// <summary>
        /// Reports whether the supplied platform is currently offered, so callers can reject stale picks before
        /// <see cref="Select"/>.
        /// </summary>
        /// <param name="platformId">Platform id to check; blank ids are never offered.</param>
        /// <returns>True when the platform is among <see cref="Options"/>.</returns>
        public bool Offers(string platformId) {
            return FindOption(platformId) != null;
        }

        /// <summary>
        /// Selects one offered platform for Play and persists it as `lastPlayPlatform`.
        /// </summary>
        /// <param name="platformId">Id of an offered platform.</param>
        /// <exception cref="InvalidOperationException">Thrown when the platform is not among the offered options.</exception>
        public void Select(string platformId) {
            EditorPlayPlatformOption option = FindOption(platformId);
            if (option == null) {
                throw new InvalidOperationException($"Platform '{platformId}' is not available for Play.");
            }

            LocalSettings.SaveLastPlayPlatform(option.PlatformId);
            Selected = option;
            LastWarnedPlatformId = string.Empty;
        }

        /// <summary>
        /// Finds the offered option with the supplied platform id.
        /// </summary>
        /// <param name="platformId">Platform id to look up; blank ids match nothing.</param>
        /// <returns>The matching option, or null when the platform is not offered.</returns>
        EditorPlayPlatformOption FindOption(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return null;
            }

            for (int index = 0; index < OptionList.Count; index++) {
                if (string.Equals(OptionList[index].PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return OptionList[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the loaded builder for one platform when that platform is installed and its builder declares Play
        /// support.
        /// </summary>
        /// <param name="loadedBuilders">Builders the platform catalog already loaded.</param>
        /// <param name="platformId">Project-supported platform id.</param>
        /// <returns>The playable builder, or null when the platform is not installed, not loaded or not playable.</returns>
        static EditorLoadedPlatformBuilder FindPlayableBuilder(IReadOnlyList<EditorLoadedPlatformBuilder> loadedBuilders, string platformId) {
            for (int index = 0; index < loadedBuilders.Count; index++) {
                EditorLoadedPlatformBuilder builder = loadedBuilders[index];
                if (builder == null) {
                    throw new ArgumentException("Loaded builders must not contain null entries.", nameof(loadedBuilders));
                } else if (!string.Equals(builder.PlatformDescriptor.Id, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (!builder.PlatformDescriptor.IsInstalled || builder.Definition.PlayerCapability == null || !builder.Definition.PlayerCapability.SupportsPlay) {
                    return null;
                }

                return builder;
            }

            return null;
        }
    }
}
