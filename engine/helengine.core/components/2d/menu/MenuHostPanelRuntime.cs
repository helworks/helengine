namespace helengine {
    /// <summary>
    /// Stores the runtime objects created for one materialized menu panel.
    /// </summary>
    internal sealed class MenuHostPanelRuntime {
        /// <summary>
        /// Initializes one runtime panel record.
        /// </summary>
        /// <param name="definition">Source definition used to build the panel.</param>
        /// <param name="rootEntity">Root entity that owns the panel item entities.</param>
        /// <param name="items">Enabled runtime items materialized for the panel.</param>
        public MenuHostPanelRuntime(MenuPanelDefinition definition, Entity rootEntity, MenuHostItemRuntime[] items) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RootEntity = rootEntity ?? throw new ArgumentNullException(nameof(rootEntity));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            SelectedItemIndex = -1;
        }

        /// <summary>
        /// Gets the source definition used to build the panel.
        /// </summary>
        public MenuPanelDefinition Definition { get; }

        /// <summary>
        /// Gets the root entity that owns the panel item entities.
        /// </summary>
        public Entity RootEntity { get; }

        /// <summary>
        /// Gets the enabled runtime items materialized for the panel.
        /// </summary>
        public MenuHostItemRuntime[] Items { get; }

        /// <summary>
        /// Gets or sets the first enabled item index currently visible in the panel.
        /// </summary>
        public int ScrollOffset { get; set; }

        /// <summary>
        /// Gets or sets the currently selected enabled item index.
        /// </summary>
        public int SelectedItemIndex { get; set; }
    }
}
