namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to edit scene-level authoring settings such as the shared canvas profile.
    /// </summary>
    public class SceneSettingsDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 520;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 248;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Height reserved for each form label row.
        /// </summary>
        public const int LabelHeight = 18;

        /// <summary>
        /// Height reserved for each text field.
        /// </summary>
        public const int FieldHeight = 24;

        /// <summary>
        /// Vertical spacing between stacked rows.
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
        /// Width reserved for the text fields.
        /// </summary>
        const int FieldWidth = 220;

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
        /// Host entity for the canvas-width label.
        /// </summary>
        readonly EditorEntity CanvasWidthLabelHost;

        /// <summary>
        /// Label that describes the canvas-width field.
        /// </summary>
        readonly TextComponent CanvasWidthLabel;

        /// <summary>
        /// Host entity for the canvas-width text field.
        /// </summary>
        readonly EditorEntity CanvasWidthFieldHost;

        /// <summary>
        /// Text field used to edit the logical canvas width.
        /// </summary>
        readonly TextBoxComponent CanvasWidthField;

        /// <summary>
        /// Host entity for the canvas-height label.
        /// </summary>
        readonly EditorEntity CanvasHeightLabelHost;

        /// <summary>
        /// Label that describes the canvas-height field.
        /// </summary>
        readonly TextComponent CanvasHeightLabel;

        /// <summary>
        /// Host entity for the canvas-height text field.
        /// </summary>
        readonly EditorEntity CanvasHeightFieldHost;

        /// <summary>
        /// Text field used to edit the logical canvas height.
        /// </summary>
        readonly TextBoxComponent CanvasHeightField;

        /// <summary>
        /// Host entity for the dont-unload label.
        /// </summary>
        readonly EditorEntity DontUnloadLabelHost;

        /// <summary>
        /// Label that describes the dont-unload checkbox.
        /// </summary>
        readonly TextComponent DontUnloadLabel;

        /// <summary>
        /// Host entity for the dont-unload checkbox.
        /// </summary>
        readonly EditorEntity DontUnloadCheckBoxHost;

        /// <summary>
        /// Checkbox used to control whether the scene survives normal single-scene transitions.
        /// </summary>
        readonly CheckBoxComponent DontUnloadCheckBox;

        /// <summary>
        /// Host entity for the status text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Status text used to show validation feedback.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Host entity for the apply button.
        /// </summary>
        readonly EditorEntity ApplyButtonHost;

        /// <summary>
        /// Button used to confirm the current scene settings.
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
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms a new scene settings payload.
        /// </summary>
        public event Action<SceneSettingsAsset> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the dialog.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes a new scene settings dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public SceneSettingsDialog(FontAsset font, EditorUiMetrics metrics)
            : base("SceneSettingsDialog", "Scene Settings", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            CanvasWidthLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CanvasWidthLabelHost);
            CanvasWidthLabel = CreateDialogLabel("Canvas Width");
            CanvasWidthLabelHost.AddComponent(CanvasWidthLabel);

            CanvasWidthFieldHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CanvasWidthFieldHost);
            CanvasWidthField = new TextBoxComponent(GetFieldSize(), DialogFont, "1280");
            CanvasWidthField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CanvasWidthFieldHost.AddComponent(CanvasWidthField);

            CanvasHeightLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CanvasHeightLabelHost);
            CanvasHeightLabel = CreateDialogLabel("Canvas Height");
            CanvasHeightLabelHost.AddComponent(CanvasHeightLabel);

            CanvasHeightFieldHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CanvasHeightFieldHost);
            CanvasHeightField = new TextBoxComponent(GetFieldSize(), DialogFont, "720");
            CanvasHeightField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CanvasHeightFieldHost.AddComponent(CanvasHeightField);

            DontUnloadLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(DontUnloadLabelHost);
            DontUnloadLabel = CreateDialogLabel("Dont Unload");
            DontUnloadLabelHost.AddComponent(DontUnloadLabel);

            DontUnloadCheckBoxHost = CreateDialogHost();
            DialogPanelRoot.AddChild(DontUnloadCheckBoxHost);
            DontUnloadCheckBox = new CheckBoxComponent(GetCheckBoxSize(), DialogFont);
            DontUnloadCheckBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            DontUnloadCheckBoxHost.AddComponent(DontUnloadCheckBox);

            StatusHost = CreateDialogHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, GetLabelHeightPixels()),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

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
        /// Shows the dialog using the supplied scene settings payload.
        /// </summary>
        /// <param name="sceneSettings">Scene settings that should populate the dialog fields.</param>
        public void Show(SceneSettingsAsset sceneSettings) {
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            }
            if (sceneSettings.CanvasProfile == null) {
                throw new InvalidOperationException("Scene settings must include a canvas profile.");
            }

            StatusText.Text = string.Empty;
            CanvasWidthField.Text = sceneSettings.CanvasProfile.Width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CanvasHeightField.Text = sceneSettings.CanvasProfile.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
            DontUnloadCheckBox.IsChecked = sceneSettings.DontUnload;
            ResetDialogPositioning();
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog.
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

            UpdateDialogFrame(windowWidth, windowHeight);
        }

        /// <summary>
        /// Recomputes the dialog content layout whenever the panel size changes.
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
        /// Updates the positions of the labels, text fields, status text, and footer buttons.
        /// </summary>
        void LayoutContent() {
            int panelPaddingPixels = GetPanelPaddingPixels();
            int headerHeightPixels = GetHeaderHeightPixels();
            int labelHeightPixels = GetLabelHeightPixels();
            int fieldHeightPixels = GetFieldHeightPixels();
            int footerHeightPixels = GetFooterHeightPixels();
            int sectionSpacingPixels = GetSectionSpacingPixels();
            int labelFieldSpacingPixels = GetLabelFieldSpacingPixels();
            int fieldWidthPixels = GetFieldSize().X;
            int checkBoxWidthPixels = GetCheckBoxSize().X;
            int checkBoxHeightPixels = GetCheckBoxSize().Y;
            int widthLabelY = headerHeightPixels + panelPaddingPixels;
            int widthFieldY = widthLabelY + labelHeightPixels + labelFieldSpacingPixels;
            int heightLabelY = widthFieldY + fieldHeightPixels + sectionSpacingPixels;
            int heightFieldY = heightLabelY + labelHeightPixels + labelFieldSpacingPixels;
            int dontUnloadRowY = heightFieldY + fieldHeightPixels + sectionSpacingPixels;
            int dontUnloadLabelInsetY = Math.Max(0, (checkBoxHeightPixels - labelHeightPixels) / 2);
            int dontUnloadRowHeight = Math.Max(checkBoxHeightPixels, labelHeightPixels);
            int statusY = dontUnloadRowY + dontUnloadRowHeight + sectionSpacingPixels;
            int footerY = AnchorBounds.Y - panelPaddingPixels - footerHeightPixels;
            int cancelX = AnchorBounds.X - panelPaddingPixels - GetCancelButtonSize().X;
            int applyX = cancelX - DialogMetrics.ScalePixels(8) - GetApplyButtonSize().X;
            int dontUnloadCheckBoxX = panelPaddingPixels + fieldWidthPixels - checkBoxWidthPixels;

            CanvasWidthLabelHost.Position = new float3(panelPaddingPixels, widthLabelY, 0f);
            CanvasWidthFieldHost.Position = new float3(panelPaddingPixels, widthFieldY, 0f);
            CanvasHeightLabelHost.Position = new float3(panelPaddingPixels, heightLabelY, 0f);
            CanvasHeightFieldHost.Position = new float3(panelPaddingPixels, heightFieldY, 0f);
            DontUnloadLabelHost.Position = new float3(panelPaddingPixels, dontUnloadRowY + dontUnloadLabelInsetY, 0f);
            DontUnloadCheckBoxHost.Position = new float3(dontUnloadCheckBoxX, dontUnloadRowY, 0f);
            StatusHost.Position = new float3(panelPaddingPixels, statusY, 0f);
            ApplyButtonHost.Position = new float3(applyX, footerY, 0f);
            CancelButtonHost.Position = new float3(cancelX, footerY, 0f);

            CanvasWidthLabel.Size = new int2(fieldWidthPixels, labelHeightPixels);
            CanvasHeightLabel.Size = new int2(fieldWidthPixels, labelHeightPixels);
            DontUnloadLabel.Size = new int2(Math.Max(1, fieldWidthPixels - checkBoxWidthPixels - DialogMetrics.ScalePixels(8)), labelHeightPixels);
            StatusText.Size = new int2(Math.Max(1, AnchorBounds.X - (panelPaddingPixels * 2)), labelHeightPixels);
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
        /// Gets the scaled field size.
        /// </summary>
        /// <returns>Scaled text field size.</returns>
        int2 GetFieldSize() {
            return new int2(
                DialogMetrics.ScalePixels(FieldWidth),
                GetFieldHeightPixels());
        }

        /// <summary>
        /// Gets the scaled checkbox size.
        /// </summary>
        /// <returns>Scaled checkbox size.</returns>
        int2 GetCheckBoxSize() {
            return new int2(
                DialogMetrics.ScalePixels(EditorPlatformSettingsSection.CheckBoxSize.X),
                DialogMetrics.ScalePixels(EditorPlatformSettingsSection.CheckBoxSize.Y));
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
        /// Gets the scaled panel padding in pixels.
        /// </summary>
        /// <returns>Scaled panel padding.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled header height in pixels.
        /// </summary>
        /// <returns>Scaled header height.</returns>
        int GetHeaderHeightPixels() {
            return DialogMetrics.ScalePixels(HeaderHeight);
        }

        /// <summary>
        /// Gets the scaled label height in pixels.
        /// </summary>
        /// <returns>Scaled label height.</returns>
        int GetLabelHeightPixels() {
            return DialogMetrics.ScalePixels(LabelHeight);
        }

        /// <summary>
        /// Gets the scaled field height in pixels.
        /// </summary>
        /// <returns>Scaled field height.</returns>
        int GetFieldHeightPixels() {
            return DialogMetrics.ScalePixels(FieldHeight);
        }

        /// <summary>
        /// Gets the scaled spacing between sections in pixels.
        /// </summary>
        /// <returns>Scaled section spacing.</returns>
        int GetSectionSpacingPixels() {
            return DialogMetrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled spacing between labels and fields in pixels.
        /// </summary>
        /// <returns>Scaled label-field spacing.</returns>
        int GetLabelFieldSpacingPixels() {
            return DialogMetrics.ScalePixels(LabelFieldSpacing);
        }

        /// <summary>
        /// Gets the scaled footer height in pixels.
        /// </summary>
        /// <returns>Scaled footer height.</returns>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Attempts to build the confirmed scene settings payload from the current dialog field values.
        /// </summary>
        /// <returns>Validated scene settings payload.</returns>
        SceneSettingsAsset BuildSceneSettingsFromFields() {
            if (!int.TryParse(CanvasWidthField.Text, out int canvasWidth) || canvasWidth < 1) {
                throw new InvalidOperationException("Canvas width must be a positive integer.");
            }
            if (!int.TryParse(CanvasHeightField.Text, out int canvasHeight) || canvasHeight < 1) {
                throw new InvalidOperationException("Canvas height must be a positive integer.");
            }

            return new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = canvasWidth,
                    Height = canvasHeight
                },
                DontUnload = DontUnloadCheckBox.IsChecked
            };
        }

        /// <summary>
        /// Handles apply-button activation by validating the current field values and raising the confirm event.
        /// </summary>
        void HandleApplyClicked() {
            try {
                SceneSettingsAsset sceneSettings = BuildSceneSettingsFromFields();
                StatusText.Text = string.Empty;
                if (ConfirmRequested != null) {
                    ConfirmRequested(sceneSettings);
                }
            } catch (Exception ex) {
                StatusText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Handles cancel-button activation by raising the cancel event.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }
    }
}
