namespace helengine.editor {
    /// <summary>
    /// Represents a single context menu entry with a label and action.
    /// </summary>
    public sealed class ContextMenuItem {
        /// <summary>
        /// Initializes a new context menu item with the provided label and action.
        /// </summary>
        /// <param name="label">Text displayed for the menu item.</param>
        /// <param name="action">Callback invoked when the item is activated.</param>
        public ContextMenuItem(string label, Action action) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Menu item label must be provided.", nameof(label));
            }
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            Label = label;
            Action = action;
            HoverAction = null;
            CloseOnActivate = true;
        }

        /// <summary>
        /// Initializes a new context menu item with activation and hover callbacks.
        /// </summary>
        /// <param name="label">Text displayed for the menu item.</param>
        /// <param name="action">Callback invoked when the item is activated.</param>
        /// <param name="hoverAction">Callback invoked when the item is hovered.</param>
        /// <param name="closeOnActivate">True to close the menu on activation.</param>
        public ContextMenuItem(string label, Action action, Action hoverAction, bool closeOnActivate) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Menu item label must be provided.", nameof(label));
            }
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            Label = label;
            Action = action;
            HoverAction = hoverAction;
            CloseOnActivate = closeOnActivate;
        }

        /// <summary>
        /// Gets the label displayed for the menu item.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the action invoked when the item is selected.
        /// </summary>
        public Action Action { get; }

        /// <summary>
        /// Gets the action invoked when the item is hovered.
        /// </summary>
        public Action HoverAction { get; }

        /// <summary>
        /// Gets a value indicating whether the menu should close on activation.
        /// </summary>
        public bool CloseOnActivate { get; }
    }
}
