namespace helengine.editor {
    /// <summary>
    /// Stores the shared title-bar chrome, collapse state, and body rows for one component section.
    /// </summary>
    public sealed class ComponentSectionView {
        /// <summary>
        /// Initializes one component section container with its persistent header chrome.
        /// </summary>
        /// <param name="root">Root entity for the section header.</param>
        /// <param name="background">Header background surface.</param>
        /// <param name="interactable">Header interactable region.</param>
        /// <param name="titleHost">Host entity for the component title.</param>
        /// <param name="titleText">Text rendered for the component title.</param>
        /// <param name="removeButtonHost">Host entity for the remove button.</param>
        /// <param name="removeButton">Remove button component.</param>
        public ComponentSectionView(
            EditorEntity root,
            SpriteComponent background,
            InteractableComponent interactable,
            EditorEntity titleHost,
            TextComponent titleText,
            EditorEntity removeButtonHost,
            ButtonComponent removeButton) {
            Root = root;
            Background = background;
            HeaderInteractable = interactable;
            TitleHost = titleHost;
            TitleText = titleText;
            RemoveButtonHost = removeButtonHost;
            RemoveButton = removeButton;
            Rows = new List<ComponentPropertyRow>();
        }

        /// <summary>
        /// Gets the root entity for the section header.
        /// </summary>
        public EditorEntity Root { get; }

        /// <summary>
        /// Gets the header background surface.
        /// </summary>
        public SpriteComponent Background { get; }

        /// <summary>
        /// Gets the header interactable used to toggle collapse.
        /// </summary>
        public InteractableComponent HeaderInteractable { get; }

        /// <summary>
        /// Gets the host entity used to position the section title.
        /// </summary>
        public EditorEntity TitleHost { get; }

        /// <summary>
        /// Gets the text component used to render the section title.
        /// </summary>
        public TextComponent TitleText { get; }

        /// <summary>
        /// Gets the host entity for the remove button.
        /// </summary>
        public EditorEntity RemoveButtonHost { get; }

        /// <summary>
        /// Gets the remove button component.
        /// </summary>
        public ButtonComponent RemoveButton { get; }

        /// <summary>
        /// Gets or sets the outline shown when the whole section exists only as a platform-specific existence override.
        /// </summary>
        public RoundedRectComponent HeaderOverrideOutline { get; set; }

        /// <summary>
        /// Gets or sets the host entity for the section-level revert button.
        /// </summary>
        public EditorEntity RevertButtonHost { get; set; }

        /// <summary>
        /// Gets or sets the section-level revert button component.
        /// </summary>
        public ButtonComponent RevertButton { get; set; }

        /// <summary>
        /// Gets the component represented by this section.
        /// </summary>
        public Component TargetComponent { get; set; }

        /// <summary>
        /// Gets or sets the common live component represented by this section when one exists.
        /// </summary>
        public Component CommonComponent { get; set; }

        /// <summary>
        /// Gets or sets the hidden save component attached to the owning entity.
        /// </summary>
        public EntitySaveComponent SaveComponent { get; set; }

        /// <summary>
        /// Gets or sets the platform id currently being edited by the section.
        /// </summary>
        public string EditingPlatformId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the section represents a platform-only detached component.
        /// </summary>
        public bool IsPlatformOnlyComponent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the section represents a common component removed on the active platform.
        /// </summary>
        public bool IsRemovedOnPlatform { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the section header is currently showing existence-override chrome.
        /// </summary>
        public bool IsExistenceOverrideActive { get; set; }

        /// <summary>
        /// Gets the rows currently owned by this section.
        /// </summary>
        public List<ComponentPropertyRow> Rows { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the section body is currently collapsed.
        /// </summary>
        public bool IsCollapsed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the section header is currently hovered.
        /// </summary>
        public bool IsHeaderHovered { get; set; }
    }
}
