namespace helengine.editor {
    /// <summary>
    /// Tracks one contributed top-level project menu rendered by the editor title bar.
    /// </summary>
    sealed class EditorTitleBarProjectMenuState {
        /// <summary>
        /// Initializes one contributed title-bar project menu state.
        /// </summary>
        /// <param name="topLevelMenuId">Stable top-level menu identifier.</param>
        /// <param name="topLevelMenuLabel">Visible top-level menu label.</param>
        /// <param name="topLevelMenuOrder">Ordering value used across contributed top-level menus.</param>
        /// <param name="buttonEntity">Entity hosting the top-level title-bar button.</param>
        /// <param name="buttonWidth">Current computed width of the top-level button.</param>
        /// <param name="menu">Context menu shown when the top-level button is activated.</param>
        /// <param name="menuItems">Context menu items shown inside the contributed menu.</param>
        public EditorTitleBarProjectMenuState(
            string topLevelMenuId,
            string topLevelMenuLabel,
            int topLevelMenuOrder,
            EditorEntity buttonEntity,
            int buttonWidth,
            ContextMenu menu,
            IReadOnlyList<ContextMenuItem> menuItems) {
            TopLevelMenuId = string.IsNullOrWhiteSpace(topLevelMenuId)
                ? throw new ArgumentException("Top-level menu id must be provided.", nameof(topLevelMenuId))
                : topLevelMenuId;
            TopLevelMenuLabel = string.IsNullOrWhiteSpace(topLevelMenuLabel)
                ? throw new ArgumentException("Top-level menu label must be provided.", nameof(topLevelMenuLabel))
                : topLevelMenuLabel;
            ButtonEntity = buttonEntity ?? throw new ArgumentNullException(nameof(buttonEntity));
            Menu = menu ?? throw new ArgumentNullException(nameof(menu));
            MenuItems = menuItems ?? throw new ArgumentNullException(nameof(menuItems));
            TopLevelMenuOrder = topLevelMenuOrder;
            ButtonWidth = buttonWidth;
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
        /// Gets the ordering value used across contributed top-level menus.
        /// </summary>
        public int TopLevelMenuOrder { get; }

        /// <summary>
        /// Gets the entity hosting the top-level title-bar button.
        /// </summary>
        public EditorEntity ButtonEntity { get; }

        /// <summary>
        /// Gets or sets the current computed top-level button width.
        /// </summary>
        public int ButtonWidth { get; set; }

        /// <summary>
        /// Gets the context menu shown when the top-level button is activated.
        /// </summary>
        public ContextMenu Menu { get; }

        /// <summary>
        /// Gets the items displayed inside the contributed context menu.
        /// </summary>
        public IReadOnlyList<ContextMenuItem> MenuItems { get; }
    }
}
