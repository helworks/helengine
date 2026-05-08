using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Renders one builder-defined settings section with dynamic rows and per-setting controls.
    /// </summary>
    public sealed class EditorPlatformSettingsSection {
        /// <summary>
        /// Vertical spacing between the section title and the first row.
        /// </summary>
        const float TitleRowSpacing = 6f;

        /// <summary>
        /// Vertical spacing between settings rows.
        /// </summary>
        const float RowSpacing = 8f;

        /// <summary>
        /// Fixed row height used by text fields and combo boxes.
        /// </summary>
        internal const int RowHeight = 24;

        /// <summary>
        /// Fixed checkbox size used for boolean settings.
        /// </summary>
        internal static readonly int2 CheckBoxSize = new int2(18, 18);

        /// <summary>
        /// Root entity that owns the title and setting rows.
        /// </summary>
        readonly EditorEntity RootHost;

        /// <summary>
        /// Host entity for the section title text.
        /// </summary>
        readonly EditorEntity TitleHost;

        /// <summary>
        /// Section title text rendered above the settings rows.
        /// </summary>
        readonly TextComponent TitleText;

        /// <summary>
        /// Font used by labels and control text.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Render order used by background controls.
        /// </summary>
        readonly byte PanelOrder;

        /// <summary>
        /// Render order used by foreground text.
        /// </summary>
        readonly byte TextOrder;

        /// <summary>
        /// Label column width used by row layout.
        /// </summary>
        readonly int LabelColumnWidth;

        /// <summary>
        /// Value column width used by text fields and combo boxes.
        /// </summary>
        readonly int ValueColumnWidth;

        /// <summary>
        /// Builder-defined rows currently rendered in the section.
        /// </summary>
        readonly List<EditorPlatformSettingBinding> Rows;

        /// <summary>
        /// Gets the root entity that should be positioned by the dialog.
        /// </summary>
        public EditorEntity Root => RootHost;

        /// <summary>
        /// Gets the section title text component.
        /// </summary>
        public TextComponent Title => TitleText;

        /// <summary>
        /// Gets the currently rendered setting bindings.
        /// </summary>
        public IReadOnlyList<EditorPlatformSettingBinding> Items => Rows;

        /// <summary>
        /// Gets the total height consumed by the current rows, including the title and spacing.
        /// </summary>
        public float ContentHeight { get; private set; }

        /// <summary>
        /// Initializes one dynamic settings section under the supplied parent entity.
        /// </summary>
        /// <param name="parent">Parent dialog entity that owns the section.</param>
        /// <param name="layerMask">Layer mask used for all section entities.</param>
        /// <param name="font">Font used by row labels and values.</param>
        /// <param name="panelOrder">Render order used by surfaces.</param>
        /// <param name="textOrder">Render order used by text.</param>
        /// <param name="labelColumnWidth">Width reserved for row labels.</param>
        /// <param name="valueColumnWidth">Width reserved for row value controls.</param>
        public EditorPlatformSettingsSection(
            EditorEntity parent,
            ushort layerMask,
            FontAsset font,
            byte panelOrder,
            byte textOrder,
            int labelColumnWidth,
            int valueColumnWidth) {
            if (parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (labelColumnWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(labelColumnWidth), "Label column width must be positive.");
            }
            if (valueColumnWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(valueColumnWidth), "Value column width must be positive.");
            }

            Font = font;
            PanelOrder = panelOrder;
            TextOrder = textOrder;
            LabelColumnWidth = labelColumnWidth;
            ValueColumnWidth = valueColumnWidth;
            Rows = new List<EditorPlatformSettingBinding>();

            RootHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            RootHost.InitComponents();
            RootHost.InitChildren();
            parent.AddChild(RootHost);

            TitleHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            TitleHost.InitComponents();
            RootHost.AddChild(TitleHost);

            TitleText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(LabelColumnWidth + ValueColumnWidth + 12, EditorPlatformSettingsSection.RowHeight),
                RenderOrder2D = TextOrder
            };
            TitleHost.AddComponent(TitleText);
        }

        /// <summary>
        /// Rebuilds the section from the supplied builder metadata and persisted option values.
        /// </summary>
        /// <param name="titleText">Title shown above the section rows.</param>
        /// <param name="settings">Builder-defined settings to render.</param>
        /// <param name="values">Persisted option values keyed by setting id.</param>
        public void Rebuild(string titleText, PlatformSettingDefinition[] settings, Dictionary<string, string> values) {
            if (values == null) {
                throw new ArgumentNullException(nameof(values));
            }

            ClearRows();
            TitleText.Text = titleText ?? string.Empty;

            if (settings == null) {
                settings = Array.Empty<PlatformSettingDefinition>();
            }

            for (int index = 0; index < settings.Length; index++) {
                PlatformSettingDefinition setting = settings[index];
                if (setting == null) {
                    continue;
                }

                EditorPlatformSettingBinding binding = new EditorPlatformSettingBinding(
                    RootHost,
                    Font,
                    PanelOrder,
                    TextOrder,
                    LabelColumnWidth,
                    ValueColumnWidth,
                    setting,
                    values);
                Rows.Add(binding);
            }

            Layout();
        }

        /// <summary>
        /// Applies the section-local layout for the current row collection.
        /// </summary>
        public void Layout() {
            TitleHost.Position = new float3(0f, 0f, 0.1f);
            TitleText.Size = new int2(LabelColumnWidth + ValueColumnWidth + 12, EditorPlatformSettingsSection.RowHeight);

            float rowY = EditorPlatformSettingsSection.RowHeight + TitleRowSpacing;
            for (int index = 0; index < Rows.Count; index++) {
                EditorPlatformSettingBinding row = Rows[index];
                row.Layout(rowY, LabelColumnWidth, ValueColumnWidth);
                rowY += EditorPlatformSettingsSection.RowHeight + RowSpacing;
            }

            ContentHeight = rowY;
        }

        /// <summary>
        /// Validates the current row values against the builder metadata.
        /// </summary>
        /// <param name="errorMessage">Validation message when a required setting is missing.</param>
        /// <returns>True when all rows are valid; otherwise false.</returns>
        public bool TryValidate(out string errorMessage) {
            for (int index = 0; index < Rows.Count; index++) {
                if (!Rows[index].TryValidate(out errorMessage)) {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Removes all currently rendered rows from the root entity.
        /// </summary>
        void ClearRows() {
            for (int index = Rows.Count - 1; index >= 0; index--) {
                EditorPlatformSettingBinding row = Rows[index];
                row.RowHost.Dispose();
            }

            Rows.Clear();
        }
    }

    /// <summary>
    /// Binds one builder-defined setting definition to its rendered controls and persisted value entry.
    /// </summary>
    public sealed class EditorPlatformSettingBinding {
        /// <summary>
        /// Mutable value dictionary used by this row binding.
        /// </summary>
        readonly Dictionary<string, string> Values;

        /// <summary>
        /// Root entity that hosts the label and control for the setting.
        /// </summary>
        public EditorEntity RowHost { get; }

        /// <summary>
        /// Host entity for the row label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Row label text.
        /// </summary>
        public TextComponent LabelText { get; }

        /// <summary>
        /// Host entity for the row control.
        /// </summary>
        public EditorEntity ControlHost { get; }

        /// <summary>
        /// Text box control used for free-form text settings.
        /// </summary>
        public TextBoxComponent TextBox { get; }

        /// <summary>
        /// Checkbox control used for boolean settings.
        /// </summary>
        public CheckBoxComponent CheckBox { get; }

        /// <summary>
        /// Combo box control used for choice settings.
        /// </summary>
        public ComboBoxComponent ComboBox { get; }

        /// <summary>
        /// Gets the builder metadata for the row.
        /// </summary>
        public PlatformSettingDefinition Setting { get; }

        /// <summary>
        /// Initializes one row binding for a builder-defined setting.
        /// </summary>
        /// <param name="parent">Parent settings section entity.</param>
        /// <param name="font">Font used by labels and control text.</param>
        /// <param name="panelOrder">Render order used by background surfaces.</param>
        /// <param name="textOrder">Render order used by text.</param>
        /// <param name="labelColumnWidth">Width reserved for the row label.</param>
        /// <param name="valueColumnWidth">Width reserved for the row control.</param>
        /// <param name="setting">Builder-defined setting metadata.</param>
        /// <param name="values">Persisted option values keyed by setting id.</param>
        public EditorPlatformSettingBinding(
            EditorEntity parent,
            FontAsset font,
            byte panelOrder,
            byte textOrder,
            int labelColumnWidth,
            int valueColumnWidth,
            PlatformSettingDefinition setting,
            Dictionary<string, string> values) {
            if (parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (setting == null) {
                throw new ArgumentNullException(nameof(setting));
            }

            Setting = setting;
            Values = values ?? throw new ArgumentNullException(nameof(values));
            string value = ResolveInitialValue(setting, Values);
            Values[setting.SettingId] = value;

            RowHost = new EditorEntity {
                LayerMask = parent.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            RowHost.InitComponents();
            RowHost.InitChildren();
            parent.AddChild(RowHost);

            LabelHost = new EditorEntity {
                LayerMask = parent.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            LabelHost.InitComponents();
            RowHost.AddChild(LabelHost);

            LabelText = new TextComponent {
                Font = font,
                Text = setting.DisplayName,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(labelColumnWidth, EditorPlatformSettingsSection.RowHeight),
                RenderOrder2D = textOrder
            };
            LabelHost.AddComponent(LabelText);

            ControlHost = new EditorEntity {
                LayerMask = parent.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            ControlHost.InitComponents();
            RowHost.AddChild(ControlHost);

            switch (setting.SettingKind) {
                case PlatformSettingKind.Boolean:
                    CheckBox = new CheckBoxComponent(EditorPlatformSettingsSection.CheckBoxSize, font, ParseBoolean(value, ParseBoolean(setting.DefaultValue, false)));
                    CheckBox.SetRenderOrders(panelOrder, textOrder);
                    CheckBox.CheckedChanged += HandleCheckBoxChanged;
                    ControlHost.AddComponent(CheckBox);
                    break;
                case PlatformSettingKind.Choice:
                    ComboBox = new ComboBoxComponent(new int2(valueColumnWidth, EditorPlatformSettingsSection.RowHeight), font, setting.AllowedValues ?? Array.Empty<string>(), ResolveChoiceIndex(setting.AllowedValues, value, setting.DefaultValue));
                    ComboBox.UseModalPresentation();
                    ComboBox.SelectionChanged += HandleComboBoxSelectionChanged;
                    ControlHost.AddComponent(ComboBox);
                    break;
                case PlatformSettingKind.Text:
                default:
                    TextBox = new TextBoxComponent(new int2(valueColumnWidth, EditorPlatformSettingsSection.RowHeight), font, string.Empty);
                    TextBox.Text = value;
                    TextBox.SetRenderOrders(panelOrder, textOrder);
                    TextBox.TextChanged += HandleTextBoxChanged;
                    ControlHost.AddComponent(TextBox);
                    break;
            }
        }

        /// <summary>
        /// Updates the row-local layout for one section pass.
        /// </summary>
        /// <param name="rowTopY">Local Y position for the row host.</param>
        /// <param name="labelColumnWidth">Width reserved for the label column.</param>
        /// <param name="valueColumnWidth">Width reserved for the value column.</param>
        public void Layout(float rowTopY, int labelColumnWidth, int valueColumnWidth) {
            RowHost.Position = new float3(0f, rowTopY, 0.1f);
            LabelHost.Position = new float3(0f, 2f, 0.1f);
            LabelText.Size = new int2(labelColumnWidth, EditorPlatformSettingsSection.RowHeight);

            ControlHost.Position = new float3(labelColumnWidth + 12f, 0f, 0.1f);
            if (TextBox != null) {
                TextBox.Size = new int2(valueColumnWidth, EditorPlatformSettingsSection.RowHeight);
                return;
            }

            if (ComboBox != null) {
                ComboBox.Size = new int2(valueColumnWidth, EditorPlatformSettingsSection.RowHeight);
                return;
            }

            if (CheckBox != null) {
                ControlHost.Position = new float3(labelColumnWidth + 12f, 3f, 0.1f);
            }
        }

        /// <summary>
        /// Validates the current control value against the builder metadata.
        /// </summary>
        /// <param name="errorMessage">Validation error when the setting is missing a required value.</param>
        /// <returns>True when the row is valid; otherwise false.</returns>
        public bool TryValidate(out string errorMessage) {
            SyncValueFromControl();

            if (!Setting.Required) {
                errorMessage = string.Empty;
                return true;
            }

            switch (Setting.SettingKind) {
                case PlatformSettingKind.Boolean:
                    errorMessage = string.Empty;
                    return true;
                case PlatformSettingKind.Choice:
                    if (ComboBox != null && ComboBox.HasSelection) {
                        errorMessage = string.Empty;
                        return true;
                    }

                    errorMessage = $"Setting '{Setting.DisplayName}' requires a selected value.";
                    return false;
                case PlatformSettingKind.Text:
                default:
                    if (TextBox != null && !string.IsNullOrWhiteSpace(TextBox.Text)) {
                        errorMessage = string.Empty;
                        return true;
                    }

                    errorMessage = $"Setting '{Setting.DisplayName}' requires a value.";
                    return false;
            }
        }

        /// <summary>
        /// Synchronizes the persisted value dictionary from the currently rendered control state.
        /// </summary>
        void SyncValueFromControl() {
            if (CheckBox != null) {
                Values[Setting.SettingId] = CheckBox.IsChecked ? "true" : "false";
                return;
            }

            if (ComboBox != null) {
                Values[Setting.SettingId] = ComboBox.HasSelection ? ComboBox.SelectedItem : Setting.DefaultValue;
                return;
            }

            if (TextBox != null) {
                Values[Setting.SettingId] = TextBox.Text ?? Setting.DefaultValue;
            }
        }

        /// <summary>
        /// Handles text-box edits by writing the current value back into the persisted dictionary.
        /// </summary>
        /// <param name="textBox">Edited text box.</param>
        void HandleTextBoxChanged(TextBoxComponent textBox) {
            if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            Values[Setting.SettingId] = textBox.Text ?? Setting.DefaultValue;
        }

        /// <summary>
        /// Handles checkbox edits by writing the current value back into the persisted dictionary.
        /// </summary>
        /// <param name="checkBox">Edited checkbox.</param>
        /// <param name="isChecked">New checked state.</param>
        void HandleCheckBoxChanged(CheckBoxComponent checkBox, bool isChecked) {
            if (checkBox == null) {
                throw new ArgumentNullException(nameof(checkBox));
            }

            Values[Setting.SettingId] = isChecked.ToString();
        }

        /// <summary>
        /// Handles combo-box edits by writing the current value back into the persisted dictionary.
        /// </summary>
        /// <param name="index">Selected item index.</param>
        /// <param name="value">Selected item value.</param>
        void HandleComboBoxSelectionChanged(int index, string value) {
            if (index < 0) {
                Values[Setting.SettingId] = Setting.DefaultValue;
                return;
            }

            Values[Setting.SettingId] = value ?? Setting.DefaultValue;
        }

        /// <summary>
        /// Resolves the initial value for one setting from the persisted dictionary or the builder default.
        /// </summary>
        static string ResolveInitialValue(PlatformSettingDefinition setting, Dictionary<string, string> values) {
            if (setting == null) {
                throw new ArgumentNullException(nameof(setting));
            }
            if (values == null) {
                throw new ArgumentNullException(nameof(values));
            }

            string defaultValue = setting.DefaultValue ?? string.Empty;
            if (values.TryGetValue(setting.SettingId, out string storedValue) && !string.IsNullOrWhiteSpace(storedValue)) {
                if (setting.SettingKind == PlatformSettingKind.Choice && setting.AllowedValues.Length > 0) {
                    for (int index = 0; index < setting.AllowedValues.Length; index++) {
                        if (string.Equals(setting.AllowedValues[index], storedValue, StringComparison.OrdinalIgnoreCase)) {
                            return storedValue;
                        }
                    }

                    return setting.AllowedValues[0];
                }

                return storedValue;
            }

            if (setting.SettingKind == PlatformSettingKind.Choice && setting.AllowedValues.Length > 0) {
                for (int index = 0; index < setting.AllowedValues.Length; index++) {
                    if (string.Equals(setting.AllowedValues[index], defaultValue, StringComparison.OrdinalIgnoreCase)) {
                        return setting.AllowedValues[index];
                    }
                }

                return setting.AllowedValues[0];
            }

            return defaultValue;
        }

        /// <summary>
        /// Resolves one selected choice index from the persisted string value or the builder default.
        /// </summary>
        static int ResolveChoiceIndex(string[] allowedValues, string storedValue, string defaultValue) {
            if (allowedValues == null || allowedValues.Length == 0) {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(storedValue)) {
                for (int index = 0; index < allowedValues.Length; index++) {
                    if (string.Equals(allowedValues[index], storedValue, StringComparison.OrdinalIgnoreCase)) {
                        return index;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(defaultValue)) {
                for (int index = 0; index < allowedValues.Length; index++) {
                    if (string.Equals(allowedValues[index], defaultValue, StringComparison.OrdinalIgnoreCase)) {
                        return index;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Parses a boolean string with a fallback when parsing fails.
        /// </summary>
        static bool ParseBoolean(string text, bool fallback) {
            return bool.TryParse(text, out bool value) ? value : fallback;
        }
    }
}
