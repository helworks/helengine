using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Stores the UI controls used to edit one builder-defined material field.
    /// </summary>
    public sealed class MaterialAssetFieldEditorRow {
        /// <summary>
        /// Initializes one field-editor row.
        /// </summary>
        /// <param name="fieldId">Builder-defined material field identifier.</param>
        /// <param name="fieldKind">Builder-defined material field kind.</param>
        /// <param name="labelHost">Host entity for the field label.</param>
        /// <param name="labelText">Text component used to render the field label.</param>
        /// <param name="valueHost">Host entity for the primary editor control.</param>
        /// <param name="textBox">Text box used by text-backed field kinds.</param>
        /// <param name="comboBox">Combo box used by choice-backed field kinds.</param>
        /// <param name="checkBox">Check box used by boolean-backed field kinds.</param>
        /// <param name="buttonHost">Optional host entity for an auxiliary action button.</param>
        /// <param name="button">Optional auxiliary action button.</param>
        /// <param name="colorControl">Optional reusable color field control.</param>
        public MaterialAssetFieldEditorRow(
            string fieldId,
            PlatformMaterialFieldKind fieldKind,
            EditorEntity labelHost,
            TextComponent labelText,
            EditorEntity valueHost,
            TextBoxComponent textBox,
            ComboBoxComponent comboBox,
            CheckBoxComponent checkBox,
            EditorEntity buttonHost,
            ButtonComponent button,
            EditorColorFieldControl colorControl) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (labelHost == null) {
                throw new ArgumentNullException(nameof(labelHost));
            } else if (labelText == null) {
                throw new ArgumentNullException(nameof(labelText));
            } else if (valueHost == null) {
                throw new ArgumentNullException(nameof(valueHost));
            }

            FieldId = fieldId;
            FieldKind = fieldKind;
            LabelHost = labelHost;
            LabelText = labelText;
            ValueHost = valueHost;
            TextBox = textBox;
            ComboBox = comboBox;
            CheckBox = checkBox;
            ButtonHost = buttonHost;
            Button = button;
            ColorControl = colorControl;
        }

        /// <summary>
        /// Gets the builder-defined material field identifier.
        /// </summary>
        public string FieldId { get; }

        /// <summary>
        /// Gets the builder-defined material field kind.
        /// </summary>
        public PlatformMaterialFieldKind FieldKind { get; }

        /// <summary>
        /// Gets the host entity for the field label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the text component used to render the field label.
        /// </summary>
        public TextComponent LabelText { get; }

        /// <summary>
        /// Gets the host entity for the primary editor control.
        /// </summary>
        public EditorEntity ValueHost { get; }

        /// <summary>
        /// Gets the text box used by text-backed field kinds.
        /// </summary>
        public TextBoxComponent TextBox { get; }

        /// <summary>
        /// Gets the combo box used by choice-backed field kinds.
        /// </summary>
        public ComboBoxComponent ComboBox { get; }

        /// <summary>
        /// Gets the check box used by boolean-backed field kinds.
        /// </summary>
        public CheckBoxComponent CheckBox { get; }

        /// <summary>
        /// Gets the optional host entity for an auxiliary action button.
        /// </summary>
        public EditorEntity ButtonHost { get; }

        /// <summary>
        /// Gets the optional auxiliary action button.
        /// </summary>
        public ButtonComponent Button { get; }

        /// <summary>
        /// Gets the optional reusable color field control.
        /// </summary>
        public EditorColorFieldControl ColorControl { get; }
    }
}
