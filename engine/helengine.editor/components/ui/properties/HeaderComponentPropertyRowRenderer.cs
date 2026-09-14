namespace helengine.editor {
    /// <summary>
    /// Renders label-only header rows used to introduce a group of property rows. A header owns no editing control,
    /// so it only stretches its label across the row and right-aligns the optional action button that some headers
    /// enable (for example the mesh modifier add button).
    /// </summary>
    sealed class HeaderComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the header rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public HeaderComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the header row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Header; }
        }

        /// <summary>
        /// Stretches the header label across the row and right-aligns the action button when one is enabled.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label; unused because the label spans the row.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            row.Label.Size = new int2(Math.Max(0, width), row.Label.Size.Y);
            if (row.ActionButtonHost != null && row.ActionButton != null && row.ActionButtonHost.Enabled) {
                float buttonY = (float)Math.Round((height - ComponentPropertiesView.PickButtonHeight) * 0.5);
                row.ActionButtonHost.Position = new float3(width - ComponentPropertiesView.PickButtonWidth, buttonY, 0.2f);
            }
        }
    }
}
