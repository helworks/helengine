namespace helengine {
    /// <summary>
    /// Stores the live scene references associated with one baked menu item.
    /// </summary>
    internal sealed class MenuItemRuntime {
        /// <summary>
        /// Initializes one baked menu item runtime record.
        /// </summary>
        /// <param name="definition">Serialized item metadata component.</param>
        /// <param name="index">Enabled-item index inside the owning panel.</param>
        /// <param name="entity">Owning row entity.</param>
        /// <param name="background">Rounded rectangle used for selection visuals.</param>
        public MenuItemRuntime(
            MenuItemComponent definition,
            int index,
            Entity entity,
            RoundedRectComponent background) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Index = index;
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Background = background ?? throw new ArgumentNullException(nameof(background));
        }

        /// <summary>
        /// Gets the serialized metadata component for the item.
        /// </summary>
        public MenuItemComponent Definition { get; }

        /// <summary>
        /// Gets the enabled-item index inside the owning panel.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the entity that owns the baked row visuals.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the rounded rectangle used for selection-state color updates.
        /// </summary>
        public RoundedRectComponent Background { get; }
    }
}
