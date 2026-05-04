namespace helengine {
    /// <summary>
    /// Stores the runtime objects created for one enabled menu item inside a materialized panel.
    /// </summary>
    internal sealed class MenuHostItemRuntime {
        /// <summary>
        /// Initializes one runtime menu item record.
        /// </summary>
        /// <param name="panel">Owning runtime panel.</param>
        /// <param name="definition">Source definition used to build the item.</param>
        /// <param name="index">Zero-based enabled-item index inside the panel.</param>
        /// <param name="entity">Entity that owns the rendered button.</param>
        /// <param name="button">Interactive button used to render and activate the item.</param>
        public MenuHostItemRuntime(
            MenuHostPanelRuntime panel,
            MenuItemDefinition definition,
            int index,
            Entity entity,
            ButtonComponent button) {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (index < 0) {
                throw new ArgumentOutOfRangeException(nameof(index), "Menu item index must be non-negative.");
            }
            Index = index;
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Button = button ?? throw new ArgumentNullException(nameof(button));
        }

        /// <summary>
        /// Gets the runtime panel that owns the item.
        /// </summary>
        public MenuHostPanelRuntime Panel { get; }

        /// <summary>
        /// Gets the source definition used to build the item.
        /// </summary>
        public MenuItemDefinition Definition { get; }

        /// <summary>
        /// Gets the zero-based enabled-item index inside the panel.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the entity that owns the rendered button.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the interactive button used to render and activate the item.
        /// </summary>
        public ButtonComponent Button { get; }
    }
}
