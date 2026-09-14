namespace helengine.editor {
    /// <summary>
    /// Shared presentation for the asset-reference row kinds (material, font and model). Every asset row shows the
    /// current asset as a label followed by a pick button that opens the asset browser, so only the label text and
    /// the picker request differ between kinds.
    /// </summary>
    abstract class AssetComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Binds the renderer to the inspector view that owns the asset rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        protected AssetComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Creates the asset label and the pick button shared by every asset row kind.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created controls.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
            valueHost.LayerMask = View.RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = View.Font;
            valueText.Text = ComponentPropertiesView.EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = View.TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
            buttonHost.LayerMask = View.RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent(
                "Pick",
                new int2(ComponentPropertiesView.PickButtonWidth, ComponentPropertiesView.PickButtonHeight),
                View.Font,
                () => RequestPick(row),
                0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Places the asset label between the row label and the right-aligned pick button.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ValueHost == null || row.ValueText == null || row.ActionButtonHost == null) {
                return;
            }

            int buttonWidth = ComponentPropertiesView.PickButtonWidth;
            int valueWidth = Math.Max(0, width - labelWidth - ComponentPropertiesView.FieldSpacing - buttonWidth);
            var valueMetrics = View.Font.MeasureTight(row.ValueText.Text ?? string.Empty);
            float valueY = View.GetTextTopOffset(height, valueMetrics);
            row.ValueHost.Position = new float3(labelWidth + ComponentPropertiesView.FieldSpacing, valueY, 0.2f);
            row.ValueText.Size = new int2(valueWidth, (int)Math.Ceiling(valueMetrics.Height));

            float buttonY = (float)Math.Round((height - ComponentPropertiesView.PickButtonHeight) * 0.5);
            row.ActionButtonHost.Position = new float3(width - buttonWidth, buttonY, 0.2f);
        }

        /// <summary>
        /// Opens the asset browser for the concrete asset kind this renderer edits.
        /// </summary>
        /// <param name="row">Row whose asset reference should be replaced.</param>
        protected abstract void RequestPick(ComponentPropertyRow row);
    }
}
