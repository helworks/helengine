using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Stores UI elements and binding metadata for a component property row.
    /// </summary>
    public sealed class ComponentPropertyRow {
        /// <summary>
        /// Initializes a new row container with the required components.
        /// </summary>
        /// <param name="kind">Row layout kind.</param>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="labelHost">Host entity for the label.</param>
        /// <param name="label">Label text component.</param>
        public ComponentPropertyRow(
            ComponentPropertyRowKind kind,
            EditorEntity entity,
            EditorEntity labelHost,
            TextComponent label) {
            Kind = kind;
            Entity = entity;
            LabelHost = labelHost;
            Label = label;
        }

        /// <summary>
        /// Gets the row layout kind.
        /// </summary>
        public ComponentPropertyRowKind Kind { get; }

        /// <summary>
        /// Gets the root entity for the row.
        /// </summary>
        public EditorEntity Entity { get; }

        /// <summary>
        /// Gets the host entity for the row label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the label text component for the row.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets or sets the common live component attached to the entity.
        /// </summary>
        public Component CommonComponent { get; set; }

        /// <summary>
        /// Gets or sets the component that owns the property.
        /// </summary>
        public Component TargetComponent { get; set; }

        /// <summary>
        /// Gets or sets the hidden save component attached to the owning entity.
        /// </summary>
        public EntitySaveComponent SaveComponent { get; set; }

        /// <summary>
        /// Gets or sets the platform id currently being edited by the row.
        /// </summary>
        public string EditingPlatformId { get; set; }

        /// <summary>
        /// Gets or sets the bound property metadata.
        /// </summary>
        public PropertyInfo Property { get; set; }

        /// <summary>
        /// Gets or sets the effective value type edited by this row.
        /// </summary>
        public Type ValueType { get; set; }

        /// <summary>
        /// Gets or sets the stable custom editor type identifier when this row participates in a provider-backed editor.
        /// </summary>
        public string CustomEditorTypeId { get; set; }

        /// <summary>
        /// Gets or sets the nested member name edited by this row when it targets one field inside a provider-backed property value.
        /// </summary>
        public string NestedMemberName { get; set; }

        /// <summary>
        /// Gets or sets the stable custom-editor entry key used by provider-backed rows that edit keyed collections.
        /// </summary>
        public string CustomEditorEntryKey { get; set; }

        /// <summary>
        /// Gets or sets the indentation level applied during layout.
        /// </summary>
        public int IndentLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a provider-backed section row is currently expanded.
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Gets or sets the vector field hosts for Vector3 rows.
        /// </summary>
        public EditorEntity[] VectorFieldHosts { get; set; }

        /// <summary>
        /// Gets or sets the vector field text boxes for Vector3 rows.
        /// </summary>
        public TextBoxComponent[] VectorFields { get; set; }

        /// <summary>
        /// Gets or sets the text cache for vector fields.
        /// </summary>
        public string[] VectorCache { get; set; }

        /// <summary>
        /// Gets or sets the vector field hosts for Vector4 rows.
        /// </summary>
        public EditorEntity[] Vector4FieldHosts { get; set; }

        /// <summary>
        /// Gets or sets the vector field text boxes for Vector4 rows.
        /// </summary>
        public TextBoxComponent[] Vector4Fields { get; set; }

        /// <summary>
        /// Gets or sets the text cache for Vector4 rows.
        /// </summary>
        public string[] Vector4Cache { get; set; }

        /// <summary>
        /// Gets or sets the text box for scalar rows.
        /// </summary>
        public TextBoxComponent ScalarField { get; set; }

        /// <summary>
        /// Gets or sets the cached text for scalar rows.
        /// </summary>
        public string ScalarCache { get; set; }

        /// <summary>
        /// Gets or sets the host entity for boolean checkbox rows.
        /// </summary>
        public EditorEntity CheckBoxHost { get; set; }

        /// <summary>
        /// Gets or sets the checkbox component for boolean rows.
        /// </summary>
        public CheckBoxComponent CheckBoxField { get; set; }

        /// <summary>
        /// Gets or sets the host entity for value text.
        /// </summary>
        public EditorEntity ValueHost { get; set; }

        /// <summary>
        /// Gets or sets the value text component.
        /// </summary>
        public TextComponent ValueText { get; set; }

        /// <summary>
        /// Gets or sets the host entity for action buttons.
        /// </summary>
        public EditorEntity ActionButtonHost { get; set; }

        /// <summary>
        /// Gets or sets the action button component.
        /// </summary>
        public ButtonComponent ActionButton { get; set; }

        /// <summary>
        /// Gets or sets the background surface used by one custom section row.
        /// </summary>
        public SpriteComponent HeaderBackground { get; set; }

        /// <summary>
        /// Gets or sets the interactable surface used by one custom section row.
        /// </summary>
        public InteractableComponent HeaderInteractable { get; set; }

        /// <summary>
        /// Gets or sets the outline shown when the row is explicitly overridden on the active platform.
        /// </summary>
        public RoundedRectComponent OverrideOutline { get; set; }

        /// <summary>
        /// Gets or sets the host entity for the row-level revert button.
        /// </summary>
        public EditorEntity RevertButtonHost { get; set; }

        /// <summary>
        /// Gets or sets the row-level revert button component.
        /// </summary>
        public ButtonComponent RevertButton { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is currently overridden on the active platform.
        /// </summary>
        public bool IsOverrideActive { get; set; }
    }
}
