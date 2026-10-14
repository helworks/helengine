namespace helengine.editor {
    /// <summary>
    /// Builds the items of the viewport Play platform selector: one item per playable platform followed by
    /// <see cref="PlaySettingsItemText"/>, which opens the Play Settings dialog instead of selecting a platform.
    /// </summary>
    public static class EditorPlayPlatformMenu {
        /// <summary>Text of the item that opens the Play Settings dialog (ASCII only; the shared font charset is fixed).</summary>
        public const string PlaySettingsItemText = "Play Settings...";

        /// <summary>
        /// Builds the selector items.
        /// </summary>
        /// <param name="displayNames">Display names of the playable platforms, in order.</param>
        /// <returns>The platform names followed by the settings item, or no items when no platform is playable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="displayNames"/> is null.</exception>
        public static string[] BuildItems(IReadOnlyList<string> displayNames) {
            if (displayNames == null) {
                throw new ArgumentNullException(nameof(displayNames));
            } else if (displayNames.Count == 0) {
                return [];
            }

            string[] items = new string[displayNames.Count + 1];
            for (int index = 0; index < displayNames.Count; index++) {
                items[index] = displayNames[index];
            }
            items[displayNames.Count] = PlaySettingsItemText;
            return items;
        }

        /// <summary>
        /// Reports whether a selector index is the settings item.
        /// </summary>
        /// <param name="index">Selected index.</param>
        /// <param name="platformCount">Number of platform items before it.</param>
        /// <returns>True for the settings item.</returns>
        public static bool IsPlaySettingsItem(int index, int platformCount) {
            return platformCount > 0 && index == platformCount;
        }
    }
}
