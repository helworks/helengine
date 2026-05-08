using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Presents schema-driven per-platform material authoring inside the properties panel.
    /// </summary>
    public class MaterialAssetView {
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
        /// Field id used by compatibility shader-backed schemas for shader assignment.
        /// </summary>
        const string ShaderAssetIdFieldId = "shader-asset-id";

        /// <summary>
        /// Field id used by compatibility shader-backed schemas for vertex program assignment.
        /// </summary>
        const string VertexProgramFieldId = "vertex-program";

        /// <summary>
        /// Field id used by compatibility shader-backed schemas for pixel program assignment.
        /// </summary>
        const string PixelProgramFieldId = "pixel-program";

        /// <summary>
        /// Field id used by compatibility shader-backed schemas for variant assignment.
        /// </summary>
        const string VariantFieldId = "variant";

        /// <summary>
        /// Default shader variant mirrored into compatibility fields.
        /// </summary>
        const string DefaultVariant = "default";

        /// <summary>
        /// Font used for text elements.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Render order used for text labels and values.
        /// </summary>
        readonly byte TextOrder;

        /// <summary>
        /// Root entity that owns view visuals.
        /// </summary>
        readonly EditorEntity RootEntity;

        /// <summary>
        /// Host entity for the platform label.
        /// </summary>
        readonly EditorEntity PlatformLabelHost;

        /// <summary>
        /// Text component for the platform label.
        /// </summary>
        readonly TextComponent PlatformLabelText;

        /// <summary>
        /// Host entity for the platform picker control.
        /// </summary>
        readonly EditorEntity PlatformComboHost;

        /// <summary>
        /// Combo box used to choose which platform material settings are being edited.
        /// </summary>
        readonly ComboBoxComponent PlatformComboBox;

        /// <summary>
        /// Host entity for the schema label.
        /// </summary>
        readonly EditorEntity SchemaLabelHost;

        /// <summary>
        /// Text component for the schema label.
        /// </summary>
        readonly TextComponent SchemaLabelText;

        /// <summary>
        /// Host entity for the schema picker control.
        /// </summary>
        readonly EditorEntity SchemaComboHost;

        /// <summary>
        /// Combo box used to choose which schema is active for the selected platform.
        /// </summary>
        readonly ComboBoxComponent SchemaComboBox;

        /// <summary>
        /// Host entity for the status text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Text component used to render material authoring status messages.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Supported platform identifiers shown in the platform picker.
        /// </summary>
        readonly List<string> SupportedPlatformIds;

        /// <summary>
        /// Schema identifiers shown in the active schema picker.
        /// </summary>
        readonly List<string> AvailableSchemaIds;

        /// <summary>
        /// UI rows created for the active material schema.
        /// </summary>
        readonly List<MaterialAssetFieldEditorRow> FieldRows;

        /// <summary>
        /// Service used to load and save material settings sidecars.
        /// </summary>
        readonly MaterialAssetSettingsService SettingsService;

        /// <summary>
        /// Service used to normalize and switch builder-defined schema selections.
        /// </summary>
        readonly MaterialAssetSchemaSettingsService SchemaSettingsService;

        /// <summary>
        /// Currently selected asset entry.
        /// </summary>
        AssetBrowserEntry CurrentEntry;

        /// <summary>
        /// Currently loaded material asset.
        /// </summary>
        MaterialAsset CurrentAsset;

        /// <summary>
        /// Currently loaded material sidecar settings.
        /// </summary>
        AssetImportSettings CurrentSettings;

        /// <summary>
        /// Current platform metadata resolver.
        /// </summary>
        Func<string, EditorPlatformBuildSelectionModel> SelectionModelResolver;

        /// <summary>
        /// Currently selected platform identifier.
        /// </summary>
        string CurrentPlatformId;

        /// <summary>
        /// Cached layout height.
        /// </summary>
        int LayoutHeight;

        /// <summary>
        /// Tracks whether the view is visible.
        /// </summary>
        bool IsViewVisible;

        /// <summary>
        /// Tracks whether platform-combo updates are being applied programmatically.
        /// </summary>
        bool IsUpdatingPlatformSelection;

        /// <summary>
        /// Tracks whether schema-combo updates are being applied programmatically.
        /// </summary>
        bool IsUpdatingSchemaSelection;

        /// <summary>
        /// Initializes a new view for material asset editing.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the view entities.</param>
        public MaterialAssetView(FontAsset font, ushort layerMask) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            TextOrder = RenderOrder2D.PanelForeground;
            SupportedPlatformIds = new List<string>(4);
            AvailableSchemaIds = new List<string>(4);
            FieldRows = new List<MaterialAssetFieldEditorRow>(8);
            SettingsService = new MaterialAssetSettingsService();
            SchemaSettingsService = new MaterialAssetSchemaSettingsService();

            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.InternalEntity = true;

            PlatformLabelHost = CreateTextHost(layerMask, out PlatformLabelText, "Platform");
            RootEntity.AddChild(PlatformLabelHost);

            PlatformComboHost = new EditorEntity();
            PlatformComboHost.LayerMask = layerMask;
            RootEntity.AddChild(PlatformComboHost);

            PlatformComboBox = new ComboBoxComponent(new int2(160, RowHeight), font, Array.Empty<string>(), -1);
            PlatformComboBox.SelectionChanged += HandlePlatformSelectionChanged;
            PlatformComboHost.AddComponent(PlatformComboBox);

            SchemaLabelHost = CreateTextHost(layerMask, out SchemaLabelText, "Schema");
            RootEntity.AddChild(SchemaLabelHost);

            SchemaComboHost = new EditorEntity();
            SchemaComboHost.LayerMask = layerMask;
            RootEntity.AddChild(SchemaComboHost);

            SchemaComboBox = new ComboBoxComponent(new int2(180, RowHeight), font, Array.Empty<string>(), -1);
            SchemaComboBox.SelectionChanged += HandleSchemaSelectionChanged;
            SchemaComboHost.AddComponent(SchemaComboBox);

            StatusHost = CreateTextHost(layerMask, out StatusText, string.Empty);
            RootEntity.AddChild(StatusHost);
            StatusText.Color = ThemeManager.Colors.InputForegroundSecondary;

            Hide();
        }

        /// <summary>
        /// Gets the root entity to attach into the properties panel.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets the current height of the view layout.
        /// </summary>
        public int Height => LayoutHeight;

        /// <summary>
        /// Gets a value indicating whether the view is visible.
        /// </summary>
        public bool IsVisible => IsViewVisible;

        /// <summary>
        /// Shows the view for the specified material asset and per-platform settings payload.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="materialAsset">Material asset to edit.</param>
        /// <param name="settings">Per-platform settings sidecar for the material asset.</param>
        /// <param name="supportedPlatforms">Supported project platform identifiers.</param>
        /// <param name="activePlatformId">Currently active project platform identifier.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform.</param>
        public void Show(
            AssetBrowserEntry entry,
            MaterialAsset materialAsset,
            AssetImportSettings settings,
            IReadOnlyList<string> supportedPlatforms,
            string activePlatformId,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (supportedPlatforms.Count == 0) {
                throw new ArgumentException("At least one supported platform must be provided.", nameof(supportedPlatforms));
            } else if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            } else if (selectionModelResolver == null) {
                throw new ArgumentNullException(nameof(selectionModelResolver));
            }

            CurrentEntry = entry;
            CurrentAsset = materialAsset;
            CurrentSettings = settings;
            SelectionModelResolver = selectionModelResolver;

            SetSupportedPlatforms(supportedPlatforms);
            CurrentPlatformId = ResolveSelectedPlatformId(activePlatformId);
            IsUpdatingPlatformSelection = true;
            PlatformComboBox.SetItems(SupportedPlatformIds, FindPlatformIndex(CurrentPlatformId));
            IsUpdatingPlatformSelection = false;
            PlatformComboBox.IsOpen = false;

            RebuildFieldRows();
            UpdateDisplayedValues();
            IsViewVisible = true;
            RootEntity.Enabled = true;
        }

        /// <summary>
        /// Hides the view and clears the current material state.
        /// </summary>
        public void Hide() {
            if (CurrentEntry != null && CurrentSettings != null) {
                SyncCurrentFieldValues(saveToDisk: true);
            }

            ClearFieldRows();
            IsViewVisible = false;
            RootEntity.Enabled = false;
            CurrentEntry = null;
            CurrentAsset = null;
            CurrentSettings = null;
            SelectionModelResolver = null;
            CurrentPlatformId = string.Empty;
            SupportedPlatformIds.Clear();
            AvailableSchemaIds.Clear();
            IsUpdatingPlatformSelection = true;
            PlatformComboBox.SetItems(Array.Empty<string>(), -1);
            IsUpdatingPlatformSelection = false;
            IsUpdatingSchemaSelection = true;
            SchemaComboBox.SetItems(Array.Empty<string>(), -1);
            IsUpdatingSchemaSelection = false;
            StatusText.Text = string.Empty;
        }

        /// <summary>
        /// Updates the view layout within the properties panel.
        /// </summary>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available width in pixels.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (!IsViewVisible) {
                LayoutHeight = 0;
                return;
            }

            int safeWidth = Math.Max(0, width);
            int labelWidth = Math.Min(LabelWidth, safeWidth);
            int currentTop = top;

            LayoutLabelAndCombo(PlatformLabelHost, PlatformLabelText, PlatformComboHost, labelWidth, Math.Max(0, safeWidth - labelWidth - ControlSpacing), left, currentTop);

            currentTop += RowHeight + RowSpacing;
            LayoutLabelAndCombo(SchemaLabelHost, SchemaLabelText, SchemaComboHost, labelWidth, Math.Max(0, safeWidth - labelWidth - ControlSpacing), left, currentTop);

            for (int index = 0; index < FieldRows.Count; index++) {
                currentTop += RowHeight + RowSpacing;
                LayoutFieldRow(FieldRows[index], left, currentTop, safeWidth, labelWidth);
            }

            currentTop += RowHeight + RowSpacing;
            StatusHost.Position = new float3(left, currentTop, 0.2f);
            StatusText.Size = new int2(safeWidth, RowHeight);

            LayoutHeight = (currentTop - top) + RowHeight;
        }

        /// <summary>
        /// Handles platform picker selection changes.
        /// </summary>
        /// <param name="index">Selected platform index.</param>
        /// <param name="value">Selected platform identifier.</param>
        void HandlePlatformSelectionChanged(int index, string value) {
            if (IsUpdatingPlatformSelection) {
                return;
            }

            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Platform selection was not provided.");
            }

            SyncCurrentFieldValues(saveToDisk: true);
            CurrentPlatformId = value;
            RebuildFieldRows();
            UpdateDisplayedValues();
        }

        /// <summary>
        /// Handles schema picker selection changes.
        /// </summary>
        /// <param name="index">Selected schema index.</param>
        /// <param name="value">Selected schema display label.</param>
        void HandleSchemaSelectionChanged(int index, string value) {
            if (IsUpdatingSchemaSelection) {
                return;
            } else if (index < 0 || index >= AvailableSchemaIds.Count) {
                throw new InvalidOperationException("Schema selection index is out of range.");
            }

            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            if (materialSettings == null) {
                return;
            }

            string schemaId = AvailableSchemaIds[index];
            SchemaSettingsService.SelectSchema(materialSettings, ResolveAvailableSchemas(), schemaId);
            RebuildFieldRows();
            SaveCurrentMaterialState();
            UpdateDisplayedValues();
        }

        /// <summary>
        /// Handles text-field changes for one schema-driven field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose text changed.</param>
        /// <param name="textBox">Text box whose value changed.</param>
        void HandleTextFieldChanged(string fieldId, TextBoxComponent textBox) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = textBox.Text ?? string.Empty;
        }

        /// <summary>
        /// Handles text-field submission for one schema-driven field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose text was submitted.</param>
        /// <param name="textBox">Text box whose value was submitted.</param>
        void HandleTextFieldSubmitted(string fieldId, TextBoxComponent textBox) {
            HandleTextFieldChanged(fieldId, textBox);
            SaveCurrentMaterialState();
            UpdateDisplayedValues();
        }

        /// <summary>
        /// Handles combo-box selection changes for one schema-driven choice field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose selection changed.</param>
        /// <param name="value">Selected serialized value.</param>
        void HandleChoiceFieldChanged(string fieldId, string value) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = value;
            SaveCurrentMaterialState();
            UpdateDisplayedValues();
        }

        /// <summary>
        /// Handles check-box state changes for one schema-driven boolean field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose value changed.</param>
        /// <param name="isChecked">Current checked state.</param>
        void HandleBooleanFieldChanged(string fieldId, bool isChecked) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = isChecked ? "true" : "false";
            SaveCurrentMaterialState();
            UpdateDisplayedValues();
        }

        /// <summary>
        /// Requests a shader pick from the asset picker service for one asset-reference field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier that should receive the shader asset id.</param>
        void RequestShaderPick(string fieldId) {
            if (CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                return;
            }

            EditorAssetPickerService.RequestPick(entry => HandleShaderPicked(fieldId, entry), EditorFileTemplateRegistry.ShaderExtension);
        }

        /// <summary>
        /// Handles shader selections from the asset picker.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier that should receive the shader asset id.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleShaderPicked(string fieldId, AssetBrowserEntry entry) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }
            if (entry == null || CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                return;
            }
            if (entry.IsDirectory || !IsShaderEntry(entry)) {
                return;
            }

            try {
                string shaderId = ShaderAssetIdUtils.BuildShaderAssetId(entry.FullPath);
                ApplyShaderIdToActivePlatform(fieldId, shaderId);
                UpdateFieldControlsFromSettings();
                SaveCurrentMaterialState();
                UpdateDisplayedValues();
                RefreshShaderResources(CurrentAsset.ShaderAssetId);
            } catch (Exception ex) {
                Logger.WriteError($"Failed to assign platform shader: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies one shader assignment to the active platform settings payload.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier that stores the shader asset id.</param>
        /// <param name="shaderId">Shader identifier to apply.</param>
        void ApplyShaderIdToActivePlatform(string fieldId, string shaderId) {
            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            if (materialSettings == null) {
                throw new InvalidOperationException("Active platform material settings are not available.");
            }

            materialSettings.FieldValues[fieldId] = shaderId ?? string.Empty;
            materialSettings.FieldValues[VertexProgramFieldId] = string.IsNullOrWhiteSpace(shaderId) ? string.Empty : string.Concat(shaderId, ".vs");
            materialSettings.FieldValues[PixelProgramFieldId] = string.IsNullOrWhiteSpace(shaderId) ? string.Empty : string.Concat(shaderId, ".ps");
            materialSettings.FieldValues[VariantFieldId] = string.IsNullOrWhiteSpace(shaderId) ? string.Empty : DefaultVariant;
        }

        /// <summary>
        /// Saves the material sidecar and mirrors the active platform fields back into the raw material asset for compatibility.
        /// </summary>
        void SaveCurrentMaterialState() {
            if (CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                throw new InvalidOperationException("Cannot save a material view that is not bound to an asset.");
            }

            SettingsService.ApplyPlatformCompatibilityFields(CurrentAsset, CurrentSettings, CurrentPlatformId);
            SaveMaterialAsset(CurrentEntry.FullPath, CurrentAsset);
            SettingsService.Save(CurrentEntry.FullPath, CurrentSettings);
        }

        /// <summary>
        /// Forces a shader reload so runtime materials update immediately.
        /// </summary>
        /// <param name="shaderId">Shader identifier to reload.</param>
        void RefreshShaderResources(string shaderId) {
            if (string.IsNullOrWhiteSpace(shaderId)) {
                return;
            }

            try {
                ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAsset(shaderId);
                Core.Instance.RenderManager3D.InvalidateShaderResources(shaderId, shaderAsset);
            } catch (Exception ex) {
                Logger.WriteError($"Shader refresh failed for '{shaderId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the material asset back to disk.
        /// </summary>
        /// <param name="path">Material asset file path.</param>
        /// <param name="materialAsset">Material asset to serialize.</param>
        void SaveMaterialAsset(string path, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Rebuilds the visible field-editor rows for the active schema.
        /// </summary>
        void RebuildFieldRows() {
            ClearFieldRows();

            PlatformMaterialSchemaDefinition materialSchema = EnsureActiveSchema();
            if (materialSchema == null) {
                return;
            }

            for (int index = 0; index < materialSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = materialSchema.Fields[index];
                MaterialAssetFieldEditorRow row = CreateFieldRow(field);
                FieldRows.Add(row);
                RootEntity.AddChild(row.LabelHost);
                RootEntity.AddChild(row.ValueHost);
                if (row.ButtonHost != null) {
                    RootEntity.AddChild(row.ButtonHost);
                }
            }

            UpdateFieldControlsFromSettings();
        }

        /// <summary>
        /// Removes the currently visible field-editor rows.
        /// </summary>
        void ClearFieldRows() {
            for (int index = 0; index < FieldRows.Count; index++) {
                MaterialAssetFieldEditorRow row = FieldRows[index];
                row.LabelHost.Dispose();
                row.ValueHost.Dispose();
                if (row.ButtonHost != null) {
                    row.ButtonHost.Dispose();
                }
            }

            FieldRows.Clear();
        }

        /// <summary>
        /// Creates one field-editor row for the supplied material field definition.
        /// </summary>
        /// <param name="field">Builder-defined field to render.</param>
        /// <returns>Created field-editor row.</returns>
        MaterialAssetFieldEditorRow CreateFieldRow(PlatformMaterialFieldDefinition field) {
            EditorEntity labelHost = CreateTextHost(RootEntity.LayerMask, out TextComponent labelText, field.DisplayName);
            EditorEntity valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;

            if (field.FieldKind == PlatformMaterialFieldKind.Boolean) {
                CheckBoxComponent checkBox = new CheckBoxComponent(new int2(RowHeight, RowHeight), Font);
                checkBox.CheckedChanged += (component, isChecked) => HandleBooleanFieldChanged(field.FieldId, isChecked);
                valueHost.AddComponent(checkBox);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, null, null, checkBox, null, null);
            }

            if (field.FieldKind == PlatformMaterialFieldKind.Choice) {
                ComboBoxComponent comboBox = new ComboBoxComponent(new int2(180, RowHeight), Font, field.AllowedValues, -1);
                comboBox.SelectionChanged += (index, value) => HandleChoiceFieldChanged(field.FieldId, value);
                valueHost.AddComponent(comboBox);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, null, comboBox, null, null, null);
            }

            TextBoxComponent textBox = new TextBoxComponent(new int2(180, RowHeight), Font);
            textBox.TextChanged += currentTextBox => HandleTextFieldChanged(field.FieldId, currentTextBox);
            textBox.Submitted += currentTextBox => HandleTextFieldSubmitted(field.FieldId, currentTextBox);
            valueHost.AddComponent(textBox);

            if (IsShaderPickerField(field)) {
                EditorEntity buttonHost = new EditorEntity();
                buttonHost.LayerMask = RootEntity.LayerMask;
                ButtonComponent button = new ButtonComponent("Pick", new int2(ButtonWidth, RowHeight), Font, () => RequestShaderPick(field.FieldId));
                buttonHost.AddComponent(button);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, textBox, null, null, buttonHost, button);
            }

            return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, textBox, null, null, null, null);
        }

        /// <summary>
        /// Updates field controls to reflect the active platform field-value payload.
        /// </summary>
        void UpdateFieldControlsFromSettings() {
            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            PlatformMaterialSchemaDefinition materialSchema = FindActiveSchema();
            if (materialSettings == null || materialSchema == null) {
                return;
            }

            for (int index = 0; index < FieldRows.Count; index++) {
                MaterialAssetFieldEditorRow row = FieldRows[index];
                PlatformMaterialFieldDefinition field = FindFieldDefinition(materialSchema, row.FieldId);
                string value = ResolveFieldValue(materialSettings, field);
                ApplyFieldValueToControl(row, field, value);
            }
        }

        /// <summary>
        /// Synchronizes the current field controls back into the active platform settings payload.
        /// </summary>
        /// <param name="saveToDisk">True when the material asset and sidecar should be persisted after syncing.</param>
        void SyncCurrentFieldValues(bool saveToDisk) {
            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            if (materialSettings == null) {
                return;
            }

            for (int index = 0; index < FieldRows.Count; index++) {
                MaterialAssetFieldEditorRow row = FieldRows[index];
                if (row.TextBox != null) {
                    materialSettings.FieldValues[row.FieldId] = row.TextBox.Text ?? string.Empty;
                } else if (row.ComboBox != null) {
                    materialSettings.FieldValues[row.FieldId] = row.ComboBox.HasSelection ? row.ComboBox.SelectedItem ?? string.Empty : string.Empty;
                } else if (row.CheckBox != null) {
                    materialSettings.FieldValues[row.FieldId] = row.CheckBox.IsChecked ? "true" : "false";
                }
            }

            if (saveToDisk) {
                SaveCurrentMaterialState();
            }
        }

        /// <summary>
        /// Updates status text and schema label for the active platform.
        /// </summary>
        void UpdateDisplayedValues() {
            PlatformMaterialSchemaDefinition materialSchema = FindActiveSchema();
            StatusText.Text = string.Concat("Status: Editing ", CurrentPlatformId);
            if (materialSchema == null) {
                StatusText.Text = "Status: Active platform has no material schema.";
            }
        }

        /// <summary>
        /// Ensures the active platform points at one valid schema and updates the schema combo-box selection.
        /// </summary>
        /// <returns>Resolved active schema or null when no schema is available.</returns>
        PlatformMaterialSchemaDefinition EnsureActiveSchema() {
            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            PlatformMaterialSchemaDefinition[] materialSchemas = ResolveAvailableSchemas();
            if (materialSettings == null) {
                UpdateSchemaPicker(materialSchemas, string.Empty);
                return null;
            }

            PlatformMaterialSchemaDefinition materialSchema = SchemaSettingsService.EnsureSelectedSchema(materialSettings, materialSchemas);
            string schemaId = materialSchema?.SchemaId ?? string.Empty;
            UpdateSchemaPicker(materialSchemas, schemaId);
            return materialSchema;
        }

        /// <summary>
        /// Finds the active material schema for the selected platform.
        /// </summary>
        /// <returns>Active schema or null when none was published.</returns>
        PlatformMaterialSchemaDefinition FindActiveSchema() {
            MaterialAssetProcessorSettings materialSettings = GetActiveMaterialSettings();
            string schemaId = materialSettings?.SchemaId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(schemaId)) {
                return null;
            }

            return FindSchemaDefinition(ResolveAvailableSchemas(), schemaId);
        }

        /// <summary>
        /// Resolves the material schemas published for the selected platform.
        /// </summary>
        /// <returns>Published material schemas for the selected platform.</returns>
        PlatformMaterialSchemaDefinition[] ResolveAvailableSchemas() {
            EditorPlatformBuildSelectionModel selectionModel = SelectionModelResolver?.Invoke(CurrentPlatformId);
            if (selectionModel == null || selectionModel.MaterialSchemas == null) {
                return Array.Empty<PlatformMaterialSchemaDefinition>();
            }

            return selectionModel.MaterialSchemas;
        }

        /// <summary>
        /// Updates the schema combo-box entries and current selection.
        /// </summary>
        /// <param name="materialSchemas">Schemas available to the selected platform.</param>
        /// <param name="selectedSchemaId">Schema identifier that should be selected.</param>
        void UpdateSchemaPicker(
            IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas,
            string selectedSchemaId) {
            AvailableSchemaIds.Clear();
            List<string> schemaDisplayNames = new List<string>(materialSchemas.Count);

            for (int index = 0; index < materialSchemas.Count; index++) {
                PlatformMaterialSchemaDefinition materialSchema = materialSchemas[index];
                AvailableSchemaIds.Add(materialSchema.SchemaId);
                schemaDisplayNames.Add(materialSchema.DisplayName);
            }

            IsUpdatingSchemaSelection = true;
            SchemaComboBox.SetItems(schemaDisplayNames, FindSchemaIndex(selectedSchemaId));
            IsUpdatingSchemaSelection = false;
            SchemaComboBox.IsOpen = false;
        }

        /// <summary>
        /// Finds the selected schema index inside the active schema picker list.
        /// </summary>
        /// <param name="schemaId">Schema identifier to locate.</param>
        /// <returns>Matching picker index or -1 when the identifier is unavailable.</returns>
        int FindSchemaIndex(string schemaId) {
            if (string.IsNullOrWhiteSpace(schemaId)) {
                return AvailableSchemaIds.Count > 0 ? 0 : -1;
            }

            for (int index = 0; index < AvailableSchemaIds.Count; index++) {
                if (string.Equals(AvailableSchemaIds[index], schemaId, StringComparison.OrdinalIgnoreCase)) {
                    return index;
                }
            }

            return AvailableSchemaIds.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Finds one schema definition by identifier.
        /// </summary>
        /// <param name="materialSchemas">Schemas to search.</param>
        /// <param name="schemaId">Schema identifier to locate.</param>
        /// <returns>Matching schema or null when no schema matches the identifier.</returns>
        PlatformMaterialSchemaDefinition FindSchemaDefinition(
            IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas,
            string schemaId) {
            if (materialSchemas == null) {
                throw new ArgumentNullException(nameof(materialSchemas));
            } else if (string.IsNullOrWhiteSpace(schemaId)) {
                return null;
            }

            for (int index = 0; index < materialSchemas.Count; index++) {
                PlatformMaterialSchemaDefinition materialSchema = materialSchemas[index];
                if (string.Equals(materialSchema.SchemaId, schemaId, StringComparison.OrdinalIgnoreCase)) {
                    return materialSchema;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds one field definition inside a material schema.
        /// </summary>
        /// <param name="materialSchema">Schema whose fields should be searched.</param>
        /// <param name="fieldId">Field identifier to locate.</param>
        /// <returns>Matching field definition.</returns>
        PlatformMaterialFieldDefinition FindFieldDefinition(PlatformMaterialSchemaDefinition materialSchema, string fieldId) {
            if (materialSchema == null) {
                throw new ArgumentNullException(nameof(materialSchema));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            for (int index = 0; index < materialSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = materialSchema.Fields[index];
                if (string.Equals(field.FieldId, fieldId, StringComparison.OrdinalIgnoreCase)) {
                    return field;
                }
            }

            throw new InvalidOperationException($"Field '{fieldId}' was not found on schema '{materialSchema.SchemaId}'.");
        }

        /// <summary>
        /// Resolves the active platform material settings payload.
        /// </summary>
        /// <returns>Material settings for the active platform, or null when unavailable.</returns>
        MaterialAssetProcessorSettings GetActiveMaterialSettings() {
            if (CurrentSettings == null || CurrentSettings.Processor == null || CurrentSettings.Processor.Platforms == null) {
                return null;
            }

            AssetPlatformProcessorSettings platformSettings;
            if (!CurrentSettings.Processor.Platforms.TryGetValue(CurrentPlatformId, out platformSettings) || platformSettings == null) {
                return null;
            }

            return platformSettings.Material;
        }

        /// <summary>
        /// Resolves the serialized value that should initialize one field editor.
        /// </summary>
        /// <param name="materialSettings">Active platform material settings payload.</param>
        /// <param name="field">Builder-defined field being resolved.</param>
        /// <returns>Serialized field value.</returns>
        string ResolveFieldValue(MaterialAssetProcessorSettings materialSettings, PlatformMaterialFieldDefinition field) {
            if (materialSettings.FieldValues != null && materialSettings.FieldValues.TryGetValue(field.FieldId, out string value)) {
                return value ?? string.Empty;
            }

            return field.DefaultValue ?? string.Empty;
        }

        /// <summary>
        /// Applies one serialized field value to the corresponding editor control.
        /// </summary>
        /// <param name="row">Editor row whose control should be updated.</param>
        /// <param name="field">Field definition that owns the control.</param>
        /// <param name="value">Serialized field value to display.</param>
        void ApplyFieldValueToControl(MaterialAssetFieldEditorRow row, PlatformMaterialFieldDefinition field, string value) {
            if (row.TextBox != null) {
                row.TextBox.Text = value ?? string.Empty;
                return;
            }

            if (row.ComboBox != null) {
                int selectedIndex = FindAllowedValueIndex(field.AllowedValues, value);
                row.ComboBox.SetItems(field.AllowedValues, selectedIndex);
                return;
            }

            if (row.CheckBox != null) {
                row.CheckBox.IsChecked = ParseBooleanValue(value);
            }
        }

        /// <summary>
        /// Finds the selected index of one serialized choice value.
        /// </summary>
        /// <param name="allowedValues">Allowed choice values defined by the schema.</param>
        /// <param name="value">Serialized value to locate.</param>
        /// <returns>Matching choice index or zero when the value is unavailable.</returns>
        int FindAllowedValueIndex(string[] allowedValues, string value) {
            if (allowedValues == null || allowedValues.Length == 0) {
                return -1;
            }

            for (int index = 0; index < allowedValues.Length; index++) {
                if (string.Equals(allowedValues[index], value, StringComparison.OrdinalIgnoreCase)) {
                    return index;
                }
            }

            return 0;
        }

        /// <summary>
        /// Parses one serialized boolean value.
        /// </summary>
        /// <param name="value">Serialized value to parse.</param>
        /// <returns>Parsed boolean state.</returns>
        bool ParseBooleanValue(string value) {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        /// <summary>
        /// Populates the supported-platform list from the supplied project platform identifiers.
        /// </summary>
        /// <param name="supportedPlatforms">Supported project platform identifiers.</param>
        void SetSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            SupportedPlatformIds.Clear();
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                SupportedPlatformIds.Add(platformId);
            }
        }

        /// <summary>
        /// Resolves the platform id that should be selected initially.
        /// </summary>
        /// <param name="activePlatformId">Current project platform identifier.</param>
        /// <returns>Selected platform identifier guaranteed to exist in the view.</returns>
        string ResolveSelectedPlatformId(string activePlatformId) {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                if (string.Equals(SupportedPlatformIds[index], activePlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return SupportedPlatformIds[index];
                }
            }

            return SupportedPlatformIds[0];
        }

        /// <summary>
        /// Finds the combo-box index for one platform identifier.
        /// </summary>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching combo-box index or zero when the identifier is unavailable.</returns>
        int FindPlatformIndex(string platformId) {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                if (string.Equals(SupportedPlatformIds[index], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return index;
                }
            }

            return 0;
        }

        /// <summary>
        /// Creates one text-host entity and text component pair.
        /// </summary>
        /// <param name="layerMask">Layer mask applied to the host entity.</param>
        /// <param name="textComponent">Created text component.</param>
        /// <param name="text">Initial text content.</param>
        /// <returns>Host entity that owns the text component.</returns>
        EditorEntity CreateTextHost(ushort layerMask, out TextComponent textComponent, string text) {
            EditorEntity host = new EditorEntity();
            host.LayerMask = layerMask;

            textComponent = new TextComponent();
            textComponent.Font = Font;
            textComponent.Text = text;
            textComponent.Color = ThemeManager.Colors.InputForegroundPrimary;
            textComponent.RenderOrder2D = TextOrder;
            host.AddComponent(textComponent);
            return host;
        }

        /// <summary>
        /// Lays out one label and combo-box row.
        /// </summary>
        /// <param name="labelHost">Label host entity.</param>
        /// <param name="labelText">Label text component.</param>
        /// <param name="comboHost">Combo-box host entity.</param>
        /// <param name="labelWidth">Reserved label width.</param>
        /// <param name="comboWidth">Available combo-box width.</param>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        void LayoutLabelAndCombo(
            EditorEntity labelHost,
            TextComponent labelText,
            EditorEntity comboHost,
            int labelWidth,
            int comboWidth,
            int left,
            int top) {
            labelHost.Position = new float3(left, top, 0.2f);
            labelText.Size = new int2(labelWidth, RowHeight);

            int comboLeft = left + labelWidth + ControlSpacing;
            comboHost.Position = new float3(comboLeft, top, 0.2f);
        }

        /// <summary>
        /// Lays out one schema-driven field row.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="safeWidth">Available width in pixels.</param>
        /// <param name="labelWidth">Reserved label width.</param>
        void LayoutFieldRow(MaterialAssetFieldEditorRow row, int left, int top, int safeWidth, int labelWidth) {
            int buttonWidth = row.ButtonHost != null ? Math.Min(ButtonWidth, safeWidth) : 0;
            int valueWidth = Math.Max(0, safeWidth - labelWidth - (row.ButtonHost != null ? buttonWidth + (ControlSpacing * 2) : ControlSpacing));

            row.LabelHost.Position = new float3(left, top, 0.2f);
            row.LabelText.Size = new int2(labelWidth, RowHeight);

            int valueLeft = left + labelWidth + ControlSpacing;
            row.ValueHost.Position = new float3(valueLeft, top, 0.2f);

            if (row.TextBox != null) {
                row.TextBox.Size = new int2(valueWidth, RowHeight);
            } else if (row.ComboBox != null) {
                row.ComboBox.Size = new int2(valueWidth, RowHeight);
            } else if (row.CheckBox != null) {
                row.CheckBox.Size = new int2(RowHeight, RowHeight);
            }

            if (row.ButtonHost != null) {
                int buttonLeft = valueLeft + valueWidth + ControlSpacing;
                row.ButtonHost.Position = new float3(buttonLeft, top, 0.2f);
            }
        }

        /// <summary>
        /// Determines whether one field should expose the shader picker convenience button.
        /// </summary>
        /// <param name="field">Field definition to evaluate.</param>
        /// <returns>True when the field should expose the shader picker.</returns>
        bool IsShaderPickerField(PlatformMaterialFieldDefinition field) {
            return field.FieldKind == PlatformMaterialFieldKind.AssetReference &&
                string.Equals(field.FieldId, ShaderAssetIdFieldId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the entry represents a shader source file.
        /// </summary>
        /// <param name="entry">Asset entry to evaluate.</param>
        /// <returns>True when the entry is a shader source file.</returns>
        bool IsShaderEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                return false;
            }

            return string.Equals(entry.Extension, EditorFileTemplateRegistry.ShaderExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
