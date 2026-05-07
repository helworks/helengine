namespace helengine {
    /// <summary>
    /// Stores the shortcut bindings used by textbox editing commands.
    /// </summary>
    public sealed class TextBoxShortcutRegistry {
        /// <summary>
        /// Initializes the registry with the default desktop textbox shortcuts.
        /// </summary>
        public TextBoxShortcutRegistry() {
            SelectAllShortcut = new TextBoxShortcutBinding(Keys.A, true, false, false);
            CopyShortcut = new TextBoxShortcutBinding(Keys.C, true, false, false);
            PasteShortcut = new TextBoxShortcutBinding(Keys.V, true, false, false);
        }

        /// <summary>
        /// Gets or sets the shortcut that selects the entire textbox content.
        /// </summary>
        public TextBoxShortcutBinding SelectAllShortcut { get; set; }

        /// <summary>
        /// Gets or sets the shortcut that copies the active selection to the clipboard.
        /// </summary>
        public TextBoxShortcutBinding CopyShortcut { get; set; }

        /// <summary>
        /// Gets or sets the shortcut that pastes clipboard text into the textbox.
        /// </summary>
        public TextBoxShortcutBinding PasteShortcut { get; set; }
    }
}
