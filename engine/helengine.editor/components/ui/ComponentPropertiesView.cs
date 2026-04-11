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
        /// Render order used for label text.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Tracks whether the view is updating text fields internally.
        /// </summary>
        bool IsSynchronizing;

        /// <summary>
        /// Initializes a new component properties view.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        public ComponentPropertiesView(FontAsset font, ContentManager contentManager) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            Font = font;
            AssetContentManager = contentManager;
            AssetReferenceFactory = new SceneAssetReferenceFactory();
            RootEntity = new EditorEntity();
            RootEntity.LayerMask = 0b1000000000000000;
            RootEntity.Position = float3.Zero;
            RootEntity.Enabled = false;

            RowPool = new List<ComponentPropertyRow>(16);
            ActiveRows = new List<ComponentPropertyRow>(16);
            VectorFieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            ScalarFieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            ModelLabels = new Dictionary<RuntimeModel, string>();
            MaterialLabels = new Dictionary<RuntimeMaterial, string>();
            TextOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);
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

                AddHeaderRow(component.GetType().Name);
                AddPropertyRows(component);
            }

            if (ActiveRows.Count == 0) {
                RootEntity.Enabled = false;
            }
        }

        /// <summary>
        /// Hides the view and clears active rows.
        /// </summary>
        public void Hide() {
            ClearActiveRows();
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
            for (int i = 0; i < ActiveRows.Count; i++) {
                ComponentPropertyRow row = ActiveRows[i];
                int rowHeight = row.Kind == ComponentPropertyRowKind.Header ? HeaderHeight : RowHeight;
                LayoutRow(row, width, y, rowHeight);
                y += rowHeight + RowSpacing;
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
        /// Adds a header row for a component section.
        /// </summary>
        /// <param name="title">Component name to display.</param>
        void AddHeaderRow(string title) {
            ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Header);
            row.Label.Text = title ?? string.Empty;
            row.Label.Color = ThemeManager.Colors.InputForegroundSecondary;
            row.Entity.Enabled = true;
            ActiveRows.Add(row);
        }

        /// <summary>
        /// Adds editable rows for the properties of a component.
        /// </summary>
        /// <param name="component">Component to inspect.</param>
        void AddPropertyRows(Component component) {
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
                    ActiveRows.Add(row);
                    continue;
                }

                if (propertyType == typeof(RuntimeMaterial) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Material);
                    BindPropertyRow(row, component, property);
                    UpdateMaterialRow(row);
                    ActiveRows.Add(row);
                    continue;
                }

                if (propertyType == typeof(RuntimeModel) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Model);
                    BindPropertyRow(row, component, property);
                    UpdateModelRow(row);
                    ActiveRows.Add(row);
                    continue;
                }

                if (IsEditableScalar(propertyType) && isEditable) {
                    ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Scalar);
                    BindPropertyRow(row, component, property);
                    UpdateScalarRow(row);
                    ActiveRows.Add(row);
                    continue;
                }

                ComponentPropertyRow readOnly = AcquireRow(ComponentPropertyRowKind.ReadOnly);
                BindPropertyRow(readOnly, component, property);
                UpdateReadOnlyRow(readOnly);
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
            row.Entity.Position = new float3(0, top, 0.2f);
            int labelWidth = Math.Min(LabelWidth, width);

            var labelMetrics = Font.MeasureTight(row.Label.Text ?? string.Empty);
            float labelY = GetTextTopOffset(height, labelMetrics);
            row.LabelHost.Position = new float3(0, labelY, 0.2f);
            row.Label.Size = new int2(labelWidth, (int)Math.Ceiling(labelMetrics.Height));

            switch (row.Kind) {
                case ComponentPropertyRowKind.Header:
                    LayoutHeaderRow(row, width, height);
                    break;
                case ComponentPropertyRowKind.Vector3:
                    LayoutVectorRow(row, width, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Material:
                    LayoutMaterialRow(row, width, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Model:
                    LayoutMaterialRow(row, width, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Scalar:
                    LayoutScalarRow(row, width, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.ReadOnly:
                    LayoutReadOnlyRow(row, width, height, labelWidth);
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

            ModelAsset modelAsset = LoadModelAsset(entry.FullPath);
            return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
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
