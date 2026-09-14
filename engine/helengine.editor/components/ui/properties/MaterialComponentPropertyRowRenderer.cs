namespace helengine.editor {
    /// <summary>
    /// Renders material reference rows, preferring the authored label captured when the material was picked and
    /// falling back to the runtime material id when no label is known.
    /// </summary>
    sealed class MaterialComponentPropertyRowRenderer : AssetComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the material rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public MaterialComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the material row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Material; }
        }

        /// <summary>
        /// Shows the label of the assigned material, or the empty-asset placeholder when nothing is assigned.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            object rawValue = View.GetPropertyValue(row);
            if (rawValue is RuntimeMaterial material) {
                if (View.MaterialLabels.TryGetValue(material, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                if (string.IsNullOrWhiteSpace(material.Id)) {
                    row.ValueText.Text = ComponentPropertiesView.EmptyAssetLabel;
                } else {
                    row.ValueText.Text = material.Id;
                }
            } else {
                row.ValueText.Text = ComponentPropertiesView.EmptyAssetLabel;
            }
        }

        /// <summary>
        /// Opens the asset browser filtered to material assets.
        /// </summary>
        /// <param name="row">Row whose material reference should be replaced.</param>
        protected override void RequestPick(ComponentPropertyRow row) {
            View.RequestMaterialPick(row);
        }
    }
}
