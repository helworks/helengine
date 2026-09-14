namespace helengine.editor {
    /// <summary>
    /// Renders Vector4 property rows as four side-by-side numeric text fields labelled R, G, B and A, which is the
    /// shape used by colour-like nested values exposed through provider-backed editors.
    /// </summary>
    sealed class Vector4ComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Minimum width in pixels allowed for one vector component field.
        /// </summary>
        const int MinimumFieldWidth = 40;

        /// <summary>
        /// Initial width in pixels used when the field control is created.
        /// </summary>
        const int InitialFieldWidth = 48;

        /// <summary>
        /// Placeholder text shown in each component field before a value is bound.
        /// </summary>
        static readonly string[] Placeholders = new string[] { "R", "G", "B", "A" };

        /// <summary>
        /// Binds the renderer to the inspector view that owns the Vector4 rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public Vector4ComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the Vector4 row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.Vector4; }
        }

        /// <summary>
        /// Creates the four component text fields and registers them for submit handling.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created fields.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            row.Vector4FieldHosts = new EditorEntity[4];
            row.Vector4Fields = new TextBoxComponent[4];
            row.Vector4Cache = new string[4];

            for (int index = 0; index < row.Vector4FieldHosts.Length; index++) {
                var fieldHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
                fieldHost.LayerMask = View.RootEntity.LayerMask;
                fieldHost.Position = float3.Zero;
                rowEntity.AddChild(fieldHost);

                var field = new TextBoxComponent(new int2(InitialFieldWidth, ComponentPropertiesView.FieldHeight), View.Font, Placeholders[index]);
                field.Submitted += View.HandleVector4Submitted;
                fieldHost.AddComponent(field);

                row.Vector4FieldHosts[index] = fieldHost;
                row.Vector4Fields[index] = field;
                View.Vector4FieldRows[field] = row;
            }
        }

        /// <summary>
        /// Writes the bound Vector4 value into the component fields, falling back to zeros when no value is readable.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            if (!View.TryGetVector4Value(row, out float4 value)) {
                View.SetVector4Fields(row, 0.0, 0.0, 0.0, 0.0);
                return;
            }

            View.SetVector4Fields(row, value.X, value.Y, value.Z, value.W);
        }

        /// <summary>
        /// Spreads the four component fields across the space left over after the row label.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.Vector4FieldHosts == null || row.Vector4Fields == null) {
                return;
            }

            int available = Math.Max(0, width - labelWidth - (ComponentPropertiesView.FieldSpacing * 3));
            int fieldWidth = Math.Max(MinimumFieldWidth, available / 4);
            float fieldY = (float)Math.Round((height - ComponentPropertiesView.FieldHeight) * 0.5);

            int fieldX = labelWidth + ComponentPropertiesView.FieldSpacing;
            for (int index = 0; index < row.Vector4FieldHosts.Length; index++) {
                row.Vector4FieldHosts[index].Position = new float3(fieldX, fieldY, 0.2f);
                row.Vector4Fields[index].Size = new int2(fieldWidth, ComponentPropertiesView.FieldHeight);
                fieldX += fieldWidth + ComponentPropertiesView.FieldSpacing;
            }
        }
    }
}
