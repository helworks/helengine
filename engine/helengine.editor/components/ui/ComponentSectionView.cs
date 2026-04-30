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
        /// Gets the component represented by this section.
        /// </summary>
        public Component TargetComponent { get; set; }

        /// <summary>
        /// Gets the rows currently owned by this section.
        /// </summary>
        public List<ComponentPropertyRow> Rows { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the section body is currently collapsed.
        /// </summary>
        public bool IsCollapsed { get; set; }
    }
}
