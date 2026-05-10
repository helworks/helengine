namespace helengine.editor {
    /// <summary>
    /// Represents one live panel instance tracked by the editor workspace system.
    /// </summary>
    public sealed class EditorWorkspacePanelInstance {
        /// <summary>
        /// Stable instance identifier used by workspace persistence.
        /// </summary>
        public string InstanceId { get; }

        /// <summary>
        /// Stable type identifier used to recreate the panel instance.
        /// </summary>
        public string PanelTypeId { get; }

        /// <summary>
        /// User-visible title stored for the panel instance.
        /// </summary>
        public string DisplayTitle { get; }

        /// <summary>
        /// Panel controller that owns the dockable entity and per-panel state.
        /// </summary>
        public IEditorWorkspacePanelController Controller { get; }

        /// <summary>
        /// Delegate subscribed to the dockable close request event for this instance.
        /// </summary>
        public Action CloseRequestedHandler { get; }

        /// <summary>
        /// Gets the dockable entity shown in the workspace.
        /// </summary>
        public DockableEntity Dockable => Controller.Dockable;

        /// <summary>
        /// Initializes one live panel instance record.
        /// </summary>
        /// <param name="instanceId">Stable instance identifier used by workspace persistence.</param>
        /// <param name="panelTypeId">Stable type identifier used to recreate the panel instance.</param>
        /// <param name="displayTitle">User-visible title stored for the panel instance.</param>
        /// <param name="controller">Panel controller that owns the dockable entity and state.</param>
        /// <param name="closeRequestedHandler">Delegate subscribed to the dockable close event.</param>
        public EditorWorkspacePanelInstance(string instanceId, string panelTypeId, string displayTitle, IEditorWorkspacePanelController controller, Action closeRequestedHandler) {
            if (string.IsNullOrWhiteSpace(instanceId)) {
                throw new ArgumentException("Instance identifier must be provided.", nameof(instanceId));
            }
            if (string.IsNullOrWhiteSpace(panelTypeId)) {
                throw new ArgumentException("Panel type identifier must be provided.", nameof(panelTypeId));
            }
            if (string.IsNullOrWhiteSpace(displayTitle)) {
                throw new ArgumentException("Display title must be provided.", nameof(displayTitle));
            }
            if (controller == null) {
                throw new ArgumentNullException(nameof(controller));
            }
            if (closeRequestedHandler == null) {
                throw new ArgumentNullException(nameof(closeRequestedHandler));
            }

            InstanceId = instanceId;
            PanelTypeId = panelTypeId;
            DisplayTitle = displayTitle;
            Controller = controller;
            CloseRequestedHandler = closeRequestedHandler;
        }
    }
}
