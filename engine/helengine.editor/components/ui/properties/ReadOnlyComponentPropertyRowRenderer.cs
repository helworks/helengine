namespace helengine.editor {
    /// <summary>
    /// Renders rows for property types the inspector cannot edit, showing the current value as plain text so the
    /// value is still visible even though no editing control exists for it.
    /// </summary>
    sealed class ReadOnlyComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the read-only rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public ReadOnlyComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the read-only row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.ReadOnly; }
        }

        /// <summary>
        /// Creates the value text shown in place of an editing control.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created text.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
            valueHost.LayerMask = View.RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = View.Font;
            valueText.Text = string.Empty;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = View.TextOrder;
            valueHost.AddComponent(valueText);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
        }

        /// <summary>
        /// Writes the string form of the bound value into the row text.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            object rawValue = View.GetRowValue(row);
            if (rawValue == null) {
                row.ValueText.Text = string.Empty;
            } else {
                row.ValueText.Text = rawValue.ToString();
            }
        }

        /// <summary>
        /// Stretches the value text across the width remaining after the row label.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ValueHost == null || row.ValueText == null) {
                return;
            }

            int valueWidth = Math.Max(0, width - labelWidth - ComponentPropertiesView.FieldSpacing);
            var valueMetrics = View.Font.MeasureTight(row.ValueText.Text ?? string.Empty);
            float valueY = View.GetTextTopOffset(height, valueMetrics);
            row.ValueHost.Position = new float3(labelWidth + ComponentPropertiesView.FieldSpacing, valueY, 0.2f);
            row.ValueText.Size = new int2(valueWidth, (int)Math.Ceiling(valueMetrics.Height));
        }
    }
}
