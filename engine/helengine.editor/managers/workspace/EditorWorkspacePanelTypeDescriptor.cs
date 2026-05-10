namespace helengine.editor {
    /// <summary>
    /// Describes one panel type that can be created through the workspace UI.
    /// </summary>
    public sealed class EditorWorkspacePanelTypeDescriptor {
        /// <summary>
        /// Stable type identifier used in persisted workspace layouts.
        /// </summary>
        public string PanelTypeId { get; }

        /// <summary>
        /// User-visible panel title used for new instances.
        /// </summary>
        public string DisplayTitle { get; }

        /// <summary>
        /// Default undocked panel size.
        /// </summary>
        public int2 DefaultSize { get; }

        /// <summary>
        /// Factory that creates one new controller instance for the owning editor session.
        /// </summary>
        public Func<EditorSession, IEditorWorkspacePanelController> CreateController { get; }

        /// <summary>
        /// Initializes one panel type descriptor.
        /// </summary>
        /// <param name="panelTypeId">Stable type identifier used in persisted workspace layouts.</param>
        /// <param name="displayTitle">User-visible title shown on created panel instances.</param>
        /// <param name="defaultSize">Default undocked panel size.</param>
        /// <param name="createController">Factory that creates one new panel controller.</param>
        public EditorWorkspacePanelTypeDescriptor(string panelTypeId, string displayTitle, int2 defaultSize, Func<EditorSession, IEditorWorkspacePanelController> createController) {
            if (string.IsNullOrWhiteSpace(panelTypeId)) {
                throw new ArgumentException("Panel type identifier must be provided.", nameof(panelTypeId));
            }
            if (string.IsNullOrWhiteSpace(displayTitle)) {
                throw new ArgumentException("Display title must be provided.", nameof(displayTitle));
            }
            if (createController == null) {
                throw new ArgumentNullException(nameof(createController));
            }

            PanelTypeId = panelTypeId;
            DisplayTitle = displayTitle;
            DefaultSize = defaultSize;
            CreateController = createController;
        }
    }
}
