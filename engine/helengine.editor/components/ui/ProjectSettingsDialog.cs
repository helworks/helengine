namespace helengine.editor {
    /// <summary>
    /// Modal dialog opened from Build > Settings that edits the project's name and description in the project file.
    /// </summary>
    public class ProjectSettingsDialog : EditorDialogBase {
        /// <summary>
        /// Unscaled dialog width.
        /// </summary>
        public const int PanelWidth = 520;

        /// <summary>
        /// Unscaled dialog height.
        /// </summary>
        public const int PanelHeight = 214;

        /// <summary>
        /// Unscaled padding between the panel edge and its content.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Unscaled label row height.
        /// </summary>
        public const int LabelHeight = 18;

        /// <summary>
        /// Unscaled text field height.
        /// </summary>
        public const int FieldHeight = 24;

        /// <summary>
        /// Unscaled spacing between field groups.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Unscaled footer height that hosts the buttons.
        /// </summary>
        public const int FooterHeight = 28;

        /// <summary>
        /// Unscaled header height.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Unscaled spacing between a label and its field.
        /// </summary>
        const int LabelFieldSpacing = 6;

        /// <summary>
        /// Unscaled Save button size.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);

        /// <summary>
        /// Unscaled Cancel button size.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Host entity of the Name label.
        /// </summary>
        readonly EditorEntity NameLabelHost;

        /// <summary>
        /// Name label.
        /// </summary>
        readonly TextComponent NameLabel;

        /// <summary>
        /// Host entity of the Name field.
        /// </summary>
        readonly EditorEntity NameFieldHost;

        /// <summary>
        /// Editable project name.
        /// </summary>
        readonly TextBoxComponent NameField;

        /// <summary>
        /// Host entity of the Description label.
        /// </summary>
        readonly EditorEntity DescriptionLabelHost;

        /// <summary>
        /// Description label.
        /// </summary>
        readonly TextComponent DescriptionLabel;

        /// <summary>
        /// Host entity of the Description field.
        /// </summary>
        readonly EditorEntity DescriptionFieldHost;

        /// <summary>
        /// Editable project description.
        /// </summary>
        readonly TextBoxComponent DescriptionField;

        /// <summary>
        /// Host entity of the validation status line.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Validation status line.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Host entity of the Save button.
        /// </summary>
        readonly EditorEntity SaveButtonHost;

        /// <summary>
        /// Save button.
        /// </summary>
        readonly ButtonComponent SaveButton;

        /// <summary>
        /// Host entity of the Cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>
        /// Cancel button.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// True once construction has finished and layout may run.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised with the validated settings when the user clicks Save.
        /// </summary>
        public event Action<EditorProjectSettings> ConfirmRequested;

        /// <summary>
        /// Raised when the user clicks Cancel or closes the dialog.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Creates the dialog with default UI metrics.
        /// </summary>
        public ProjectSettingsDialog(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font)
            : this(ownerCore, interactionServices, font, EditorUiMetrics.Default) { }

        /// <summary>
        /// Creates the dialog and all of its widgets; it starts hidden.
        /// </summary>
        public ProjectSettingsDialog(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font, EditorUiMetrics metrics)
            : base(ownerCore, interactionServices, "ProjectSettingsDialog", "Project Settings", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            NameLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(NameLabelHost);
            NameLabel = CreateDialogLabel("Name");
            NameLabelHost.AddComponent(NameLabel);

            NameFieldHost = CreateDialogHost();
            DialogPanelRoot.AddChild(NameFieldHost);
            NameField = new TextBoxComponent(GetFieldSize(), DialogFont, string.Empty);
            NameField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            NameFieldHost.AddComponent(NameField);

            DescriptionLabelHost = CreateDialogHost();
            DialogPanelRoot.AddChild(DescriptionLabelHost);
            DescriptionLabel = CreateDialogLabel("Description");
            DescriptionLabelHost.AddComponent(DescriptionLabel);

            DescriptionFieldHost = CreateDialogHost();
            DialogPanelRoot.AddChild(DescriptionFieldHost);
            DescriptionField = new TextBoxComponent(GetFieldSize(), DialogFont, string.Empty);
            DescriptionField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            DescriptionFieldHost.AddComponent(DescriptionField);

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

            SaveButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", GetSaveButtonSize(), DialogFont, HandleSaveClicked, 0f);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            SaveButtonHost.AddComponent(SaveButton);

            CancelButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", GetCancelButtonSize(), DialogFont, HandleCancelClicked, 0f);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            CancelButtonHost.AddComponent(CancelButton);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Fills the fields from the supplied settings and shows the dialog.
        /// </summary>
        /// <param name="settings">Current project name and description.</param>
        public void Show(EditorProjectSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            StatusText.Text = string.Empty;
            NameField.Text = settings.Name ?? string.Empty;
            DescriptionField.Text = settings.Description ?? string.Empty;
            ResetDialogPositioning();
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog without raising any event.
        /// </summary>
        public void Hide() {
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Re-frames the dialog for a new window size.
        /// </summary>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            UpdateDialogFrame(windowWidth, windowHeight);
        }

        /// <summary>
        /// Treats the header close button as Cancel.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Re-lays out the content after the frame changed.
        /// </summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutContent();
        }

        /// <summary>
        /// Positions labels, fields, status and buttons top to bottom.
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
            int nameLabelY = headerHeightPixels + panelPaddingPixels;
            int nameFieldY = nameLabelY + labelHeightPixels + labelFieldSpacingPixels;
            int descriptionLabelY = nameFieldY + fieldHeightPixels + sectionSpacingPixels;
            int descriptionFieldY = descriptionLabelY + labelHeightPixels + labelFieldSpacingPixels;
            int statusY = descriptionFieldY + fieldHeightPixels + sectionSpacingPixels;
            int footerY = AnchorBounds.Y - panelPaddingPixels - footerHeightPixels;
            int cancelX = AnchorBounds.X - panelPaddingPixels - GetCancelButtonSize().X;
            int saveX = cancelX - DialogMetrics.ScalePixels(8) - GetSaveButtonSize().X;

            NameLabelHost.Position = new float3(panelPaddingPixels, nameLabelY, 0f);
            NameFieldHost.Position = new float3(panelPaddingPixels, nameFieldY, 0f);
            DescriptionLabelHost.Position = new float3(panelPaddingPixels, descriptionLabelY, 0f);
            DescriptionFieldHost.Position = new float3(panelPaddingPixels, descriptionFieldY, 0f);
            StatusHost.Position = new float3(panelPaddingPixels, statusY, 0f);
            SaveButtonHost.Position = new float3(saveX, footerY, 0f);
            CancelButtonHost.Position = new float3(cancelX, footerY, 0f);

            NameLabel.Size = new int2(fieldWidthPixels, labelHeightPixels);
            DescriptionLabel.Size = new int2(fieldWidthPixels, labelHeightPixels);
            StatusText.Size = new int2(Math.Max(1, AnchorBounds.X - (panelPaddingPixels * 2)), labelHeightPixels);
        }

        /// <summary>
        /// Creates one internal entity parented to the dialog panel.
        /// </summary>
        EditorEntity CreateDialogHost() {
            return new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Creates one themed label.
        /// </summary>
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
        /// Text fields span the panel width minus the padding.
        /// </summary>
        int2 GetFieldSize() {
            return new int2(
                DialogMetrics.ScalePixels(PanelWidth - (PanelPadding * 2)),
                GetFieldHeightPixels());
        }

        /// <summary>
        /// Scaled Save button size.
        /// </summary>
        int2 GetSaveButtonSize() {
            return new int2(
                DialogMetrics.ScalePixels(SaveButtonSize.X),
                DialogMetrics.ScalePixels(SaveButtonSize.Y));
        }

        /// <summary>
        /// Scaled Cancel button size.
        /// </summary>
        int2 GetCancelButtonSize() {
            return new int2(
                DialogMetrics.ScalePixels(CancelButtonSize.X),
                DialogMetrics.ScalePixels(CancelButtonSize.Y));
        }

        /// <summary>
        /// Scaled panel padding.
        /// </summary>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Scaled header height.
        /// </summary>
        int GetHeaderHeightPixels() {
            return DialogMetrics.ScalePixels(HeaderHeight);
        }

        /// <summary>
        /// Scaled label height.
        /// </summary>
        int GetLabelHeightPixels() {
            return DialogMetrics.ScalePixels(LabelHeight);
        }

        /// <summary>
        /// Scaled field height.
        /// </summary>
        int GetFieldHeightPixels() {
            return DialogMetrics.ScalePixels(FieldHeight);
        }

        /// <summary>
        /// Scaled section spacing.
        /// </summary>
        int GetSectionSpacingPixels() {
            return DialogMetrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Scaled label-to-field spacing.
        /// </summary>
        int GetLabelFieldSpacingPixels() {
            return DialogMetrics.ScalePixels(LabelFieldSpacing);
        }

        /// <summary>
        /// Scaled footer height.
        /// </summary>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Builds validated settings from the fields; a blank name is an error shown on the status line.
        /// </summary>
        EditorProjectSettings BuildSettingsFromFields() {
            string name = (NameField.Text ?? string.Empty).Trim();
            if (name.Length == 0) {
                throw new InvalidOperationException("Project name must be provided.");
            }

            return new EditorProjectSettings {
                Name = name,
                Description = (DescriptionField.Text ?? string.Empty).Trim()
            };
        }

        /// <summary>
        /// Validates the fields and raises <see cref="ConfirmRequested"/>, or shows the validation message.
        /// </summary>
        void HandleSaveClicked() {
            try {
                EditorProjectSettings settings = BuildSettingsFromFields();
                StatusText.Text = string.Empty;
                if (ConfirmRequested != null) {
                    ConfirmRequested(settings);
                }
            } catch (InvalidOperationException ex) {
                StatusText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Raises <see cref="CancelRequested"/>.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }
    }
}
