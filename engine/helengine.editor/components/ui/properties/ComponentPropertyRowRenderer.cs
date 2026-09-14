namespace helengine.editor {
    /// <summary>
    /// Owns every presentation concern of one component property row kind: creating the controls the row needs,
    /// refreshing them from the bound value, and placing them during layout. The inspector view keeps one renderer
    /// per <see cref="ComponentPropertyRowKind"/> so a new row kind is a new cohesive class instead of another set
    /// of parallel Build/Update/Layout branches spread across the view.
    /// </summary>
    abstract class ComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view whose rows, fonts, theme and edit callbacks it draws against.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        protected ComponentPropertyRowRenderer(ComponentPropertiesView view) {
            if (view == null) {
                throw new ArgumentNullException(nameof(view));
            }

            View = view;
        }

        /// <summary>
        /// Gets the inspector view that owns the rows drawn by this renderer.
        /// </summary>
        protected ComponentPropertiesView View { get; }

        /// <summary>
        /// Gets the row kind this renderer is registered for.
        /// </summary>
        public abstract ComponentPropertyRowKind Kind { get; }

        /// <summary>
        /// Creates the kind-specific controls for a freshly pooled row. Kinds whose rows carry no controls beyond the
        /// shared label and override chrome intentionally leave this empty.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created controls.</param>
        public virtual void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
        }

        /// <summary>
        /// Refreshes the row controls from the currently bound value. Kinds that display no value intentionally leave
        /// this empty.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public virtual void Update(ComponentPropertyRow row) {
        }

        /// <summary>
        /// Places the kind-specific controls inside the row bounds after the shared label has been positioned.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public virtual void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
        }
    }
}
