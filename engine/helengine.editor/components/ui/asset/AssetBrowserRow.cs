namespace helengine.editor {
    /// <summary>
    /// Bundles row entities and state for the asset browser list.
    /// </summary>
    sealed class AssetBrowserRow {
        /// <summary>
        /// Initializes a new row container with its visual components.
        /// </summary>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="background">Background sprite.</param>
        /// <param name="iconBackground">Sprite used for the entry icon background.</param>
        /// <param name="iconText">Text component used for the icon label.</param>
        /// <param name="label">Text component used for the entry label.</param>
        /// <param name="interactable">Interactable hit region.</param>
        public AssetBrowserRow(
            EditorEntity entity,
            SpriteComponent background,
            SpriteComponent iconBackground,
            TextComponent iconText,
            TextComponent label,
            InteractableComponent interactable) {

            Entity = entity;
            Background = background;
            IconBackground = iconBackground;
            IconText = iconText;
            Label = label;
            Interactable = interactable;
            BaseColor = ThemeManager.Colors.SurfacePrimary;
        }

        /// <summary>
        /// Gets the root entity for this row.
        /// </summary>
        public EditorEntity Entity { get; }

        /// <summary>
        /// Gets the background sprite for the row.
        /// </summary>
        public SpriteComponent Background { get; }

        /// <summary>
        /// Gets the sprite used to render the icon background.
        /// </summary>
        public SpriteComponent IconBackground { get; }

        /// <summary>
        /// Gets the text component used for the icon label.
        /// </summary>
        public TextComponent IconText { get; }

        /// <summary>
        /// Gets the text component used for the entry label.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets the interactable region for input handling.
        /// </summary>
        public InteractableComponent Interactable { get; }

        /// <summary>
        /// Gets or sets the entry represented by this row.
        /// </summary>
        public AssetBrowserEntry? Entry { get; set; }

        /// <summary>
        /// Gets or sets the base color when the row is idle.
        /// </summary>
        public byte4 BaseColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is hovered.
        /// </summary>
        public bool IsHovering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is pressed.
        /// </summary>
        public bool IsPressed { get; set; }
    }
}
