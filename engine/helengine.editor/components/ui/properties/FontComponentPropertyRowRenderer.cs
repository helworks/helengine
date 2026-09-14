namespace helengine.editor {
    /// <summary>
    /// Renders font reference rows, preferring the authored label captured when the font was picked and falling back
    /// to the name reported by the loaded font asset.
    /// </summary>
    sealed class FontComponentPropertyRowRenderer : AssetComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the font rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public FontComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the font row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Font; }
        }

        /// <summary>
        /// Shows the label of the assigned font, or the empty-asset placeholder when nothing is assigned.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            object rawValue = View.GetPropertyValue(row);
            if (rawValue is FontAsset font) {
                if (View.FontLabels.TryGetValue(font, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                if (font.FontInfo == null || string.IsNullOrWhiteSpace(font.FontInfo.Name)) {
                    row.ValueText.Text = ComponentPropertiesView.EmptyAssetLabel;
                } else {
                    row.ValueText.Text = font.FontInfo.Name;
                }
            } else {
                row.ValueText.Text = ComponentPropertiesView.EmptyAssetLabel;
            }
        }

        /// <summary>
        /// Opens the asset browser filtered to font assets.
        /// </summary>
        /// <param name="row">Row whose font reference should be replaced.</param>
        protected override void RequestPick(ComponentPropertyRow row) {
            View.RequestFontPick(row);
        }
    }
}
