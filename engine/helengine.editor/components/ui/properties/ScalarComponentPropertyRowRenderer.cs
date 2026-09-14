namespace helengine.editor {
    /// <summary>
    /// Renders scalar property rows as a single text field, optionally sharing the row with the scene-map action
    /// button that some provider-backed scalar rows enable next to the field.
    /// </summary>
    sealed class ScalarComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Minimum width in pixels allowed for the scalar field.
        /// </summary>
        const int MinimumFieldWidth = 48;

        /// <summary>
        /// Initial width in pixels used when the field control is created.
        /// </summary>
        const int InitialFieldWidth = 120;

        /// <summary>
        /// Binds the renderer to the inspector view that owns the scalar rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public ScalarComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the scalar row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Scalar; }
        }

        /// <summary>
        /// Creates the scalar text field and registers it for submit handling.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created field.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            var fieldHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
            fieldHost.LayerMask = View.RootEntity.LayerMask;
            fieldHost.Position = float3.Zero;
            rowEntity.AddChild(fieldHost);

            var field = new TextBoxComponent(new int2(InitialFieldWidth, ComponentPropertiesView.FieldHeight), View.Font, string.Empty);
            field.Submitted += View.HandleScalarSubmitted;
            fieldHost.AddComponent(field);

            row.ScalarField = field;
            row.ScalarCache = string.Empty;
            View.ScalarFieldRows[field] = row;
        }

        /// <summary>
        /// Writes the formatted bound value into the scalar field.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            object rawValue = View.GetRowValue(row);
            string text = View.FormatScalarValue(rawValue);
            View.UpdateScalarField(row, text);
        }

        /// <summary>
        /// Stretches the scalar field across the remaining width, reserving room for the action button when the row
        /// currently exposes one.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ScalarField == null) {
                return;
            }

            bool hasActionButton = row.ActionButtonHost != null && row.ActionButton != null && row.ActionButtonHost.Enabled;
            int actionButtonWidth = 0;
            if (hasActionButton) {
                actionButtonWidth = ComponentPropertiesView.PickButtonWidth + ComponentPropertiesView.FieldSpacing;
            }

            int fieldWidth = Math.Max(MinimumFieldWidth, width - labelWidth - ComponentPropertiesView.FieldSpacing - actionButtonWidth);
            float fieldY = (float)Math.Round((height - ComponentPropertiesView.FieldHeight) * 0.5);
            row.ScalarField.Parent.Position = new float3(labelWidth + ComponentPropertiesView.FieldSpacing, fieldY, 0.2f);
            row.ScalarField.Size = new int2(fieldWidth, ComponentPropertiesView.FieldHeight);
            if (hasActionButton) {
                float buttonY = (float)Math.Round((height - ComponentPropertiesView.PickButtonHeight) * 0.5);
                row.ActionButtonHost.Position = new float3(labelWidth + ComponentPropertiesView.FieldSpacing + fieldWidth + ComponentPropertiesView.FieldSpacing, buttonY, 0.2f);
            }
        }
    }
}
