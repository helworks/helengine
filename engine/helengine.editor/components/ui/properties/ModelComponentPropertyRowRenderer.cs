namespace helengine.editor {
    /// <summary>
    /// Renders model reference rows, preferring the authored label captured when the model was picked and falling
    /// back to the runtime model id, or to a generic assigned marker for models that carry no id.
    /// </summary>
    sealed class ModelComponentPropertyRowRenderer : AssetComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the model rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public ModelComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the model row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Model; }
        }

        /// <summary>
        /// Shows the label of the assigned model, or the empty-asset placeholder when nothing is assigned.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            object rawValue = View.GetRowValue(row);
            if (rawValue is RuntimeModel model) {
                if (View.ModelLabels.TryGetValue(model, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                if (string.IsNullOrWhiteSpace(model.Id)) {
                    row.ValueText.Text = ComponentPropertiesView.AssignedAssetLabel;
                } else {
                    row.ValueText.Text = model.Id;
                }
                return;
            }

            row.ValueText.Text = ComponentPropertiesView.EmptyAssetLabel;
        }

        /// <summary>
        /// Opens the asset browser filtered to model assets.
        /// </summary>
        /// <param name="row">Row whose model reference should be replaced.</param>
        protected override void RequestPick(ComponentPropertyRow row) {
            View.RequestModelPick(row);
        }
    }
}
