using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to edit per-platform build and graphics profiles.
    /// </summary>
    public class ProfilesDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 560;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 372;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Height reserved for the footer buttons.
        /// </summary>
        public const int FooterHeight = 28;

        /// <summary>
        /// Vertical spacing between groups of controls.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height reserved for each form row.
        /// </summary>
        public const int FieldRowHeight = 24;

        /// <summary>
        /// Height reserved for section titles.
        /// </summary>
        public const int SectionTitleHeight = 18;

        /// <summary>
        /// Width reserved for field labels.
        /// </summary>
        public const int LabelColumnWidth = 180;

        /// <summary>
        /// Width reserved for numeric text fields.
        /// </summary>
        public const int NumericFieldWidth = 96;

        /// <summary>
        /// Width reserved for the platform selector combo box.
        /// </summary>
        public const int PlatformComboBoxWidth = 220;

        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Font used for all dialog labels and controls.
        /// </summary>
        readonly FontAsset DialogFontValue;

        /// <summary>
        /// Host entity for the platform selector label.
        /// </summary>
        readonly EditorEntity PlatformLabelHost;

        /// <summary>
        /// Label describing the platform selector.
        /// </summary>
        readonly TextComponent PlatformLabelText;

        /// <summary>
        /// Host entity for the platform selector control.
        /// </summary>
        readonly EditorEntity PlatformComboBoxHost;

        /// <summary>
        /// Combo box used to switch between per-platform profile records.
        /// </summary>
        readonly ComboBoxComponent PlatformComboBox;

        /// <summary>
        /// Host entity for the build-profile section title.
        /// </summary>
        readonly EditorEntity BuildTitleHost;

        /// <summary>
        /// Text component that renders the build-profile section title.
        /// </summary>
        readonly TextComponent BuildTitleText;

        /// <summary>
        /// Host entity for the build texture scale label.
        /// </summary>
        readonly EditorEntity TextureScaleLabelHost;

        /// <summary>
        /// Label describing the texture-scale field.
        /// </summary>
        readonly TextComponent TextureScaleLabelText;

        /// <summary>
        /// Host entity for the build texture scale text box.
        /// </summary>
        readonly EditorEntity TextureScaleTextBoxHost;

        /// <summary>
        /// Text box used to edit build-time texture scaling.
        /// </summary>
        readonly TextBoxComponent TextureScaleTextBox;

        /// <summary>
        /// Host entity for the shader-pruning label.
        /// </summary>
        readonly EditorEntity ShaderPruningLabelHost;

        /// <summary>
        /// Label describing the shader-pruning checkbox.
        /// </summary>
        readonly TextComponent ShaderPruningLabelText;

        /// <summary>
        /// Host entity for the shader-pruning checkbox.
        /// </summary>
        readonly EditorEntity ShaderPruningCheckBoxHost;

        /// <summary>
        /// Checkbox used to toggle shader variant pruning.
        /// </summary>
        readonly CheckBoxComponent ShaderPruningCheckBox;

        /// <summary>
        /// Host entity for the graphics-profile section title.
        /// </summary>
        readonly EditorEntity GraphicsTitleHost;

        /// <summary>
        /// Text component that renders the graphics-profile section title.
        /// </summary>
        readonly TextComponent GraphicsTitleText;

        /// <summary>
        /// Host entity for the default-width label.
        /// </summary>
        readonly EditorEntity WidthLabelHost;

        /// <summary>
        /// Label describing the default-width field.
        /// </summary>
        readonly TextComponent WidthLabelText;

        /// <summary>
        /// Host entity for the default-width text box.
        /// </summary>
        readonly EditorEntity WidthTextBoxHost;

        /// <summary>
        /// Text box used to edit the default backbuffer width.
        /// </summary>
        readonly TextBoxComponent WidthTextBox;

        /// <summary>
        /// Host entity for the default-height label.
        /// </summary>
        readonly EditorEntity HeightLabelHost;

        /// <summary>
        /// Label describing the default-height field.
        /// </summary>
        readonly TextComponent HeightLabelText;

        /// <summary>
        /// Host entity for the default-height text box.
        /// </summary>
        readonly EditorEntity HeightTextBoxHost;

        /// <summary>
        /// Text box used to edit the default backbuffer height.
        /// </summary>
        readonly TextBoxComponent HeightTextBox;

        /// <summary>
        /// Host entity for the vsync label.
        /// </summary>
        readonly EditorEntity VSyncLabelHost;

        /// <summary>
        /// Label describing the vsync checkbox.
        /// </summary>
        readonly TextComponent VSyncLabelText;

        /// <summary>
        /// Host entity for the vsync checkbox.
        /// </summary>
        readonly EditorEntity VSyncCheckBoxHost;

        /// <summary>
        /// Checkbox used to toggle default vsync behavior.
        /// </summary>
        readonly CheckBoxComponent VSyncCheckBox;

        /// <summary>
        /// Host entity for the fullscreen label.
        /// </summary>
        readonly EditorEntity FullscreenLabelHost;

        /// <summary>
        /// Label describing the fullscreen checkbox.
        /// </summary>
        readonly TextComponent FullscreenLabelText;

        /// <summary>
        /// Host entity for the fullscreen checkbox.
        /// </summary>
        readonly EditorEntity FullscreenCheckBoxHost;

        /// <summary>
        /// Checkbox used to toggle default fullscreen behavior.
        /// </summary>
        readonly CheckBoxComponent FullscreenCheckBox;

        /// <summary>
        /// Host entity for the status text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Status text shown above the footer buttons.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// Host entity for the save button.
        /// </summary>
        readonly EditorEntity SaveButtonHost;

        /// <summary>
        /// Save button component.
        /// </summary>
        readonly ButtonComponent SaveButton;

        /// <summary>
        /// Supported platform ids currently shown in the combo box.
        /// </summary>
        readonly List<string> SupportedPlatformIds;

        /// <summary>
        /// Mutable profile document currently being edited.
        /// </summary>
        EditorProfileSettingsDocument CurrentDocument;

        /// <summary>
        /// Currently selected platform id being edited.
        /// </summary>
        string CurrentPlatformId;

        /// <summary>
        /// Tracks whether the platform selection is being updated by code instead of by the user.
        /// </summary>
        bool IsInitializingSelection;

        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms the edited profiles.
        /// </summary>
        public event Action<ProfilesDialogSelection> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the profiles workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes one profiles dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public ProfilesDialog(FontAsset font) : base("ProfilesDialog", "Profiles", font, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            DialogFontValue = font;
            SupportedPlatformIds = new List<string>(8);

            PlatformLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(PlatformLabelHost);
            PlatformLabelText = CreateLabelText("Platform");
            PlatformLabelHost.AddComponent(PlatformLabelText);

            PlatformComboBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(PlatformComboBoxHost);
            PlatformComboBox = new ComboBoxComponent(new int2(PlatformComboBoxWidth, FieldRowHeight), DialogFontValue, Array.Empty<string>(), -1);
            PlatformComboBox.SelectionChanged += HandlePlatformSelectionChanged;
            PlatformComboBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder, RenderOrder2D.ModalBackground, RenderOrder2D.ModalForeground);
            PlatformComboBoxHost.AddComponent(PlatformComboBox);

            BuildTitleHost = CreateTextHost();
            DialogPanelRoot.AddChild(BuildTitleHost);
            BuildTitleText = CreateSectionTitleText("Build Profiles");
            BuildTitleHost.AddComponent(BuildTitleText);

            TextureScaleLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(TextureScaleLabelHost);
            TextureScaleLabelText = CreateLabelText("Texture scale %");
            TextureScaleLabelHost.AddComponent(TextureScaleLabelText);

            TextureScaleTextBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(TextureScaleTextBoxHost);
            TextureScaleTextBox = new TextBoxComponent(new int2(NumericFieldWidth, FieldRowHeight), DialogFontValue, string.Empty);
            TextureScaleTextBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            TextureScaleTextBoxHost.AddComponent(TextureScaleTextBox);

            ShaderPruningLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(ShaderPruningLabelHost);
            ShaderPruningLabelText = CreateLabelText("Shader variant pruning");
            ShaderPruningLabelHost.AddComponent(ShaderPruningLabelText);

            ShaderPruningCheckBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(ShaderPruningCheckBoxHost);
            ShaderPruningCheckBox = new CheckBoxComponent(new int2(18, 18), DialogFontValue, true);
            ShaderPruningCheckBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            ShaderPruningCheckBoxHost.AddComponent(ShaderPruningCheckBox);

            GraphicsTitleHost = CreateTextHost();
            DialogPanelRoot.AddChild(GraphicsTitleHost);
            GraphicsTitleText = CreateSectionTitleText("Graphics Profiles");
            GraphicsTitleHost.AddComponent(GraphicsTitleText);

            WidthLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(WidthLabelHost);
            WidthLabelText = CreateLabelText("Default width");
            WidthLabelHost.AddComponent(WidthLabelText);

            WidthTextBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(WidthTextBoxHost);
            WidthTextBox = new TextBoxComponent(new int2(NumericFieldWidth, FieldRowHeight), DialogFontValue, string.Empty);
            WidthTextBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            WidthTextBoxHost.AddComponent(WidthTextBox);

            HeightLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(HeightLabelHost);
            HeightLabelText = CreateLabelText("Default height");
            HeightLabelHost.AddComponent(HeightLabelText);

            HeightTextBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(HeightTextBoxHost);
            HeightTextBox = new TextBoxComponent(new int2(NumericFieldWidth, FieldRowHeight), DialogFontValue, string.Empty);
            HeightTextBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            HeightTextBoxHost.AddComponent(HeightTextBox);

            VSyncLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(VSyncLabelHost);
            VSyncLabelText = CreateLabelText("VSync");
            VSyncLabelHost.AddComponent(VSyncLabelText);

            VSyncCheckBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(VSyncCheckBoxHost);
            VSyncCheckBox = new CheckBoxComponent(new int2(18, 18), DialogFontValue, true);
            VSyncCheckBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            VSyncCheckBoxHost.AddComponent(VSyncCheckBox);

            FullscreenLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(FullscreenLabelHost);
            FullscreenLabelText = CreateLabelText("Fullscreen");
            FullscreenLabelHost.AddComponent(FullscreenLabelText);

            FullscreenCheckBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(FullscreenCheckBoxHost);
            FullscreenCheckBox = new CheckBoxComponent(new int2(18, 18), DialogFontValue, false);
            FullscreenCheckBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            FullscreenCheckBoxHost.AddComponent(FullscreenCheckBox);

            StatusHost = CreateTextHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFontValue,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFontValue.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, DialogFontValue, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", SaveButtonSize, DialogFontValue, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog for the provided profile document and platform set.
        /// </summary>
        /// <param name="document">Mutable profile settings document for the current project.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the project.</param>
        /// <param name="activePlatformId">Platform currently being edited.</param>
        public void Show(EditorProfileSettingsDocument document, IReadOnlyList<string> supportedPlatforms, string activePlatformId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            int activeIndex = ResolvePlatformIndex(supportedPlatforms, activePlatformId);
            CurrentDocument = document;
            CurrentPlatformId = supportedPlatforms[activeIndex];

            SupportedPlatformIds.Clear();
            for (int i = 0; i < supportedPlatforms.Count; i++) {
                SupportedPlatformIds.Add(supportedPlatforms[i]);
            }

            ResetDialogPositioning();
            Enabled = true;
            StatusText.Text = string.Empty;

            IsInitializingSelection = true;
            PlatformComboBox.SetItems(SupportedPlatformIds, activeIndex);
            IsInitializingSelection = false;
            LoadSelectedPlatformIntoFields(CurrentPlatformId);
        }

        /// <summary>
        /// Hides the dialog and clears the transient selection state.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
            StatusText.Text = string.Empty;
            CurrentDocument = null;
            CurrentPlatformId = string.Empty;
            SupportedPlatformIds.Clear();
        }

        /// <summary>
        /// Updates dialog layout to fit the current host window size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }
            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }

            LayoutPlatformSelector();
            LayoutBuildSection();
            LayoutGraphicsSection();
            LayoutStatus();
            LayoutButtons();
        }

        /// <summary>
        /// Handles platform selection changes from the combo box.
        /// </summary>
        /// <param name="index">Selected combo-box row index.</param>
        /// <param name="platformId">Selected platform identifier.</param>
        void HandlePlatformSelectionChanged(int index, string platformId) {
            if (IsInitializingSelection || CurrentDocument == null) {
                return;
            }

            if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
                StatusText.Text = errorMessage;
                IsInitializingSelection = true;
                PlatformComboBox.SelectedIndex = ResolvePlatformIndex(SupportedPlatformIds, CurrentPlatformId);
                IsInitializingSelection = false;
                return;
            }

            CurrentPlatformId = platformId;
            StatusText.Text = string.Empty;
            LoadSelectedPlatformIntoFields(platformId);
        }

        /// <summary>
        /// Handles save button clicks by validating the current fields and raising the confirmed selection.
        /// </summary>
        void HandleSaveClicked() {
            if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
                StatusText.Text = errorMessage;
                return;
            }

            StatusText.Text = string.Empty;
            if (ConfirmRequested != null) {
                ConfirmRequested(new ProfilesDialogSelection(CurrentPlatformId, CurrentDocument));
            }
        }

        /// <summary>
        /// Handles cancel button clicks by raising the cancel action.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Loads one platform profile into the visible fields.
        /// </summary>
        /// <param name="platformId">Platform identifier to load.</param>
        void LoadSelectedPlatformIntoFields(string platformId) {
            EditorPlatformProfileSettingsDocument platform = GetPlatformDocument(platformId);

            TextureScaleTextBox.Text = platform.Build.TextureScalePercent.ToString(CultureInfo.InvariantCulture);
            ShaderPruningCheckBox.IsChecked = platform.Build.ShaderVariantPruningEnabled;
            WidthTextBox.Text = platform.Graphics.DefaultWidth.ToString(CultureInfo.InvariantCulture);
            HeightTextBox.Text = platform.Graphics.DefaultHeight.ToString(CultureInfo.InvariantCulture);
            VSyncCheckBox.IsChecked = platform.Graphics.VSyncEnabled;
            FullscreenCheckBox.IsChecked = platform.Graphics.FullscreenEnabled;
        }

        /// <summary>
        /// Stores the current field values into the active platform profile.
        /// </summary>
        /// <param name="errorMessage">Validation error message when parsing fails.</param>
        /// <returns>True when the current fields were parsed and stored successfully.</returns>
        bool TryStoreCurrentPlatformFields(out string errorMessage) {
            errorMessage = string.Empty;
            if (CurrentDocument == null) {
                errorMessage = "No profile document is loaded.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                errorMessage = "No active platform is selected.";
                return false;
            }
            if (!TryParsePositiveInteger(TextureScaleTextBox.Text, out int textureScalePercent)) {
                errorMessage = "Texture scale must be a positive whole number.";
                return false;
            }
            if (!TryParsePositiveInteger(WidthTextBox.Text, out int defaultWidth)) {
                errorMessage = "Default width must be a positive whole number.";
                return false;
            }
            if (!TryParsePositiveInteger(HeightTextBox.Text, out int defaultHeight)) {
                errorMessage = "Default height must be a positive whole number.";
                return false;
            }

            EditorPlatformProfileSettingsDocument platform = GetPlatformDocument(CurrentPlatformId);
            platform.Build.TextureScalePercent = textureScalePercent;
            platform.Build.ShaderVariantPruningEnabled = ShaderPruningCheckBox.IsChecked;
            platform.Graphics.DefaultWidth = defaultWidth;
            platform.Graphics.DefaultHeight = defaultHeight;
            platform.Graphics.VSyncEnabled = VSyncCheckBox.IsChecked;
            platform.Graphics.FullscreenEnabled = FullscreenCheckBox.IsChecked;
            return true;
        }

        /// <summary>
        /// Returns one platform profile record, creating it when the document is missing the record.
        /// </summary>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching platform profile document.</returns>
        EditorPlatformProfileSettingsDocument GetPlatformDocument(string platformId) {
            if (CurrentDocument == null) {
                throw new InvalidOperationException("The profiles dialog does not have a loaded document.");
            }

            for (int i = 0; i < CurrentDocument.Platforms.Count; i++) {
                EditorPlatformProfileSettingsDocument platform = CurrentDocument.Platforms[i];
                if (platform != null && string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    if (platform.Build == null) {
                        platform.Build = new EditorBuildProfileSettingsDocument();
                    }
                    if (platform.Graphics == null) {
                        platform.Graphics = new EditorGraphicsProfileSettingsDocument();
                    }

                    return platform;
                }
            }

            EditorPlatformProfileSettingsDocument createdPlatform = new EditorPlatformProfileSettingsDocument {
                PlatformId = platformId,
                Build = new EditorBuildProfileSettingsDocument(),
                Graphics = new EditorGraphicsProfileSettingsDocument()
            };
            CurrentDocument.Platforms.Add(createdPlatform);
            return createdPlatform;
        }

        /// <summary>
        /// Returns the selected index for one platform identifier in the supported list.
        /// </summary>
        /// <param name="platforms">Supported platform identifiers.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Zero-based platform index.</returns>
        int ResolvePlatformIndex(IReadOnlyList<string> platforms, string platformId) {
            for (int i = 0; i < platforms.Count; i++) {
                if (string.Equals(platforms[i], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }

            throw new InvalidOperationException($"Platform '{platformId}' is not available in the dialog.");
        }

        /// <summary>
        /// Parses one positive integer from the supplied string.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="value">Parsed integer value when successful.</param>
        /// <returns>True when the text parses as a positive integer.</returns>
        bool TryParsePositiveInteger(string text, out int value) {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0) {
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Creates one host entity for a left-aligned dialog row.
        /// </summary>
        /// <returns>New dialog host entity.</returns>
        EditorEntity CreateTextHost() {
            return new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Creates one standard label text component for the dialog.
        /// </summary>
        /// <param name="text">Label text to render.</param>
        /// <returns>Configured label text component.</returns>
        TextComponent CreateLabelText(string text) {
            return new TextComponent {
                Font = DialogFontValue,
                Text = text,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFontValue.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
        }

        /// <summary>
        /// Creates one section title text component for the dialog.
        /// </summary>
        /// <param name="text">Section title text to render.</param>
        /// <returns>Configured title text component.</returns>
        TextComponent CreateSectionTitleText(string text) {
            return new TextComponent {
                Font = DialogFontValue,
                Text = text,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFontValue.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
        }

        /// <summary>
        /// Positions the platform selector row.
        /// </summary>
        void LayoutPlatformSelector() {
            float rowY = HeaderHeight + PanelPadding;
            PlatformLabelHost.Position = new float3(PanelPadding, rowY + 2f, 0.1f);
            PlatformComboBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, rowY, 0.1f);
        }

        /// <summary>
        /// Positions the build-profile section controls.
        /// </summary>
        void LayoutBuildSection() {
            float titleY = HeaderHeight + PanelPadding + FieldRowHeight + SectionSpacing;
            BuildTitleHost.Position = new float3(PanelPadding, titleY, 0.1f);

            float row1Y = titleY + SectionTitleHeight + 6f;
            TextureScaleLabelHost.Position = new float3(PanelPadding, row1Y + 2f, 0.1f);
            TextureScaleTextBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, row1Y, 0.1f);

            float row2Y = row1Y + FieldRowHeight + 8f;
            ShaderPruningLabelHost.Position = new float3(PanelPadding, row2Y + 2f, 0.1f);
            ShaderPruningCheckBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, row2Y + 3f, 0.1f);
        }

        /// <summary>
        /// Positions the graphics-profile section controls.
        /// </summary>
        void LayoutGraphicsSection() {
            float titleY = HeaderHeight + PanelPadding + FieldRowHeight + SectionSpacing + SectionTitleHeight + 6f + FieldRowHeight + 8f + FieldRowHeight + SectionSpacing;
            GraphicsTitleHost.Position = new float3(PanelPadding, titleY, 0.1f);

            float row1Y = titleY + SectionTitleHeight + 6f;
            WidthLabelHost.Position = new float3(PanelPadding, row1Y + 2f, 0.1f);
            WidthTextBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, row1Y, 0.1f);

            float row2Y = row1Y + FieldRowHeight + 8f;
            HeightLabelHost.Position = new float3(PanelPadding, row2Y + 2f, 0.1f);
            HeightTextBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, row2Y, 0.1f);

            float row3Y = row2Y + FieldRowHeight + 8f;
            VSyncLabelHost.Position = new float3(PanelPadding, row3Y + 2f, 0.1f);
            VSyncCheckBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, row3Y + 3f, 0.1f);

            float row4Y = row3Y + FieldRowHeight + 8f;
            FullscreenLabelHost.Position = new float3(PanelPadding, row4Y + 2f, 0.1f);
            FullscreenCheckBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, row4Y + 3f, 0.1f);
        }

        /// <summary>
        /// Positions the status text above the footer.
        /// </summary>
        void LayoutStatus() {
            float statusY = PanelHeight - FooterHeight - 38f;
            StatusHost.Position = new float3(PanelPadding, statusY, 0.1f);
        }

        /// <summary>
        /// Positions the save and cancel buttons along the footer.
        /// </summary>
        void LayoutButtons() {
            float buttonY = PanelHeight - FooterHeight - 2f;
            CancelButtonHost.Position = new float3(PanelWidth - PanelPadding - CancelButtonSize.X, buttonY, 0.1f);
            SaveButtonHost.Position = new float3(PanelWidth - PanelPadding - CancelButtonSize.X - 10f - SaveButtonSize.X, buttonY, 0.1f);
        }

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }
    }
}
