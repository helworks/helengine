namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to edit editor-global theme and UI scale preferences.
    /// </summary>
    public class EditorPreferencesDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 680;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 520;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Height reserved for each form label row.
        /// </summary>
        public const int LabelHeight = 18;

        /// <summary>
        /// Height reserved for each combo-box field.
        /// </summary>
        public const int FieldHeight = 24;

        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height reserved for the footer buttons.
        /// </summary>
        public const int FooterHeight = 28;

        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Width reserved for each combo-box field.
        /// </summary>
        const int FieldWidth = 320;

        /// <summary>
        /// Vertical spacing preserved between each label and its field.
        /// </summary>
        const int LabelFieldSpacing = 6;

        /// <summary>
        /// Fixed size used for the apply button.
        /// </summary>
        static readonly int2 ApplyButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Available scale-mode labels exposed by the dialog.
        /// </summary>
        static readonly string[] ScaleModeItems = new[] {
            "Auto",
            "Override"
        };

        /// <summary>
        /// Available explicit scale percentages exposed by the dialog.
        /// </summary>
        static readonly string[] ScalePercentItems = new[] {
            "75%",
            "100%",
            "125%",
            "150%",
            "175%",
            "200%"
        };

        /// <summary>
        /// Theme definitions exposed by the editor theme catalog.
        /// </summary>
        readonly EditorThemeDefinition[] ThemeDefinitions;

        /// <summary>
        /// Theme display names exposed by the editor theme catalog.
        /// </summary>
        readonly string[] ThemeItems;

        /// <summary>
        /// Host entity for the theme label.
        /// </summary>
        readonly EditorEntity ThemeLabelHost;

        /// <summary>
        /// Label that describes the theme selector.
        /// </summary>
        readonly TextComponent ThemeLabel;

        /// <summary>
        /// Host entity for the theme combo box.
        /// </summary>
        readonly EditorEntity ThemeComboBoxHost;

        /// <summary>
        /// Combo box used to choose the active editor theme.
        /// </summary>
        readonly ComboBoxComponent ThemeComboBox;

        /// <summary>
        /// Host entity for the scale-mode label.
        /// </summary>
        readonly EditorEntity ScaleModeLabelHost;

        /// <summary>
        /// Label that describes the scale-mode selector.
        /// </summary>
        readonly TextComponent ScaleModeLabel;

        /// <summary>
        /// Host entity for the scale-mode combo box.
        /// </summary>
        readonly EditorEntity ScaleModeComboBoxHost;

        /// <summary>
        /// Combo box used to choose between auto and override scaling.
        /// </summary>
        readonly ComboBoxComponent ScaleModeComboBox;

        /// <summary>
        /// Host entity for the explicit scale label.
        /// </summary>
        readonly EditorEntity ScalePercentLabelHost;

        /// <summary>
        /// Label that describes the explicit scale selector.
        /// </summary>
        readonly TextComponent ScalePercentLabel;

        /// <summary>
        /// Host entity for the explicit scale combo box.
        /// </summary>
        readonly EditorEntity ScalePercentComboBoxHost;

        /// <summary>
        /// Combo box used to choose one explicit override percentage.
        /// </summary>
        readonly ComboBoxComponent ScalePercentComboBox;

        /// <summary>
        /// Host entity for the apply button.
        /// </summary>
        readonly EditorEntity ApplyButtonHost;

        /// <summary>
        /// Button used to confirm the current preference selection.
        /// </summary>
        readonly ButtonComponent ApplyButton;

        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>
        /// Button used to dismiss the dialog without applying changes.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// Last settings document shown by the dialog.
        /// </summary>
        EditorPreferencesSettings CurrentSettings;

        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms one editor-global preferences selection.
        /// </summary>
        public event Action<EditorPreferencesSettings> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the preferences workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes a new editor preferences dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public EditorPreferencesDialog(FontAsset font, EditorUiMetrics metrics)
            : base("EditorPreferencesDialog", "Preferences", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            ThemeDefinitions = CreateThemeDefinitions();
            ThemeItems = CreateThemeItems(ThemeDefinitions);

            SetDialogMinimumSize(PanelWidth, PanelHeight);

            ThemeLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ThemeLabelHost);
            ThemeLabel = CreateDialogLabel("Theme");
            ThemeLabelHost.AddComponent(ThemeLabel);

            ThemeComboBoxHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ThemeComboBoxHost);
            ThemeComboBox = new ComboBoxComponent(GetFieldSize(), DialogFont, ThemeItems, ResolveThemeIndex(EditorThemeCatalog.DefaultThemeId));
            ConfigureDialogComboBox(ThemeComboBox);
            ThemeComboBoxHost.AddComponent(ThemeComboBox);

            ScaleModeLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ScaleModeLabelHost);
            ScaleModeLabel = CreateDialogLabel("Scale Mode");
            ScaleModeLabelHost.AddComponent(ScaleModeLabel);

            ScaleModeComboBoxHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ScaleModeComboBoxHost);
            ScaleModeComboBox = new ComboBoxComponent(GetFieldSize(), DialogFont, ScaleModeItems, 0);
            ConfigureDialogComboBox(ScaleModeComboBox);
            ScaleModeComboBox.SelectionChanged += HandleScaleModeSelectionChanged;
            ScaleModeComboBoxHost.AddComponent(ScaleModeComboBox);

            ScalePercentLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ScalePercentLabelHost);
            ScalePercentLabel = CreateDialogLabel("Scale Override");
            ScalePercentLabelHost.AddComponent(ScalePercentLabel);

            ScalePercentComboBoxHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ScalePercentComboBoxHost);
            ScalePercentComboBox = new ComboBoxComponent(GetFieldSize(), DialogFont, ScalePercentItems, 1);
            ConfigureDialogComboBox(ScalePercentComboBox);
            ScalePercentComboBoxHost.AddComponent(ScalePercentComboBox);

            ApplyButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ApplyButtonHost);
            ApplyButton = new ButtonComponent("Apply", GetApplyButtonSize(), DialogFont, HandleApplyClicked, 0f);
            ApplyButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            ApplyButtonHost.AddComponent(ApplyButton);

            CancelButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", GetCancelButtonSize(), DialogFont, HandleCancelClicked, 0f);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            CancelButtonHost.AddComponent(CancelButton);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the preferences dialog for the provided current settings.
        /// </summary>
        /// <param name="settings">Current editor-global preferences.</param>
        public void Show(EditorPreferencesSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            CurrentSettings = settings;
            ResetDialogPositioning();
            SetThemeSelection(settings.ThemeId);
            SetScaleModeSelection(settings.UiScale.Mode);
            SetScalePercentSelection(settings.UiScale.OverridePercent);
            UpdateScalePercentEnabled(settings.UiScale.Mode == EditorUiScaleMode.Override);
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the preferences dialog.
        /// </summary>
        public void Hide() {
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Updates dialog sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }
        }

        /// <summary>
        /// Raises the dialog cancel flow when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Repositions the dialog content whenever the shared modal shell position or size changes.
        /// </summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutContent();
        }

        /// <summary>
        /// Updates the positions of the labels, combo boxes, and footer buttons.
        /// </summary>
        void LayoutContent() {
            int contentWidth = Math.Max(0, AnchorBounds.X - GetPanelPaddingPixels() * 2);
            int themeLabelTop = GetPanelPaddingPixels() + GetHeaderHeightPixels() + GetSectionSpacingPixels();
            int themeComboTop = themeLabelTop + GetLabelHeightPixels() + GetLabelFieldSpacingPixels();
            int scaleModeLabelTop = themeComboTop + GetFieldHeightPixels() + GetSectionSpacingPixels();
            int scaleModeComboTop = scaleModeLabelTop + GetLabelHeightPixels() + GetLabelFieldSpacingPixels();
            int scalePercentLabelTop = scaleModeComboTop + GetFieldHeightPixels() + GetSectionSpacingPixels();
            int scalePercentComboTop = scalePercentLabelTop + GetLabelHeightPixels() + GetLabelFieldSpacingPixels();
            int footerTop = AnchorBounds.Y - GetPanelPaddingPixels() - GetFooterHeightPixels();
            int buttonY = footerTop + Math.Max(0, (GetFooterHeightPixels() - GetApplyButtonSize().Y) / 2);
            int applyButtonX = AnchorBounds.X - GetPanelPaddingPixels() - GetApplyButtonSize().X;
            int cancelButtonX = applyButtonX - GetFooterButtonSpacingPixels() - GetCancelButtonSize().X;

            ThemeLabelHost.Position = new float3(GetPanelPaddingPixels(), themeLabelTop, 0.2f);
            ThemeLabel.Size = new int2(contentWidth, GetLabelHeightPixels());

            ThemeComboBoxHost.Position = new float3(GetPanelPaddingPixels(), themeComboTop, 0.2f);
            ThemeComboBox.Size = GetFieldSize();

            ScaleModeLabelHost.Position = new float3(GetPanelPaddingPixels(), scaleModeLabelTop, 0.2f);
            ScaleModeLabel.Size = new int2(contentWidth, GetLabelHeightPixels());

            ScaleModeComboBoxHost.Position = new float3(GetPanelPaddingPixels(), scaleModeComboTop, 0.2f);
            ScaleModeComboBox.Size = GetFieldSize();

            ScalePercentLabelHost.Position = new float3(GetPanelPaddingPixels(), scalePercentLabelTop, 0.2f);
            ScalePercentLabel.Size = new int2(contentWidth, GetLabelHeightPixels());

            ScalePercentComboBoxHost.Position = new float3(GetPanelPaddingPixels(), scalePercentComboTop, 0.2f);
            ScalePercentComboBox.Size = GetFieldSize();

            CancelButtonHost.Position = new float3(cancelButtonX, buttonY, 0.2f);
            ApplyButtonHost.Position = new float3(applyButtonX, buttonY, 0.2f);
        }

        /// <summary>
        /// Handles one scale-mode selection change by updating explicit override visibility.
        /// </summary>
        /// <param name="selectedIndex">Selected combo-box index.</param>
        /// <param name="selectedItem">Selected combo-box item text.</param>
        void HandleScaleModeSelectionChanged(int selectedIndex, string selectedItem) {
            UpdateScalePercentEnabled(string.Equals(selectedItem, "Override", StringComparison.Ordinal));
        }

        /// <summary>
        /// Applies the selected preferences and raises the confirmation event.
        /// </summary>
        void HandleApplyClicked() {
            EditorUiScaleMode mode = ResolveSelectedMode();
            int percent = ResolveSelectedPercent();
            CurrentSettings = new EditorPreferencesSettings(
                new EditorUiScaleSettings(mode, percent),
                ResolveSelectedThemeId());
            Hide();
            if (ConfirmRequested != null) {
                ConfirmRequested(CurrentSettings);
            }
        }

        /// <summary>
        /// Cancels the dialog without applying changes.
        /// </summary>
        void HandleCancelClicked() {
            Hide();
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Updates the explicit scale controls to match whether override mode is active.
        /// </summary>
        /// <param name="enabled">True when override controls should remain interactive and visible.</param>
        void UpdateScalePercentEnabled(bool enabled) {
            ScalePercentLabelHost.Enabled = enabled;
            ScalePercentComboBoxHost.Enabled = enabled;
        }

        /// <summary>
        /// Selects the dialog item that matches the provided theme identifier.
        /// </summary>
        /// <param name="themeId">Persisted theme identifier that should appear selected.</param>
        void SetThemeSelection(string themeId) {
            ThemeComboBox.SetItems(ThemeItems, ResolveThemeIndex(themeId));
        }

        /// <summary>
        /// Selects the dialog item that matches the provided scale mode.
        /// </summary>
        /// <param name="mode">Scale mode that should appear selected.</param>
        void SetScaleModeSelection(EditorUiScaleMode mode) {
            int selectedIndex = mode == EditorUiScaleMode.Override ? 1 : 0;
            ScaleModeComboBox.SetItems(ScaleModeItems, selectedIndex);
        }

        /// <summary>
        /// Selects the dialog item that matches the provided explicit percent.
        /// </summary>
        /// <param name="percent">Explicit scale percentage that should appear selected.</param>
        void SetScalePercentSelection(int percent) {
            int selectedIndex = ResolvePercentIndex(percent);
            ScalePercentComboBox.SetItems(ScalePercentItems, selectedIndex);
        }

        /// <summary>
        /// Resolves the currently selected theme identifier from the combo-box value.
        /// </summary>
        /// <returns>Selected stable theme identifier.</returns>
        string ResolveSelectedThemeId() {
            string selectedValue = ThemeComboBox.SelectedItem;
            for (int index = 0; index < ThemeDefinitions.Length; index++) {
                EditorThemeDefinition theme = ThemeDefinitions[index];
                if (string.Equals(theme.DisplayName, selectedValue, StringComparison.Ordinal)) {
                    return theme.Id;
                }
            }

            throw new InvalidOperationException("The selected editor theme must resolve to one catalog entry.");
        }

        /// <summary>
        /// Resolves the currently selected scale mode from the combo-box value.
        /// </summary>
        /// <returns>Selected scale mode.</returns>
        EditorUiScaleMode ResolveSelectedMode() {
            return string.Equals(ScaleModeComboBox.SelectedItem, "Override", StringComparison.Ordinal)
                ? EditorUiScaleMode.Override
                : EditorUiScaleMode.Auto;
        }

        /// <summary>
        /// Resolves the currently selected explicit percentage from the combo-box value.
        /// </summary>
        /// <returns>Selected explicit scale percentage.</returns>
        int ResolveSelectedPercent() {
            string selectedValue = ScalePercentComboBox.SelectedItem;
            string numericValue = selectedValue.Replace("%", string.Empty, StringComparison.Ordinal);
            return int.Parse(numericValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Resolves the combo-box index that matches one persisted theme identifier.
        /// </summary>
        /// <param name="themeId">Persisted theme identifier.</param>
        /// <returns>Index of the matching combo-box item.</returns>
        int ResolveThemeIndex(string themeId) {
            for (int index = 0; index < ThemeDefinitions.Length; index++) {
                EditorThemeDefinition theme = ThemeDefinitions[index];
                if (string.Equals(theme.Id, themeId, StringComparison.Ordinal)) {
                    return index;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(themeId), "Theme identifier must match one supported editor theme.");
        }

        /// <summary>
        /// Resolves the combo-box index that matches one supported explicit percentage.
        /// </summary>
        /// <param name="percent">Supported explicit scale percentage.</param>
        /// <returns>Index of the matching combo-box item.</returns>
        int ResolvePercentIndex(int percent) {
            string selectedValue = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            for (int index = 0; index < ScalePercentItems.Length; index++) {
                if (string.Equals(ScalePercentItems[index], selectedValue, StringComparison.Ordinal)) {
                    return index;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(percent), "Scale percent must match one supported editor preference value.");
        }

        /// <summary>
        /// Creates one array copy of the currently registered editor theme definitions.
        /// </summary>
        /// <returns>Theme definitions exposed by the editor theme catalog.</returns>
        static EditorThemeDefinition[] CreateThemeDefinitions() {
            return EditorThemeCatalog.Themes.ToArray();
        }

        /// <summary>
        /// Creates one display-name array for the supplied theme definitions.
        /// </summary>
        /// <param name="themeDefinitions">Theme definitions that should be exposed by the theme combo box.</param>
        /// <returns>Display-name array aligned with the supplied theme definitions.</returns>
        static string[] CreateThemeItems(EditorThemeDefinition[] themeDefinitions) {
            if (themeDefinitions == null) {
                throw new ArgumentNullException(nameof(themeDefinitions));
            }

            string[] items = new string[themeDefinitions.Length];
            for (int index = 0; index < themeDefinitions.Length; index++) {
                items[index] = themeDefinitions[index].DisplayName;
            }

            return items;
        }

        /// <summary>
        /// Creates one shared dialog host entity.
        /// </summary>
        /// <returns>New host entity configured for dialog content.</returns>
        EditorEntity CreateDialogHost() {
            return new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Creates one shared dialog label component.
        /// </summary>
        /// <param name="text">Text shown by the label.</param>
        /// <returns>New text component configured for dialog labels.</returns>
        TextComponent CreateDialogLabel(string text) {
            return new TextComponent {
                Font = DialogFont,
                Text = text,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, GetLabelHeightPixels()),
                RenderOrder2D = DialogTextOrder
            };
        }

        /// <summary>
        /// Gets the scaled combo-box field size.
        /// </summary>
        /// <returns>Scaled combo-box field size.</returns>
        int2 GetFieldSize() {
            return new int2(
                DialogMetrics.ScalePixels(FieldWidth),
                GetFieldHeightPixels());
        }

        /// <summary>
        /// Gets the scaled apply-button size.
        /// </summary>
        /// <returns>Scaled apply-button size.</returns>
        int2 GetApplyButtonSize() {
            return new int2(
                DialogMetrics.ScalePixels(ApplyButtonSize.X),
                DialogMetrics.ScalePixels(ApplyButtonSize.Y));
        }

        /// <summary>
        /// Gets the scaled cancel-button size.
        /// </summary>
        /// <returns>Scaled cancel-button size.</returns>
        int2 GetCancelButtonSize() {
            return new int2(
                DialogMetrics.ScalePixels(CancelButtonSize.X),
                DialogMetrics.ScalePixels(CancelButtonSize.Y));
        }

        /// <summary>
        /// Gets the scaled panel padding.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled header height used by the dialog content layout.
        /// </summary>
        /// <returns>Scaled header height in pixels.</returns>
        int GetHeaderHeightPixels() {
            return DialogMetrics.ScalePixels(HeaderHeight);
        }

        /// <summary>
        /// Gets the scaled label height.
        /// </summary>
        /// <returns>Scaled label height in pixels.</returns>
        int GetLabelHeightPixels() {
            return DialogMetrics.ScalePixels(LabelHeight);
        }

        /// <summary>
        /// Gets the scaled field height.
        /// </summary>
        /// <returns>Scaled field height in pixels.</returns>
        int GetFieldHeightPixels() {
            return DialogMetrics.ScalePixels(FieldHeight);
        }

        /// <summary>
        /// Gets the scaled spacing between dialog sections.
        /// </summary>
        /// <returns>Scaled section spacing in pixels.</returns>
        int GetSectionSpacingPixels() {
            return DialogMetrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled footer height.
        /// </summary>
        /// <returns>Scaled footer height in pixels.</returns>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Gets the scaled spacing between one label and its field.
        /// </summary>
        /// <returns>Scaled label-to-field spacing in pixels.</returns>
        int GetLabelFieldSpacingPixels() {
            return DialogMetrics.ScalePixels(LabelFieldSpacing);
        }

        /// <summary>
        /// Gets the scaled spacing preserved between footer buttons.
        /// </summary>
        /// <returns>Scaled footer-button spacing in pixels.</returns>
        int GetFooterButtonSpacingPixels() {
            return DialogMetrics.ScalePixels(8);
        }
    }
}
