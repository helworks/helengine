namespace helengine.editor {
    /// <summary>
    /// Renders choice property rows as a drop-down whose entries come from the row descriptor. Selection changes are
    /// forwarded to the inspector, and refreshes suppress the change notification so re-selecting the bound value is
    /// not mistaken for a user edit.
    /// </summary>
    sealed class ComboBoxComponentPropertyRowRenderer : ComponentPropertyRowRenderer {
        /// <summary>
        /// Minimum width in pixels allowed for the drop-down control.
        /// </summary>
        const int MinimumFieldWidth = 48;

        /// <summary>
        /// Initial width in pixels used when the drop-down control is created.
        /// </summary>
        const int InitialFieldWidth = 120;

        /// <summary>
        /// Binds the renderer to the inspector view that owns the choice rows.
        /// </summary>
        /// <param name="view">Inspector view that owns the rendered rows.</param>
        public ComboBoxComponentPropertyRowRenderer(ComponentPropertiesView view) : base(view) {
        }

        /// <summary>
        /// Gets the choice row kind handled by this renderer.
        /// </summary>
        public override ComponentPropertyRowKind Kind {
            get { return ComponentPropertyRowKind.ComboBox; }
        }

        /// <summary>
        /// Creates the drop-down control and forwards its selection changes to the inspector.
        /// </summary>
        /// <param name="row">Row being populated.</param>
        /// <param name="rowEntity">Row root entity that owns the created drop-down.</param>
        public override void Build(ComponentPropertyRow row, EditorEntity rowEntity) {
            var comboBoxHost = new EditorEntity(View.RootEntity.OwnerCore, View.RootEntity.InteractionServices);
            comboBoxHost.LayerMask = View.RootEntity.LayerMask;
            comboBoxHost.Position = float3.Zero;
            rowEntity.AddChild(comboBoxHost);

            var comboBox = new ComboBoxComponent(new int2(InitialFieldWidth, ComponentPropertiesView.FieldHeight), View.Font, Array.Empty<string>(), -1);
            comboBox.SelectionChanged += (selectedIndex, selectedItem) => View.HandleComboBoxRowSelectionChanged(row, selectedItem);
            comboBoxHost.AddComponent(comboBox);

            row.ComboBoxHost = comboBoxHost;
            row.ComboBoxField = comboBox;
        }

        /// <summary>
        /// Selects the entry matching the bound value, clearing the selection when no entry matches.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        public override void Update(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.ComboBoxField == null) {
                return;
            }

            string currentValue = string.Empty;
            object rawValue = View.GetRowValue(row);
            if (rawValue is string textValue) {
                currentValue = textValue;
            }

            int selectedIndex = -1;
            IReadOnlyList<string> items = row.ComboBoxField.Items;
            for (int index = 0; index < items.Count; index++) {
                if (string.Equals(items[index], currentValue, StringComparison.OrdinalIgnoreCase)) {
                    selectedIndex = index;
                    break;
                }
            }

            View.IsSynchronizing = true;
            if (row.ComboBoxField.SelectedIndex != selectedIndex) {
                row.ComboBoxField.SelectedIndex = selectedIndex;
            }
            View.IsSynchronizing = false;
        }

        /// <summary>
        /// Stretches the drop-down across the width remaining after the row label.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="width">Content width available to the row.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width already reserved for the row label.</param>
        public override void Layout(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ComboBoxHost == null || row.ComboBoxField == null) {
                return;
            }

            int fieldWidth = Math.Max(MinimumFieldWidth, width - labelWidth - ComponentPropertiesView.FieldSpacing);
            float fieldY = (float)Math.Round((height - ComponentPropertiesView.FieldHeight) * 0.5);
            row.ComboBoxHost.Position = new float3(labelWidth + ComponentPropertiesView.FieldSpacing, fieldY, 0.2f);
            row.ComboBoxField.Size = new int2(fieldWidth, ComponentPropertiesView.FieldHeight);
        }
    }
}
