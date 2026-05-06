namespace helengine {
    /// <summary>
    /// Stores the live scene references associated with one baked demo menu panel.
    /// </summary>
    internal sealed class DemoMenuPanelRuntime {
        /// <summary>
        /// Initializes one baked demo menu panel runtime record.
        /// </summary>
        /// <param name="definition">Serialized panel metadata component.</param>
        /// <param name="rootEntity">Root entity that owns the panel subtree.</param>
        /// <param name="selectedDescriptionText">Text component updated as selection changes.</param>
        /// <param name="items">Enabled baked menu items contained by the panel.</param>
        public DemoMenuPanelRuntime(
            MenuPanelComponent definition,
            Entity rootEntity,
            TextComponent selectedDescriptionText,
            DemoMenuItemRuntime[] items) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RootEntity = rootEntity ?? throw new ArgumentNullException(nameof(rootEntity));
            SelectedDescriptionText = selectedDescriptionText ?? throw new ArgumentNullException(nameof(selectedDescriptionText));
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
        /// Gets the enabled baked menu items contained by the panel.
        /// </summary>
        public DemoMenuItemRuntime[] Items { get; }

        /// <summary>
        /// Gets or sets the currently selected enabled-item index.
        /// </summary>
        public int SelectedItemIndex { get; set; }
    }
}
