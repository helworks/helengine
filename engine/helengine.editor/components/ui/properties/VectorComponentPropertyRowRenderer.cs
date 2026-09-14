namespace helengine.editor {
    /// <summary>
    /// Renders Vector3 property rows as three side-by-side numeric text fields labelled X, Y and Z, keeping the
    /// field text, the change-detection cache and the field-to-row lookup used by submit handling in one place.
    /// </summary>
    sealed class VectorComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Minimum width in pixels allowed for one vector component field.
        /// </summary>
        const int MinimumFieldWidth = 48;

        /// <summary>
        /// Initial width in pixels used when the field control is created.
        /// </summary>
        const int InitialFieldWidth = 60;

        /// <summary>
        /// Placeholder text shown in each component field before a value is bound.
        /// </summary>
        static readonly string[] Placeholders = new string[] { "X", "Y", "Z" };

        /// <summary>
        /// Binds the renderer to the inspector view that owns the Vector3 rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public VectorComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the Vector3 row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Vector3; }
        }

        /// <summary>
        /// Creates the three component text fields and registers them for submit handling.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created fields.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            row.VectorFieldHosts = new EditorEntity[3];
            row.VectorFields = new TextBoxComponent[3];
            row.VectorCache = new string[3];

            for (int i = 0; i < row.VectorFieldHosts.Length; i++) {
                var fieldHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
                fieldHost.LayerMask = View.RootEntity.LayerMask;
                fieldHost.Position = float3.Zero;
                rowEntity.AddChild(fieldHost);

                var field = new TextBoxComponent(new int2(InitialFieldWidth, ComponentPropertiesView.FieldHeight), View.Font, Placeholders[i]);
                field.Submitted += View.HandleVectorSubmitted;
                fieldHost.AddComponent(field);

                row.VectorFieldHosts[i] = fieldHost;
                row.VectorFields[i] = field;
                View.VectorFieldRows[field] = row;
            }
        }

        /// <summary>
        /// Writes the bound Vector3 value into the component fields, falling back to zeros when no value is readable.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            if (!View.TryGetVectorValue(row, out float3 value)) {
                View.SetVectorFields(row, 0.0, 0.0, 0.0);
                return;
            }

            View.SetVectorFields(row, value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Spreads the three component fields across the space left over after the row label.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.VectorFieldHosts == null || row.VectorFields == null) {
                return;
            }

            int available = Math.Max(0, width - labelWidth - (ComponentPropertiesView.FieldSpacing * 2));
            int fieldWidth = Math.Max(MinimumFieldWidth, available / 3);
            float fieldY = (float)Math.Round((height - ComponentPropertiesView.FieldHeight) * 0.5);

            int fieldX = labelWidth + ComponentPropertiesView.FieldSpacing;
            for (int i = 0; i < row.VectorFieldHosts.Length; i++) {
                row.VectorFieldHosts[i].Position = new float3(fieldX, fieldY, 0.2f);
                row.VectorFields[i].Size = new int2(fieldWidth, ComponentPropertiesView.FieldHeight);
                fieldX += fieldWidth + ComponentPropertiesView.FieldSpacing;
            }
        }
    }
}
