using System.Globalization;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Builds editable property rows for each component on a selected entity.
    /// </summary>
    public class ComponentPropertiesView {
        /// <summary>
        /// Height of header rows in pixels.
        /// </summary>
        const int HeaderHeight = 22;
        /// <summary>
        /// Height of one component title bar in pixels.
        /// </summary>
        const int SectionHeaderHeight = 26;
        /// <summary>
        /// Vertical spacing preserved between one component title bar and its first row.
        /// </summary>
        const int SectionHeaderSpacing = 6;
        /// <summary>
        /// Vertical spacing preserved between component sections.
        /// </summary>
        const int SectionSpacing = 10;
        /// <summary>
        /// Horizontal padding applied inside the body of each component section.
        /// </summary>
        const int SectionBodyPadding = 8;
        /// <summary>
        /// Padding applied before the title text inside one component section header.
        /// </summary>
        const int SectionHeaderPadding = 8;
        /// <summary>
        /// Width reserved for the fixed remove button in one component section header.
        /// </summary>
        const int SectionRemoveButtonWidth = 32;
        /// <summary>
        /// Height of property rows in pixels.
        /// </summary>
        const int RowHeight = 24;
        /// <summary>
        /// Spacing between rows in pixels.
        /// </summary>
        const int RowSpacing = 6;
        /// <summary>
        /// Width reserved for property labels.
        /// </summary>
        const int LabelWidth = 140;
        /// <summary>
        /// Height of text fields.
        /// </summary>
        const int FieldHeight = 22;
        /// <summary>
        /// Spacing between fields in vector rows.
        /// </summary>
        const int FieldSpacing = 6;
        /// <summary>
        /// Width of the material pick button.
        /// </summary>
        const int PickButtonWidth = 80;
        /// <summary>
        /// Height of the material pick button.
        /// </summary>
        const int PickButtonHeight = 22;
        /// <summary>
        /// Placeholder text for empty asset values.
        /// </summary>
        const string EmptyAssetLabel = "None";
        /// <summary>
        /// Placeholder text for assigned assets without a known label.
        /// </summary>
        const string AssignedAssetLabel = "Assigned";
        /// <summary>
        /// Extension used for material assets.
        /// </summary>
        const string MaterialExtension = ".helmat";
        /// <summary>
        /// Extension used for font assets.
        /// </summary>
        const string FontExtension = ".hefont";

        /// <summary>
        /// Font used for labels and values.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Content manager used to load serialized editor assets.
        /// </summary>
        readonly ContentManager AssetContentManager;
        /// <summary>
        /// Converts picked browser entries into stable scene asset references.
        /// </summary>
        readonly SceneAssetReferenceFactory AssetReferenceFactory;
        /// <summary>
        /// Resolves file-system model source files through the processed model cache.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;
        /// <summary>
        /// Root entity that hosts the component property rows.
        /// </summary>
        readonly EditorEntity RootEntity;
        /// <summary>
        /// Pool of inactive rows that can be reused.
        /// </summary>
        readonly List<ComponentPropertyRow> RowPool;
        /// <summary>
        /// Active rows currently displayed.
        /// </summary>
        readonly List<ComponentPropertyRow> ActiveRows;
        /// <summary>
        /// Pool of inactive component sections that can be reused.
        /// </summary>
        readonly List<ComponentSectionView> SectionPool;
        /// <summary>
        /// Active component sections currently displayed.
        /// </summary>
        readonly List<ComponentSectionView> ActiveSections;
        /// <summary>
        /// Map of vector text fields to their owning row.
        /// </summary>
        readonly Dictionary<TextBoxComponent, ComponentPropertyRow> VectorFieldRows;
        /// <summary>
        /// Map of scalar text fields to their owning row.
        /// </summary>
        readonly Dictionary<TextBoxComponent, ComponentPropertyRow> ScalarFieldRows;
        /// <summary>
        /// Tracks display labels for runtime models assigned via the picker.
        /// </summary>
        readonly Dictionary<RuntimeModel, string> ModelLabels;
        /// <summary>
        /// Tracks display labels for runtime materials assigned via the picker.
        /// </summary>
        readonly Dictionary<RuntimeMaterial, string> MaterialLabels;
        /// <summary>
        /// Tracks display labels for font assets assigned via the picker.
        /// </summary>
        readonly Dictionary<FontAsset, string> FontLabels;
        /// <summary>
        /// Render order used for label text.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Tracks the collapsed state currently chosen for visible components.
        /// </summary>
        readonly Dictionary<Component, bool> CollapsedStates;
        /// <summary>
        /// Tracks whether the view is updating text fields internally.
        /// </summary>
        bool IsSynchronizing;

        /// <summary>
        /// Raised when the user requests to remove one component section.
        /// </summary>
        public event Action<Component> RemoveRequested;

        /// <summary>
        /// Initializes a new component properties view.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        public ComponentPropertiesView(FontAsset font, ContentManager contentManager) : this(font, contentManager, null) { }

        /// <summary>
        /// Initializes a new component properties view with support for file-system model source resolution.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        public ComponentPropertiesView(FontAsset font, ContentManager contentManager, EditorFileSystemModelResolver fileSystemModelResolver) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            Font = font;
            AssetContentManager = contentManager;
            AssetReferenceFactory = new SceneAssetReferenceFactory();
            FileSystemModelResolver = fileSystemModelResolver;
            RootEntity = new EditorEntity();
            RootEntity.LayerMask = 0b1000000000000000;
            RootEntity.Position = float3.Zero;
            RootEntity.Enabled = false;

            RowPool = new List<ComponentPropertyRow>(16);
            ActiveRows = new List<ComponentPropertyRow>(16);
            SectionPool = new List<ComponentSectionView>(8);
            ActiveSections = new List<ComponentSectionView>(8);
            VectorFieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            ScalarFieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            ModelLabels = new Dictionary<RuntimeModel, string>();
            MaterialLabels = new Dictionary<RuntimeMaterial, string>();
            FontLabels = new Dictionary<FontAsset, string>();
            CollapsedStates = new Dictionary<Component, bool>();
            TextOrder = RenderOrder2D.PanelForeground;
        }

        /// <summary>
        /// Gets the root entity for the view.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets a value indicating whether the view is visible.
        /// </summary>
        public bool IsVisible => RootEntity.Enabled;

        /// <summary>
        /// Shows component properties for the specified entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        public void ShowComponents(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            ClearActiveRows();
            ClearActiveSections();
            if (entity.Components == null || entity.Components.Count == 0) {
                RootEntity.Enabled = false;
                return;
            }

            RootEntity.Enabled = true;

            for (int i = 0; i < entity.Components.Count; i++) {
                Component component = entity.Components[i];
                if (component == null) {
                    continue;
                }
                if (component is IEditorHiddenComponent) {
                    continue;
                }

                ComponentSectionView section = AcquireSection(component);
                AddPropertyRows(section, component);
                ActiveSections.Add(section);
            }

            if (ActiveSections.Count == 0) {
                RootEntity.Enabled = false;
            }
        }

        /// <summary>
        /// Hides the view and clears active rows.
        /// </summary>
        public void Hide() {
            ClearActiveRows();
            ClearActiveSections();
            RootEntity.Enabled = false;
        }

        /// <summary>
        /// Updates the layout of the rows using the provided bounds.
        /// </summary>
        /// <param name="left">Left offset relative to the parent panel.</param>
        /// <param name="top">Top offset relative to the parent panel.</param>
        /// <param name="maxWidth">Maximum width available for the rows.</param>
        public void UpdateLayout(int left, int top, int maxWidth) {
            if (!RootEntity.Enabled) {
                return;
            }

            RootEntity.Position = new float3(left, top, 0.2f);
            int width = Math.Max(0, maxWidth);
            int y = 0;
            for (int i = 0; i < ActiveSections.Count; i++) {
                ComponentSectionView section = ActiveSections[i];
                LayoutSectionHeader(section, width, y);
                y += SectionHeaderHeight;

                if (!section.IsCollapsed) {
                    y += SectionHeaderSpacing;
                    for (int rowIndex = 0; rowIndex < section.Rows.Count; rowIndex++) {
                        ComponentPropertyRow row = section.Rows[rowIndex];
                        int rowHeight = row.Kind == ComponentPropertyRowKind.Header ? HeaderHeight : RowHeight;
                        row.Entity.Enabled = true;
                        LayoutRow(row, width, y, rowHeight);
                        y += rowHeight + RowSpacing;
                    }
                } else {
                    SetSectionRowsEnabled(section, false);
                }

                y += SectionSpacing;
            }
        }

        /// <summary>
        /// Clears active rows and returns them to the pool.
        /// </summary>
        void ClearActiveRows() {
            for (int i = 0; i < ActiveRows.Count; i++) {
                ComponentPropertyRow row = ActiveRows[i];
                row.Entity.Enabled = false;
                row.TargetComponent = null;
                row.Property = null;
                RowPool.Add(row);
            }

            ActiveRows.Clear();
        }

        /// <summary>
        /// Clears active component sections and returns them to the pool.
        /// </summary>
        void ClearActiveSections() {
            for (int i = 0; i < ActiveSections.Count; i++) {
                ComponentSectionView section = ActiveSections[i];
                section.Root.Enabled = false;
                section.TargetComponent = null;
                section.Rows.Clear();
                section.IsCollapsed = false;
                SectionPool.Add(section);
            }

            ActiveSections.Clear();
        }

        /// <summary>
        /// Adds editable rows for the properties of a component.
        /// </summary>
        /// <param name="component">Component to inspect.</param>
        void AddPropertyRows(ComponentSectionView section, Component component) {
            PropertyInfo[] properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < properties.Length; i++) {
                PropertyInfo property = properties[i];
                if (!ShouldShowProperty(property)) {
                    continue;
                }

                Type propertyType = property.PropertyType;
                bool isEditable = property.CanWrite;

                if (propertyType == typeof(float3) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Vector3);
                    BindPropertyRow(row, component, property);
                    UpdateVectorRow(row);
                    section.Rows.Add(row);
                    ActiveRows.Add(row);
                    continue;
                }

                if (propertyType == typeof(RuntimeMaterial) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Material);
                    BindPropertyRow(row, component, property);
                    UpdateMaterialRow(row);
                    section.Rows.Add(row);
                    ActiveRows.Add(row);
                    continue;
                }

                if (propertyType == typeof(FontAsset) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Font);
                    BindPropertyRow(row, component, property);
                    UpdateFontRow(row);
                    section.Rows.Add(row);
                    ActiveRows.Add(row);
                    continue;
                }

                if (propertyType == typeof(RuntimeModel) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Model);
                    BindPropertyRow(row, component, property);
                    UpdateModelRow(row);
                    section.Rows.Add(row);
                    ActiveRows.Add(row);
                    continue;
                }

                if (IsEditableScalar(propertyType) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Scalar);
                    BindPropertyRow(row, component, property);
                    UpdateScalarRow(row);
                    section.Rows.Add(row);
                    ActiveRows.Add(row);
                    continue;
                }

                ComponentPropertyRow readOnly = AcquireRow(ComponentPropertyRowKind.ReadOnly);
                BindPropertyRow(readOnly, component, property);
                UpdateReadOnlyRow(readOnly);
                section.Rows.Add(readOnly);
                ActiveRows.Add(readOnly);
            }
        }

        /// <summary>
        /// Associates a row with a component property and resets the label text.
        /// </summary>
        /// <param name="row">Row to bind.</param>
        /// <param name="component">Component owning the property.</param>
        /// <param name="property">Property metadata.</param>
        void BindPropertyRow(ComponentPropertyRow row, Component component, PropertyInfo property) {
            row.TargetComponent = component;
            row.Property = property;
            row.Label.Text = property.Name;
            row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            row.Entity.Enabled = true;
        }

        /// <summary>
        /// Updates a Vector3 row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateVectorRow(ComponentPropertyRow row) {
            if (!TryGetVectorValue(row, out float3 value)) {
                SetVectorFields(row, 0.0, 0.0, 0.0);
                return;
            }

            SetVectorFields(row, value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Updates a scalar row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateScalarRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            string text = FormatScalarValue(rawValue);
            UpdateScalarField(row, text);
        }

        /// <summary>
        /// Updates a read-only row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateReadOnlyRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            row.ValueText.Text = rawValue == null ? string.Empty : rawValue.ToString();
        }

        /// <summary>
        /// Updates a material row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateMaterialRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            if (rawValue is RuntimeMaterial material) {
                if (MaterialLabels.TryGetValue(material, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                row.ValueText.Text = string.IsNullOrWhiteSpace(material.Id) ? EmptyAssetLabel : material.Id;
            } else {
                row.ValueText.Text = EmptyAssetLabel;
            }
        }

        /// <summary>
        /// Updates a font row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateFontRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            if (rawValue is FontAsset font) {
                if (FontLabels.TryGetValue(font, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                row.ValueText.Text = string.IsNullOrWhiteSpace(font.FontInfo?.Name) ? EmptyAssetLabel : font.FontInfo.Name;
            } else {
                row.ValueText.Text = EmptyAssetLabel;
            }
        }

        /// <summary>
        /// Updates a model row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateModelRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            if (rawValue is RuntimeModel model) {
                if (ModelLabels.TryGetValue(model, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                row.ValueText.Text = string.IsNullOrWhiteSpace(model.Id) ? AssignedAssetLabel : model.Id;
                return;
            }

            row.ValueText.Text = EmptyAssetLabel;
        }

        /// <summary>
        /// Determines whether a property should be shown in the list.
        /// </summary>
        /// <param name="property">Property metadata.</param>
        /// <returns>True when the property is eligible for display.</returns>
        bool ShouldShowProperty(PropertyInfo property) {
            if (property == null) {
                return false;
            }

            if (property.GetIndexParameters().Length > 0) {
                return false;
            }

            if (!property.CanRead) {
                return false;
            }

            if (string.Equals(property.Name, "Parent", StringComparison.Ordinal)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a property type should be edited as a scalar text field.
        /// </summary>
        /// <param name="propertyType">Property type to test.</param>
        /// <returns>True when the type should use a scalar field.</returns>
        bool IsEditableScalar(Type propertyType) {
            if (propertyType == typeof(string)) {
                return true;
            }
            if (propertyType == typeof(int) ||
                propertyType == typeof(float) ||
                propertyType == typeof(double) ||
                propertyType == typeof(byte) ||
                propertyType == typeof(short) ||
                propertyType == typeof(long) ||
                propertyType == typeof(uint) ||
                propertyType == typeof(ulong) ||
                propertyType == typeof(ushort) ||
                propertyType == typeof(sbyte) ||
                propertyType == typeof(bool)) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retrieves the current property value from a row.
        /// </summary>
        /// <param name="row">Row to query.</param>
        /// <returns>Property value or null.</returns>
        object GetPropertyValue(ComponentPropertyRow row) {
            if (row.TargetComponent == null || row.Property == null) {
                return null;
            }

            return row.Property.GetValue(row.TargetComponent);
        }

        /// <summary>
        /// Attempts to read a Vector3 value from the row property.
        /// </summary>
        /// <param name="row">Row to query.</param>
        /// <param name="value">Vector value when available.</param>
        /// <returns>True when a Vector3 value was read.</returns>
        bool TryGetVectorValue(ComponentPropertyRow row, out float3 value) {
            value = float3.Zero;
            object rawValue = GetPropertyValue(row);
            if (rawValue is float3 vector) {
                value = vector;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the scalar field text and cache for a row.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="text">Text to apply.</param>
        void UpdateScalarField(ComponentPropertyRow row, string text) {
            if (row.ScalarField == null) {
                return;
            }

            IsSynchronizing = true;
            row.ScalarField.Text = text ?? string.Empty;
            row.ScalarCache = text ?? string.Empty;
            IsSynchronizing = false;
        }

        /// <summary>
        /// Applies vector field text to the row and cache.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="x">X value.</param>
        /// <param name="y">Y value.</param>
        /// <param name="z">Z value.</param>
        void SetVectorFields(ComponentPropertyRow row, double x, double y, double z) {
            if (row.VectorFields == null || row.VectorCache == null) {
                return;
            }

            string xText = FormatDouble(x);
            string yText = FormatDouble(y);
            string zText = FormatDouble(z);

            IsSynchronizing = true;
            row.VectorFields[0].Text = xText;
            row.VectorFields[1].Text = yText;
            row.VectorFields[2].Text = zText;
            row.VectorCache[0] = xText;
            row.VectorCache[1] = yText;
            row.VectorCache[2] = zText;
            IsSynchronizing = false;
        }

        /// <summary>
        /// Formats a scalar value for display.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted text.</returns>
        string FormatScalarValue(object value) {
            if (value == null) {
                return string.Empty;
            }

            if (value is float floatValue) {
                return FormatDouble(floatValue);
            }
            if (value is double doubleValue) {
                return FormatDouble(doubleValue);
            }
            if (value is bool boolValue) {
                return boolValue ? "True" : "False";
            }
            if (value is IFormattable formattable) {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        /// <summary>
        /// Formats a double value using invariant culture.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted string.</returns>
        string FormatDouble(double value) {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Attempts to parse a Vector3 from the row fields.
        /// </summary>
        /// <param name="row">Row to parse.</param>
        /// <param name="x">Parsed X value.</param>
        /// <param name="y">Parsed Y value.</param>
        /// <param name="z">Parsed Z value.</param>
        /// <returns>True when all fields parse successfully.</returns>
        bool TryReadVector(ComponentPropertyRow row, out double x, out double y, out double z) {
            x = 0.0;
            y = 0.0;
            z = 0.0;

            if (row.VectorFields == null || row.VectorFields.Length < 3) {
                return false;
            }

            if (!TryReadNumber(row.VectorFields[0].Text, out x)) {
                return false;
            }
            if (!TryReadNumber(row.VectorFields[1].Text, out y)) {
                return false;
            }
            if (!TryReadNumber(row.VectorFields[2].Text, out z)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a numeric value using invariant culture.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="value">Parsed numeric value.</param>
        /// <returns>True when parsing succeeds.</returns>
        bool TryReadNumber(string text, out double value) {
            if (string.IsNullOrWhiteSpace(text)) {
                value = 0.0;
                return false;
            }

            return double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        /// Handles submit events for vector fields.
        /// </summary>
        /// <param name="field">Submitted text box.</param>
        void HandleVectorSubmitted(TextBoxComponent field) {
            if (IsSynchronizing) {
                return;
            }

            if (!VectorFieldRows.TryGetValue(field, out ComponentPropertyRow row)) {
                return;
            }

            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            if (!TryReadVector(row, out double x, out double y, out double z)) {
                return;
            }

            var value = new float3((float)x, (float)y, (float)z);
            if (GetPropertyValue(row) is float3 currentValue && currentValue == value) {
                SetVectorFields(row, x, y, z);
                return;
            }

            row.Property.SetValue(row.TargetComponent, value);
            SetVectorFields(row, x, y, z);
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Handles submit events for scalar fields.
        /// </summary>
        /// <param name="field">Submitted text box.</param>
        void HandleScalarSubmitted(TextBoxComponent field) {
            if (IsSynchronizing) {
                return;
            }

            if (!ScalarFieldRows.TryGetValue(field, out ComponentPropertyRow row)) {
                return;
            }

            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            Type targetType = row.Property.PropertyType;
            if (!TryParseScalar(field.Text, targetType, out object parsed)) {
                return;
            }

            object currentValue = GetPropertyValue(row);
            if (Equals(currentValue, parsed)) {
                UpdateScalarField(row, FormatScalarValue(parsed));
                return;
            }

            row.Property.SetValue(row.TargetComponent, parsed);
            UpdateScalarField(row, FormatScalarValue(parsed));
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Attempts to parse a scalar value from text.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="targetType">Target property type.</param>
        /// <param name="value">Parsed value when successful.</param>
        /// <returns>True when parsing succeeds.</returns>
        bool TryParseScalar(string text, Type targetType, out object value) {
            value = null;
            if (targetType == typeof(string)) {
                value = text ?? string.Empty;
                return true;
            }

            if (targetType == typeof(bool)) {
                bool boolValue;
                if (bool.TryParse(text, out boolValue)) {
                    value = boolValue;
                    return true;
                }
                return false;
            }

            double numeric;
            if (!TryReadNumber(text, out numeric)) {
                return false;
            }

            if (targetType == typeof(float)) {
                value = (float)numeric;
                return true;
            }
            if (targetType == typeof(double)) {
                value = numeric;
                return true;
            }
            if (targetType == typeof(int)) {
                value = (int)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(long)) {
                value = (long)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(short)) {
                value = (short)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(byte)) {
                value = (byte)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(uint)) {
                value = (uint)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(ulong)) {
                value = (ulong)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(ushort)) {
                value = (ushort)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(sbyte)) {
                value = (sbyte)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Requests the asset picker for a material field.
        /// </summary>
        /// <param name="row">Material row to update.</param>
        void RequestMaterialPick(ComponentPropertyRow row) {
            EditorAssetPickerService.RequestPick(entry => HandleMaterialPicked(row, entry), MaterialExtension);
        }

        /// <summary>
        /// Requests the asset picker for a font field.
        /// </summary>
        /// <param name="row">Font row to update.</param>
        void RequestFontPick(ComponentPropertyRow row) {
            EditorAssetPickerService.RequestPick(entry => HandleFontPicked(row, entry), FontExtension);
        }

        /// <summary>
        /// Applies a picked material asset to the row property.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleMaterialPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            try {
                RuntimeMaterial material = LoadMaterial(entry);
                row.Property.SetValue(row.TargetComponent, material);
                if (entry != null && material != null) {
                    MaterialLabels[material] = entry.Name ?? string.Empty;
                }
                StorePickedAssetReference(row, entry);
                UpdateMaterialRow(row);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Material pick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a picked font asset to the row property.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleFontPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            try {
                FontAsset font = LoadFont(entry);
                row.Property.SetValue(row.TargetComponent, font);
                if (entry != null && font != null) {
                    FontLabels[font] = entry.Name ?? string.Empty;
                }
                StorePickedAssetReference(row, entry);
                UpdateFontRow(row);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Font pick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a runtime material from the selected asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to load.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial LoadMaterial(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsGenerated) {
                return GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(entry);
            }

            string extension = entry.Extension;
            if (!IsMaterialExtension(extension)) {
                throw new InvalidOperationException("Selected asset is not a material.");
            }

            MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                return null;
            }

            ShaderAsset shaderAsset = LoadShaderAsset(materialAsset.ShaderAssetId);
            return Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Loads a font asset from the selected asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to load.</param>
        /// <returns>Font asset instance.</returns>
        FontAsset LoadFont(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!IsFontExtension(entry.Extension)) {
                throw new InvalidOperationException("Selected asset is not a font.");
            }

            return AssetContentManager.Load<FontAsset>(entry.FullPath, RuntimeContentProcessorIds.FontAsset);
        }

        /// <summary>
        /// Loads a material asset from disk.
        /// </summary>
        /// <param name="path">Path to the material asset.</param>
        /// <returns>Material asset instance.</returns>
        MaterialAsset LoadMaterialAsset(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            }

            return AssetContentManager.Load<MaterialAsset>(path, EditorContentProcessorIds.MaterialAsset);
        }

        /// <summary>
        /// Loads a shader asset from the shader cache.
        /// </summary>
        /// <param name="shaderId">Shader asset identifier.</param>
        /// <returns>Shader asset instance.</returns>
        ShaderAsset LoadShaderAsset(string shaderId) {
            if (string.IsNullOrWhiteSpace(shaderId)) {
                throw new InvalidOperationException("Material does not specify a shader asset id.");
            }

            return EditorShaderPackageService.LoadShaderAsset(shaderId);
        }

        /// <summary>
        /// Determines whether an extension represents a material asset.
        /// </summary>
        /// <param name="extension">Extension to test.</param>
        /// <returns>True when the extension is material-like.</returns>
        bool IsMaterialExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            return string.Equals(extension, MaterialExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether an extension represents a font asset.
        /// </summary>
        /// <param name="extension">Extension to test.</param>
        /// <returns>True when the extension is font-like.</returns>
        bool IsFontExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            return string.Equals(extension, FontExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the vertical offset needed to center text using tight font metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container.</param>
        /// <param name="metrics">Tight font metrics.</param>
        /// <returns>Top offset for centered text.</returns>
        float GetTextTopOffset(int containerHeight, FontTightMetrics metrics) {
            double height = Math.Max(containerHeight, 1);
            double offset = (height - metrics.Height) * 0.5 - metrics.MinTop;
            return (float)Math.Round(offset);
        }

        /// <summary>
        /// Lays out a row using the provided width and height.
        /// </summary>
        /// <param name="row">Row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="top">Top offset for the row.</param>
        /// <param name="height">Row height.</param>
        void LayoutRow(ComponentPropertyRow row, int width, int top, int height) {
            int bodyWidth = Math.Max(0, width - SectionBodyPadding * 2);
            row.Entity.Position = new float3(SectionBodyPadding, top, 0.2f);
            int labelWidth = Math.Min(LabelWidth, bodyWidth);

            var labelMetrics = Font.MeasureTight(row.Label.Text ?? string.Empty);
            float labelY = GetTextTopOffset(height, labelMetrics);
            row.LabelHost.Position = new float3(0, labelY, 0.2f);
            row.Label.Size = new int2(labelWidth, (int)Math.Ceiling(labelMetrics.Height));

            switch (row.Kind) {
                case ComponentPropertyRowKind.Header:
                    LayoutHeaderRow(row, bodyWidth, height);
                    break;
                case ComponentPropertyRowKind.Vector3:
                    LayoutVectorRow(row, bodyWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Material:
                    LayoutMaterialRow(row, bodyWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Font:
                    LayoutMaterialRow(row, bodyWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Model:
                    LayoutMaterialRow(row, bodyWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Scalar:
                    LayoutScalarRow(row, bodyWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.ReadOnly:
                    LayoutReadOnlyRow(row, bodyWidth, height, labelWidth);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Layouts a header row to span the full width.
        /// </summary>
        /// <param name="row">Header row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        void LayoutHeaderRow(ComponentPropertyRow row, int width, int height) {
            row.Label.Size = new int2(Math.Max(0, width), row.Label.Size.Y);
        }

        /// <summary>
        /// Updates the placement of one component section title bar.
        /// </summary>
        /// <param name="section">Section whose title bar should be laid out.</param>
        /// <param name="width">Available content width.</param>
        /// <param name="top">Top offset for the section title bar.</param>
        void LayoutSectionHeader(ComponentSectionView section, int width, int top) {
            int safeWidth = Math.Max(1, width);
            section.Root.Enabled = true;
            section.Root.Position = new float3(0f, top, 0.2f);
            section.Background.Size = new int2(safeWidth, SectionHeaderHeight);
            section.HeaderInteractable.Size = new int2(safeWidth, SectionHeaderHeight);

            FontTightMetrics titleMetrics = Font.MeasureTight(section.TitleText.Text ?? string.Empty);
            float titleY = GetTextTopOffset(SectionHeaderHeight, titleMetrics);
            section.TitleHost.Position = new float3(SectionHeaderPadding, titleY, 0.2f);

            int titleWidth = Math.Max(1, safeWidth - SectionHeaderPadding - SectionRemoveButtonWidth - SectionHeaderPadding);
            section.TitleText.Size = new int2(titleWidth, Math.Max(1, (int)Math.Ceiling(Math.Max(titleMetrics.Height, Font.LineHeight))));

            int removeButtonX = Math.Max(0, safeWidth - SectionRemoveButtonWidth);
            section.RemoveButtonHost.Position = new float3(removeButtonX, 0f, 0.2f);
        }

        /// <summary>
        /// Layouts a Vector3 row with three text fields.
        /// </summary>
        /// <param name="row">Vector3 row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutVectorRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.VectorFieldHosts == null || row.VectorFields == null) {
                return;
            }

            int available = Math.Max(0, width - labelWidth - (FieldSpacing * 2));
            int fieldWidth = Math.Max(48, available / 3);
            float fieldY = (float)Math.Round((height - FieldHeight) * 0.5);

            int fieldX = labelWidth + FieldSpacing;
            for (int i = 0; i < row.VectorFieldHosts.Length; i++) {
                row.VectorFieldHosts[i].Position = new float3(fieldX, fieldY, 0.2f);
                row.VectorFields[i].Size = new int2(fieldWidth, FieldHeight);
                fieldX += fieldWidth + FieldSpacing;
            }
        }

        /// <summary>
        /// Layouts a material row with a value label and pick button.
        /// </summary>
        /// <param name="row">Material row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutMaterialRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ValueHost == null || row.ValueText == null || row.ActionButtonHost == null) {
                return;
            }

            int buttonWidth = PickButtonWidth;
            int valueWidth = Math.Max(0, width - labelWidth - FieldSpacing - buttonWidth);
            var valueMetrics = Font.MeasureTight(row.ValueText.Text ?? string.Empty);
            float valueY = GetTextTopOffset(height, valueMetrics);
            row.ValueHost.Position = new float3(labelWidth + FieldSpacing, valueY, 0.2f);
            row.ValueText.Size = new int2(valueWidth, (int)Math.Ceiling(valueMetrics.Height));

            float buttonY = (float)Math.Round((height - PickButtonHeight) * 0.5);
            row.ActionButtonHost.Position = new float3(width - buttonWidth, buttonY, 0.2f);
        }

        /// <summary>
        /// Layouts a scalar row with a single text field.
        /// </summary>
        /// <param name="row">Scalar row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutScalarRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ScalarField == null) {
                return;
            }

            int fieldWidth = Math.Max(48, width - labelWidth - FieldSpacing);
            float fieldY = (float)Math.Round((height - FieldHeight) * 0.5);
            row.ScalarField.Parent.Position = new float3(labelWidth + FieldSpacing, fieldY, 0.2f);
            row.ScalarField.Size = new int2(fieldWidth, FieldHeight);
        }

        /// <summary>
        /// Layouts a read-only row with a value label.
        /// </summary>
        /// <param name="row">Read-only row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutReadOnlyRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ValueHost == null || row.ValueText == null) {
                return;
            }

            int valueWidth = Math.Max(0, width - labelWidth - FieldSpacing);
            var valueMetrics = Font.MeasureTight(row.ValueText.Text ?? string.Empty);
            float valueY = GetTextTopOffset(height, valueMetrics);
            row.ValueHost.Position = new float3(labelWidth + FieldSpacing, valueY, 0.2f);
            row.ValueText.Size = new int2(valueWidth, (int)Math.Ceiling(valueMetrics.Height));
        }

        /// <summary>
        /// Sets the enabled state for all rows owned by one component section.
        /// </summary>
        /// <param name="section">Section whose rows should be toggled.</param>
        /// <param name="enabled">True when the rows should be visible.</param>
        void SetSectionRowsEnabled(ComponentSectionView section, bool enabled) {
            for (int i = 0; i < section.Rows.Count; i++) {
                section.Rows[i].Entity.Enabled = enabled;
            }
        }

        /// <summary>
        /// Retrieves or creates a component section for the supplied component.
        /// </summary>
        /// <param name="component">Component that will own the section.</param>
        /// <returns>Prepared component section.</returns>
        ComponentSectionView AcquireSection(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            ComponentSectionView section;
            if (SectionPool.Count > 0) {
                int index = SectionPool.Count - 1;
                section = SectionPool[index];
                SectionPool.RemoveAt(index);
            } else {
                section = CreateSection();
            }

            section.TargetComponent = component;
            section.TitleText.Text = FormatComponentTitle(component.GetType().Name);
            section.IsCollapsed = CollapsedStates.TryGetValue(component, out bool isCollapsed) && isCollapsed;
            section.Root.Enabled = true;
            section.Rows.Clear();
            return section;
        }

        /// <summary>
        /// Creates one reusable component section with header chrome and remove button wiring.
        /// </summary>
        /// <returns>Prepared component section instance.</returns>
        ComponentSectionView CreateSection() {
            EditorEntity root = new EditorEntity();
            root.LayerMask = RootEntity.LayerMask;
            root.Position = float3.Zero;
            root.Enabled = false;
            RootEntity.AddChild(root);

            SpriteComponent background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = RenderOrder2D.PanelSurface,
                Size = new int2(1, SectionHeaderHeight)
            };
            root.AddComponent(background);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(1, SectionHeaderHeight),
                HoverCursor = PointerCursorKind.Hand
            };
            root.AddComponent(interactable);

            EditorEntity titleHost = new EditorEntity {
                LayerMask = RootEntity.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            root.AddChild(titleHost);

            TextComponent titleText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
                RenderOrder2D = TextOrder
            };
            titleHost.AddComponent(titleText);

            EditorEntity removeButtonHost = new EditorEntity {
                LayerMask = RootEntity.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            root.AddChild(removeButtonHost);

            ComponentSectionView section = null;
            section = new ComponentSectionView(
                root,
                background,
                interactable,
                titleHost,
                titleText,
                removeButtonHost,
                new ButtonComponent("X", new int2(SectionRemoveButtonWidth, SectionHeaderHeight), Font, () => HandleSectionRemoveClicked(section), 0f));

            removeButtonHost.AddComponent(section.RemoveButton);
            section.RemoveButton.SetRenderOrders(TextOrder, TextOrder);
            section.RemoveButton.UseHoverOnlyBackground();
            section.RemoveButton.UseSquareCorners();
            section.RemoveButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            UpdateSectionHeaderVisual(section);
            interactable.CursorEvent += (pos, delta, state) => HandleSectionHeaderCursor(section, pos, state);

            return section;
        }

        /// <summary>
        /// Handles header pointer interactions so one section can be collapsed or expanded.
        /// </summary>
        /// <param name="section">Section receiving the interaction.</param>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <param name="state">Current pointer state.</param>
        void HandleSectionHeaderCursor(ComponentSectionView section, int2 pos, PointerInteraction state) {
            if (section == null) {
                return;
            }

            if (state == PointerInteraction.Hover) {
                section.IsHeaderHovered = true;
                UpdateSectionHeaderVisual(section);
                return;
            }

            if (state == PointerInteraction.Leave) {
                section.IsHeaderHovered = false;
                UpdateSectionHeaderVisual(section);
                return;
            }

            if (state != PointerInteraction.Press) {
                return;
            }

            if (IsPointerOverSectionRemoveButton(pos, section.Background.Size.X)) {
                return;
            }

            section.IsCollapsed = !section.IsCollapsed;
            if (section.TargetComponent != null) {
                CollapsedStates[section.TargetComponent] = section.IsCollapsed;
            }
            SetSectionRowsEnabled(section, !section.IsCollapsed);
        }

        /// <summary>
        /// Raises one remove request for the supplied component section.
        /// </summary>
        /// <param name="section">Section whose component should be removed.</param>
        void HandleSectionRemoveClicked(ComponentSectionView section) {
            if (section == null || section.TargetComponent == null) {
                return;
            }

            if (RemoveRequested != null) {
                RemoveRequested(section.TargetComponent);
            }
        }

        /// <summary>
        /// Determines whether a pointer position overlaps the fixed remove button area.
        /// </summary>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <param name="headerWidth">Current width of the header.</param>
        /// <returns>True when the pointer overlaps the remove button area.</returns>
        bool IsPointerOverSectionRemoveButton(int2 pos, int headerWidth) {
            int removeButtonX = Math.Max(0, headerWidth - SectionRemoveButtonWidth);
            return pos.X >= removeButtonX &&
                   pos.X <= headerWidth &&
                   pos.Y >= 0 &&
                   pos.Y <= SectionHeaderHeight;
        }

        /// <summary>
        /// Updates one component section header to reflect the current hover state.
        /// </summary>
        /// <param name="section">Section whose header chrome should be refreshed.</param>
        void UpdateSectionHeaderVisual(ComponentSectionView section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }

            section.Background.Color = section.IsHeaderHovered
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.AccentSecondary;
            section.TitleText.Color = section.IsHeaderHovered
                ? ThemeManager.Colors.TextOnAccent
                : ThemeManager.Colors.InputForegroundPrimary;
        }

        /// <summary>
        /// Formats one component type name into a readable editor title.
        /// </summary>
        /// <param name="componentTypeName">Raw component type name.</param>
        /// <returns>Readable component title.</returns>
        string FormatComponentTitle(string componentTypeName) {
            if (string.IsNullOrWhiteSpace(componentTypeName)) {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(componentTypeName.Length + 8);
            for (int i = 0; i < componentTypeName.Length; i++) {
                char current = componentTypeName[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(componentTypeName[i - 1])) {
                    builder.Append(' ');
                }
                builder.Append(current);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Retrieves or creates a row of the requested kind.
        /// </summary>
        /// <param name="kind">Row layout kind.</param>
        /// <returns>Prepared row instance.</returns>
        ComponentPropertyRow AcquireRow(ComponentPropertyRowKind kind) {
            for (int i = 0; i < RowPool.Count; i++) {
                ComponentPropertyRow row = RowPool[i];
                if (row.Kind == kind) {
                    RowPool.RemoveAt(i);
                    return row;
                }
            }

            return CreateRow(kind);
        }

        /// <summary>
        /// Creates a new row of the requested kind.
        /// </summary>
        /// <param name="kind">Row layout kind.</param>
        /// <returns>New row instance.</returns>
        ComponentPropertyRow CreateRow(ComponentPropertyRowKind kind) {
            var rowEntity = new EditorEntity();
            rowEntity.LayerMask = RootEntity.LayerMask;
            rowEntity.Position = float3.Zero;
            RootEntity.AddChild(rowEntity);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = RootEntity.LayerMask;
            labelHost.Position = float3.Zero;
            rowEntity.AddChild(labelHost);

            var label = new TextComponent();
            label.Font = Font;
            label.Text = string.Empty;
            label.Color = ThemeManager.Colors.InputForegroundPrimary;
            label.Size = new int2(1, 1);
            label.RenderOrder2D = TextOrder;
            labelHost.AddComponent(label);

            var row = new ComponentPropertyRow(kind, rowEntity, labelHost, label);

            switch (kind) {
                case ComponentPropertyRowKind.Vector3:
                    BuildVectorRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Material:
                    BuildMaterialRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Font:
                    BuildFontRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Model:
                    BuildModelRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Scalar:
                    BuildScalarRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.ReadOnly:
                    BuildReadOnlyRow(row, rowEntity);
                    break;
                default:
                    break;
            }

            return row;
        }

        /// <summary>
        /// Builds the Vector3 field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildVectorRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            row.VectorFieldHosts = new EditorEntity[3];
            row.VectorFields = new TextBoxComponent[3];
            row.VectorCache = new string[3];

            string[] placeholders = new[] { "X", "Y", "Z" };
            for (int i = 0; i < row.VectorFieldHosts.Length; i++) {
                var fieldHost = new EditorEntity();
                fieldHost.LayerMask = RootEntity.LayerMask;
                fieldHost.Position = float3.Zero;
                rowEntity.AddChild(fieldHost);

                var field = new TextBoxComponent(new int2(60, FieldHeight), Font, placeholders[i]);
                field.Submitted += HandleVectorSubmitted;
                fieldHost.AddComponent(field);

                row.VectorFieldHosts[i] = fieldHost;
                row.VectorFields[i] = field;
                VectorFieldRows[field] = row;
            }
        }

        /// <summary>
        /// Builds the material field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildMaterialRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent("Pick", new int2(PickButtonWidth, PickButtonHeight), Font, () => RequestMaterialPick(row), 0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Builds the font field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildFontRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent("Pick", new int2(PickButtonWidth, PickButtonHeight), Font, () => RequestFontPick(row), 0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Builds the model field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildModelRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent("Pick", new int2(PickButtonWidth, PickButtonHeight), Font, () => RequestModelPick(row), 0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Requests the asset picker for a model field.
        /// </summary>
        /// <param name="row">Model row to update.</param>
        void RequestModelPick(ComponentPropertyRow row) {
            EditorAssetPickerService.RequestPick(entry => HandleModelPicked(row, entry));
        }

        /// <summary>
        /// Applies a picked model asset to the row property.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleModelPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            try {
                RuntimeModel model = LoadModel(entry);
                row.Property.SetValue(row.TargetComponent, model);
                if (entry != null) {
                    ModelLabels[model] = entry.Name ?? string.Empty;
                }
                StorePickedAssetReference(row, entry);
                UpdateModelRow(row);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Model pick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stores a stable scene asset reference for one picked asset-backed property row.
        /// </summary>
        /// <param name="row">Row whose property received the picked asset.</param>
        /// <param name="entry">Picked asset entry used to build the stable reference.</param>
        void StorePickedAssetReference(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            if (!TryGetEntitySaveComponent(row.TargetComponent, out EntitySaveComponent saveComponent)) {
                return;
            }

            SceneAssetReference assetReference = AssetReferenceFactory.CreateFromEntry(entry);
            saveComponent.SetAssetReference(row.TargetComponent, row.Property.Name, assetReference);
        }

        /// <summary>
        /// Attempts to read the hidden save component attached to the row's owning entity.
        /// </summary>
        /// <param name="component">Component whose owning entity should be inspected.</param>
        /// <param name="saveComponent">Hidden save component when one is attached.</param>
        /// <returns>True when the owning entity exposes one save component.</returns>
        bool TryGetEntitySaveComponent(Component component, out EntitySaveComponent saveComponent) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            saveComponent = null;
            Entity entity = component.Parent;
            if (entity == null || entity.Components == null) {
                return false;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent entitySaveComponent) {
                    saveComponent = entitySaveComponent;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Loads a runtime model from the selected asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to load.</param>
        /// <returns>Runtime model instance.</returns>
        RuntimeModel LoadModel(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsGenerated) {
                return GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
            }

            if (FileSystemModelResolver == null) {
                ModelAsset modelAsset = LoadModelAsset(entry.FullPath);
                return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            }

            return FileSystemModelResolver.ResolveRuntimeModel(entry.FullPath);
        }

        /// <summary>
        /// Loads a model asset from disk.
        /// </summary>
        /// <param name="path">Path to the model asset.</param>
        /// <returns>Model asset instance.</returns>
        ModelAsset LoadModelAsset(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Model path must be provided.", nameof(path));
            }

            return AssetContentManager.Load<ModelAsset>(path, EditorContentProcessorIds.ModelAsset);
        }


        /// <summary>
        /// Builds the scalar field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildScalarRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var fieldHost = new EditorEntity();
            fieldHost.LayerMask = RootEntity.LayerMask;
            fieldHost.Position = float3.Zero;
            rowEntity.AddChild(fieldHost);

            var field = new TextBoxComponent(new int2(120, FieldHeight), Font, string.Empty);
            field.Submitted += HandleScalarSubmitted;
            fieldHost.AddComponent(field);

            row.ScalarField = field;
            row.ScalarCache = string.Empty;
            ScalarFieldRows[field] = row;
        }

        /// <summary>
        /// Builds the read-only value text for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildReadOnlyRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = string.Empty;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
        }
    }
}
