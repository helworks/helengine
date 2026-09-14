namespace helengine.editor {
    /// <summary>
    /// Renders boolean property rows as a square checkbox placed after the row label. The same renderer also serves
    /// component existence-toggle rows, which reuse the checkbox control but source their state from the platform
    /// editing service rather than from a reflected property.
    /// </summary>
    sealed class BooleanComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Minimum edge length in pixels for the checkbox control.
        /// </summary>
        const int MinimumCheckBoxSize = 16;

        /// <summary>
        /// Binds the renderer to the inspector view that owns the boolean rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public BooleanComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the boolean row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Boolean; }
        }

        /// <summary>
        /// Creates the checkbox control and subscribes it to the inspector check handler.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created checkbox.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            var checkBoxHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
            checkBoxHost.LayerMask = View.RootEntity.LayerMask;
            checkBoxHost.Position = float3.Zero;
            rowEntity.AddChild(checkBoxHost);

            var checkBox = new CheckBoxComponent(new int2(ComponentPropertiesView.FieldHeight, ComponentPropertiesView.FieldHeight), View.Font);
            checkBox.SetRenderOrders(RenderOrder2D.PanelSurface, View.TextOrder);
            checkBox.CheckedChanged += View.HandleBooleanCheckedChanged;
            checkBoxHost.AddComponent(checkBox);

            row.CheckBoxHost = checkBoxHost;
            row.CheckBoxField = checkBox;
        }

        /// <summary>
        /// Applies the bound boolean value to the checkbox, treating a missing or non-boolean value as unchecked.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            bool isChecked = false;
            object rawValue = View.GetRowValue(row);
            if (rawValue is bool boolValue) {
                isChecked = boolValue;
            }

            View.UpdateBooleanField(row, isChecked);
        }

        /// <summary>
        /// Centres the checkbox vertically just after the row label.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.CheckBoxHost == null || row.CheckBoxField == null) {
                return;
            }

            int checkBoxSize = Math.Max(MinimumCheckBoxSize, ComponentPropertiesView.FieldHeight);
            float checkBoxY = (float)Math.Round((height - checkBoxSize) * 0.5);
            row.CheckBoxHost.Position = new float3(labelWidth + ComponentPropertiesView.FieldSpacing, checkBoxY, 0.2f);
            row.CheckBoxField.Size = new int2(checkBoxSize, checkBoxSize);
        }
    }
}
