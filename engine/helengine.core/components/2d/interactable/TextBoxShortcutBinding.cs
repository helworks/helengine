namespace helengine {
    /// <summary>
    /// Describes one textbox command shortcut and the modifier combination required to trigger it.
    /// </summary>
    public sealed class TextBoxShortcutBinding {
        /// <summary>
        /// Initializes one shortcut binding with the supplied key and modifier requirements.
        /// </summary>
        /// <param name="key">Primary key that triggers the shortcut.</param>
        /// <param name="requiresControl">True when either control key must be pressed.</param>
        /// <param name="requiresShift">True when either shift key must be pressed.</param>
        /// <param name="requiresAlt">True when either alt key must be pressed.</param>
        public TextBoxShortcutBinding(Keys key, bool requiresControl, bool requiresShift, bool requiresAlt) {
            Key = key;
            RequiresControl = requiresControl;
            RequiresShift = requiresShift;
            RequiresAlt = requiresAlt;
        }

        /// <summary>
        /// Gets the primary key that triggers the shortcut.
        /// </summary>
        public Keys Key { get; }

        /// <summary>
        /// Gets whether either control key must be pressed.
        /// </summary>
        public bool RequiresControl { get; }

        /// <summary>
        /// Gets whether either shift key must be pressed.
        /// </summary>
        public bool RequiresShift { get; }

        /// <summary>
        /// Gets whether either alt key must be pressed.
        /// </summary>
        public bool RequiresAlt { get; }

        /// <summary>
        /// Returns whether the supplied key and modifier state satisfy this shortcut binding.
        /// </summary>
        /// <param name="key">Key pressed on the current frame.</param>
        /// <param name="isControlPressed">True when either control key is pressed.</param>
        /// <param name="isShiftPressed">True when either shift key is pressed.</param>
        /// <param name="isAltPressed">True when either alt key is pressed.</param>
        /// <returns>True when the supplied key and modifiers match this binding exactly.</returns>
        public bool Matches(Keys key, bool isControlPressed, bool isShiftPressed, bool isAltPressed) {
            if (Key != key) {
                return false;
            }
            if (RequiresControl != isControlPressed) {
                return false;
            }
            if (RequiresShift != isShiftPressed) {
                return false;
            }

            return RequiresAlt == isAltPressed;
        }
    }
}
