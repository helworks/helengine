namespace helengine {
    /// <summary>
    /// Stores the live scene references associated with one baked menu panel.
    /// </summary>
    internal sealed class MenuPanelRuntime {
        /// <summary>
        /// Initializes one baked menu panel runtime record.
        /// </summary>
        /// <param name="definition">Serialized panel metadata component.</param>
        /// <param name="rootEntity">Root entity that owns the panel subtree.</param>
        /// <param name="selectedDescriptionText">Text component updated as selection changes.</param>
        /// <param name="itemsRootEntity">Scrolling item-root entity translated as the row offset changes.</param>
        /// <param name="itemsScrollComponent">Reusable row-based scroll component bound to the panel item list.</param>
        /// <param name="items">Enabled baked menu items contained by the panel.</param>
        public MenuPanelRuntime(
            MenuPanelComponent definition,
            Entity rootEntity,
            TextComponent selectedDescriptionText,
            Entity itemsRootEntity,
            ScrollComponent itemsScrollComponent,
            MenuItemRuntime[] items) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RootEntity = rootEntity ?? throw new ArgumentNullException(nameof(rootEntity));
            SelectedDescriptionText = selectedDescriptionText ?? throw new ArgumentNullException(nameof(selectedDescriptionText));
            ItemsRootEntity = itemsRootEntity ?? throw new ArgumentNullException(nameof(itemsRootEntity));
            ItemsScrollComponent = itemsScrollComponent ?? throw new ArgumentNullException(nameof(itemsScrollComponent));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            SelectedItemIndex = -1;
        }

        /// <summary>
        /// Gets the serialized panel metadata component.
        /// </summary>
        public MenuPanelComponent Definition { get; }

        /// <summary>
        /// Gets the root entity that owns the baked panel subtree.
        /// </summary>
        public Entity RootEntity { get; }

        /// <summary>
        /// Gets the description text component updated when selection changes.
        /// </summary>
        public TextComponent SelectedDescriptionText { get; }

        /// <summary>
        /// Gets the scrolling item-root entity translated when the scroll offset changes.
        /// </summary>
        public Entity ItemsRootEntity { get; }

        /// <summary>
        /// Gets the reusable row-based scroll component bound to the panel item list.
        /// </summary>
        public ScrollComponent ItemsScrollComponent { get; }

        /// <summary>
        /// Gets the enabled baked menu items contained by the panel.
        /// </summary>
        public MenuItemRuntime[] Items { get; }

        /// <summary>
        /// Gets or sets the currently selected enabled-item index.
        /// </summary>
        public int SelectedItemIndex { get; set; }
    }
}
