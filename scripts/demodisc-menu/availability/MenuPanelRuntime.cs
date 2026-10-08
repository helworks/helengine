namespace DemoDisc.menu {
    /// <summary>
    /// Stores borrowed scene references and owns the live records associated with one baked menu panel.
    /// </summary>
    internal sealed class MenuPanelRuntime : IDisposable {
        /// <summary>
        /// Owns the row runtime records and the array containing them, without owning their scene entities.
        /// </summary>
        [NativeOwnedMember]
        MenuItemRuntime[] ItemsValue;

        /// <summary>
        /// Initializes the panel's borrowed hierarchy references and takes ownership of its row records.
        /// </summary>
        /// <param name="definition">Serialized panel metadata owned by the scene entity.</param>
        /// <param name="rootEntity">Borrowed root entity that activates the panel visuals.</param>
        /// <param name="itemsRootEntity">Borrowed item root translated by scrolling.</param>
        /// <param name="itemsScrollComponent">Borrowed scroll component owned by the item root.</param>
        /// <param name="items">Row records and container transferred to this panel runtime.</param>
        public MenuPanelRuntime(MenuPanelComponent definition, Entity rootEntity, Entity itemsRootEntity,
            ScrollComponent itemsScrollComponent, [NativeTakesOwnership] MenuItemRuntime[] items) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RootEntity = rootEntity ?? throw new ArgumentNullException(nameof(rootEntity));
            ItemsRootEntity = itemsRootEntity ?? throw new ArgumentNullException(nameof(itemsRootEntity));
            ItemsScrollComponent = itemsScrollComponent ?? throw new ArgumentNullException(nameof(itemsScrollComponent));
            ItemsValue = items ?? throw new ArgumentNullException(nameof(items));
            SelectedItemIndex = -1;
        }

        /// <summary>Gets the borrowed serialized panel metadata.</summary>
        public MenuPanelComponent Definition { get; }

        /// <summary>Gets the borrowed entity that controls panel visibility.</summary>
        public Entity RootEntity { get; }

        /// <summary>Gets the borrowed entity translated by the scroll offset.</summary>
        public Entity ItemsRootEntity { get; }

        /// <summary>Gets the borrowed scroll component supplying bounds and offset.</summary>
        public ScrollComponent ItemsScrollComponent { get; }

        /// <summary>Gets the owned records for the visible, available rows.</summary>
        public MenuItemRuntime[] Items { get { return ItemsValue; } }

        /// <summary>Gets or sets the selected row index in the filtered visible row order.</summary>
        public int SelectedItemIndex { get; set; }

        /// <summary>
        /// Disposes each row record and its container without touching entity-owned hierarchy objects.
        /// </summary>
        public void Dispose() {
            NativeOwnership.DisposeItemsAndRelease(ref ItemsValue);
        }
    }
}
