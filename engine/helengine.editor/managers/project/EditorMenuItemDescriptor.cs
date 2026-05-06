namespace helengine.editor {
    /// <summary>
    /// Describes one project-authored menu item contributed by an editor module.
    /// </summary>
    public sealed class EditorMenuItemDescriptor {
        /// <summary>
        /// Initializes one editor menu item descriptor.
        /// </summary>
        /// <param name="topLevelMenuId">Stable top-level menu identifier.</param>
        /// <param name="topLevelMenuLabel">Visible top-level menu label.</param>
        /// <param name="topLevelMenuOrder">Ordering value used for top-level menus.</param>
        /// <param name="menuItemId">Stable menu item identifier.</param>
        /// <param name="menuItemLabel">Visible menu item label.</param>
        /// <param name="menuItemOrder">Ordering value used inside one top-level menu.</param>
        /// <param name="commandId">Backing editor command identifier executed when the item is activated.</param>
        public EditorMenuItemDescriptor(
            string topLevelMenuId,
            string topLevelMenuLabel,
            int topLevelMenuOrder,
            string menuItemId,
            string menuItemLabel,
            int menuItemOrder,
            string commandId) {
            if (string.IsNullOrWhiteSpace(topLevelMenuId)) {
                throw new ArgumentException("Top-level menu id must be provided.", nameof(topLevelMenuId));
            }
            if (string.IsNullOrWhiteSpace(topLevelMenuLabel)) {
                throw new ArgumentException("Top-level menu label must be provided.", nameof(topLevelMenuLabel));
            }
            if (string.IsNullOrWhiteSpace(menuItemId)) {
                throw new ArgumentException("Menu item id must be provided.", nameof(menuItemId));
            }
            if (string.IsNullOrWhiteSpace(menuItemLabel)) {
                throw new ArgumentException("Menu item label must be provided.", nameof(menuItemLabel));
            }
            if (string.IsNullOrWhiteSpace(commandId)) {
                throw new ArgumentException("Command id must be provided.", nameof(commandId));
            }

            TopLevelMenuId = topLevelMenuId;
            TopLevelMenuLabel = topLevelMenuLabel;
            TopLevelMenuOrder = topLevelMenuOrder;
            MenuItemId = menuItemId;
            MenuItemLabel = menuItemLabel;
            MenuItemOrder = menuItemOrder;
            CommandId = commandId;
        }

        /// <summary>
        /// Gets the stable top-level menu identifier.
        /// </summary>
        public string TopLevelMenuId { get; }

        /// <summary>
        /// Gets the visible top-level menu label.
        /// </summary>
        public string TopLevelMenuLabel { get; }

        /// <summary>
        /// Gets the ordering value used for top-level menus.
        /// </summary>
        public int TopLevelMenuOrder { get; }

        /// <summary>
        /// Gets the stable menu item identifier.
        /// </summary>
        public string MenuItemId { get; }

        /// <summary>
        /// Gets the visible menu item label.
        /// </summary>
        public string MenuItemLabel { get; }

        /// <summary>
        /// Gets the ordering value used inside one top-level menu.
        /// </summary>
        public int MenuItemOrder { get; }

        /// <summary>
        /// Gets the backing editor command identifier executed when the item is activated.
        /// </summary>
        public string CommandId { get; }
    }
}
