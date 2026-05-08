using helengine.baseplatform.Definitions;

namespace helengine.editor;

/// <summary>
/// Owns the schema selector and field rows for one platform inside the material authoring view.
/// </summary>
public sealed class MaterialAssetPlatformPanel : IDisposable {
    /// <summary>
    /// Height of each visible row.
    /// </summary>
    const int RowHeight = 24;

    /// <summary>
    /// Spacing between stacked rows.
    /// </summary>
    const int RowSpacing = 6;

    /// <summary>
    /// Spacing between label, value, and button controls.
    /// </summary>
    const int ControlSpacing = 8;

    /// <summary>
    /// Width reserved for row labels.
    /// </summary>
    const int LabelWidth = 96;

    /// <summary>
    /// Width reserved for the optional field action button.
    /// </summary>
    const int ButtonWidth = 80;

    /// <summary>
    /// Label text shown above the schema selector.
    /// </summary>
    const string SchemaLabel = "Schema";

    /// <summary>
    /// Root entity that owns the platform-specific panel visuals.
    /// </summary>
    readonly EditorEntity RootValue;

    /// <summary>
    /// Host entity for the schema label.
    /// </summary>
    readonly EditorEntity SchemaLabelHost;

    /// <summary>
    /// Text component used to render the schema label.
    /// </summary>
    readonly TextComponent SchemaLabelText;

    /// <summary>
    /// Host entity for the schema picker control.
    /// </summary>
    readonly EditorEntity SchemaComboHost;

    /// <summary>
    /// Combo box used to choose which schema is active for this platform.
    /// </summary>
    readonly ComboBoxComponent SchemaComboBox;

    /// <summary>
    /// UI rows created for the active schema.
    /// </summary>
    readonly List<MaterialAssetFieldEditorRow> FieldRowsValue;

    /// <summary>
    /// Tracks whether schema-combo updates are being applied programmatically.
    /// </summary>
    bool IsUpdatingSchemaSelection;

    /// <summary>
    /// Cached layout height.
    /// </summary>
    int LayoutHeightValue;

    /// <summary>
    /// Initializes a new platform panel.
    /// </summary>
    /// <param name="platformId">Platform identifier represented by the panel.</param>
    /// <param name="font">Font used for text rendering.</param>
    /// <param name="layerMask">Layer mask applied to the panel hierarchy.</param>
    /// <param name="textOrder">Render order used for text labels and values.</param>
    public MaterialAssetPlatformPanel(string platformId, FontAsset font, ushort layerMask, byte textOrder) {
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        } else if (font == null) {
            throw new ArgumentNullException(nameof(font));
        }

        PlatformId = platformId;
        FieldRowsValue = new List<MaterialAssetFieldEditorRow>(8);

        RootValue = new EditorEntity();
        RootValue.LayerMask = layerMask;
        RootValue.InternalEntity = true;

        SchemaLabelHost = new EditorEntity();
        SchemaLabelHost.LayerMask = layerMask;
        RootValue.AddChild(SchemaLabelHost);

        SchemaLabelText = new TextComponent();
        SchemaLabelText.Font = font;
        SchemaLabelText.Text = SchemaLabel;
        SchemaLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
        SchemaLabelText.RenderOrder2D = textOrder;
        SchemaLabelHost.AddComponent(SchemaLabelText);

        SchemaComboHost = new EditorEntity();
        SchemaComboHost.LayerMask = layerMask;
        RootValue.AddChild(SchemaComboHost);

        SchemaComboBox = new ComboBoxComponent(new int2(180, RowHeight), font, Array.Empty<string>(), -1);
        SchemaComboHost.AddComponent(SchemaComboBox);
    }

    /// <summary>
    /// Gets the platform identifier represented by the panel.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the root entity that owns the panel visuals.
    /// </summary>
    public EditorEntity Root => RootValue;

    /// <summary>
    /// Gets the schema selector control for the platform.
    /// </summary>
    public ComboBoxComponent SchemaComboBoxControl => SchemaComboBox;

    /// <summary>
    /// Gets the editable field rows attached to this panel.
    /// </summary>
    public IReadOnlyList<MaterialAssetFieldEditorRow> FieldRows => FieldRowsValue;

    /// <summary>
    /// Gets the cached layout height for the panel content.
    /// </summary>
    public int Height => LayoutHeightValue;

    /// <summary>
    /// Gets or sets a value indicating whether the schema combo-box is being updated programmatically.
    /// </summary>
    public bool IsUpdatingSchemaSelectionValue {
        get { return IsUpdatingSchemaSelection; }
        set { IsUpdatingSchemaSelection = value; }
    }

    /// <summary>
    /// Sets the panel visibility.
    /// </summary>
    /// <param name="isVisible">True when the panel should be shown.</param>
    public void SetVisible(bool isVisible) {
        RootValue.Enabled = isVisible;
    }

    /// <summary>
    /// Updates the schema combo-box entries and current selection.
    /// </summary>
    /// <param name="materialSchemas">Schemas available to the panel's platform.</param>
    /// <param name="selectedSchemaId">Schema identifier that should be selected.</param>
    public void UpdateSchemaPicker(
        IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas,
        string selectedSchemaId) {
        if (materialSchemas == null) {
            throw new ArgumentNullException(nameof(materialSchemas));
        }

        List<string> schemaDisplayNames = new List<string>(materialSchemas.Count);
        for (int index = 0; index < materialSchemas.Count; index++) {
            PlatformMaterialSchemaDefinition materialSchema = materialSchemas[index];
            schemaDisplayNames.Add(materialSchema.DisplayName);
        }

        IsUpdatingSchemaSelection = true;
        try {
            SchemaComboBox.SetItems(schemaDisplayNames, FindSchemaIndex(materialSchemas, selectedSchemaId));
        } finally {
            IsUpdatingSchemaSelection = false;
        }

        SchemaComboBox.IsOpen = false;
    }

    /// <summary>
    /// Adds one field row to the panel.
    /// </summary>
    /// <param name="row">Created field row.</param>
    public void AddFieldRow(MaterialAssetFieldEditorRow row) {
        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }

        FieldRowsValue.Add(row);
        RootValue.AddChild(row.LabelHost);
        RootValue.AddChild(row.ValueHost);
        if (row.ButtonHost != null) {
            RootValue.AddChild(row.ButtonHost);
        }
    }

    /// <summary>
    /// Disposes the current field rows and clears the panel content.
    /// </summary>
    public void ClearFieldRows() {
        for (int index = 0; index < FieldRowsValue.Count; index++) {
            MaterialAssetFieldEditorRow row = FieldRowsValue[index];
            row.LabelHost.Dispose();
            row.ValueHost.Dispose();
            if (row.ButtonHost != null) {
                row.ButtonHost.Dispose();
            }
        }

        FieldRowsValue.Clear();
    }

    /// <summary>
    /// Updates the panel layout within the material authoring view.
    /// </summary>
    /// <param name="left">Left offset in pixels.</param>
    /// <param name="top">Top offset in pixels.</param>
    /// <param name="width">Available width in pixels.</param>
    public void UpdateLayout(int left, int top, int width) {
        int safeWidth = Math.Max(0, width);
        int labelWidth = (int)Math.Round(safeWidth * 0.4, MidpointRounding.AwayFromZero);
        if (labelWidth > safeWidth) {
            labelWidth = safeWidth;
        }

        int comboWidth = safeWidth - labelWidth - ControlSpacing;
        if (comboWidth < 1) {
            comboWidth = 1;
        }

        int currentTop = 0;

        RootValue.Position = new float3(left, top, 0.1f);
        SchemaLabelHost.Position = new float3(0f, currentTop, 0.2f);
        SchemaLabelText.Size = new int2(labelWidth, RowHeight);

        int comboLeft = labelWidth + ControlSpacing;
        SchemaComboHost.Position = new float3(comboLeft, currentTop, 0.2f);
        SchemaComboBox.Size = new int2(comboWidth, RowHeight);

        currentTop += RowHeight + RowSpacing;
        for (int index = 0; index < FieldRowsValue.Count; index++) {
            LayoutFieldRow(FieldRowsValue[index], currentTop, safeWidth, labelWidth);
            currentTop += RowHeight + RowSpacing;
        }

        LayoutHeightValue = currentTop;
    }

    /// <summary>
    /// Disposes the panel and all generated child content.
    /// </summary>
    public void Dispose() {
        ClearFieldRows();
        RootValue.Dispose();
    }

    /// <summary>
    /// Finds the selected schema index inside one schema list.
    /// </summary>
    /// <param name="materialSchemas">Schemas to search.</param>
    /// <param name="schemaId">Schema identifier to locate.</param>
    /// <returns>Matching combo-box index or zero when the identifier is unavailable.</returns>
    int FindSchemaIndex(IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas, string schemaId) {
        if (string.IsNullOrWhiteSpace(schemaId)) {
            return materialSchemas.Count > 0 ? 0 : -1;
        }

        for (int index = 0; index < materialSchemas.Count; index++) {
            if (string.Equals(materialSchemas[index].SchemaId, schemaId, StringComparison.OrdinalIgnoreCase)) {
                return index;
            }
        }

        return materialSchemas.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Lays out one schema-driven field row.
    /// </summary>
    /// <param name="row">Row to lay out.</param>
    /// <param name="top">Top offset in pixels relative to the panel.</param>
    /// <param name="safeWidth">Available width in pixels.</param>
    /// <param name="labelWidth">Reserved label width.</param>
    void LayoutFieldRow(MaterialAssetFieldEditorRow row, int top, int safeWidth, int labelWidth) {
        int buttonWidth = row.ButtonHost != null ? Math.Min(ButtonWidth, safeWidth) : 0;
        int valueWidth = Math.Max(0, safeWidth - labelWidth - (row.ButtonHost != null ? buttonWidth + (ControlSpacing * 2) : ControlSpacing));

        row.LabelHost.Position = new float3(0f, top, 0.2f);
        row.LabelText.Size = new int2(labelWidth, RowHeight);

        int valueLeft = labelWidth + ControlSpacing;
        row.ValueHost.Position = new float3(valueLeft, top, 0.2f);

        if (row.TextBox != null) {
            row.TextBox.Size = new int2(valueWidth, RowHeight);
        } else if (row.ComboBox != null) {
            row.ComboBox.Size = new int2(valueWidth, RowHeight);
        } else if (row.CheckBox != null) {
            row.CheckBox.Size = new int2(RowHeight, RowHeight);
        } else if (row.ColorControl != null) {
            row.ColorControl.Size = new int2(valueWidth, RowHeight);
        }

        if (row.ButtonHost != null) {
            int buttonLeft = valueLeft + valueWidth + ControlSpacing;
            row.ButtonHost.Position = new float3(buttonLeft, top, 0.2f);
        }
    }
}
