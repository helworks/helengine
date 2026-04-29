using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to change the supported build platforms for the current project.
    /// </summary>
    public class BuildSettingsDialog : EditorEntity {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 420;
        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 236;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// Height reserved for each platform row.
        /// </summary>
        public const int PlatformRowHeight = 24;
        /// <summary>
        /// Height reserved for the footer buttons.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Corner radius applied to the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness applied to the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the header close button.
        /// </summary>
        static readonly int2 CloseButtonSize = new int2(22, 22);
        /// <summary>
        /// Fixed size used for each platform checkbox.
        /// </summary>
        static readonly int2 CheckBoxSize = new int2(18, 18);

        /// <summary>
        /// Font used for dialog labels and buttons.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity hosting the panel content.
        /// </summary>
        readonly EditorEntity PanelRoot;
        /// <summary>
        /// Dialog background shape.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;
        /// <summary>
        /// Host entity for the dialog title.
        /// </summary>
        readonly EditorEntity TitleHost;
        /// <summary>
        /// Title text shown at the top of the dialog.
        /// </summary>
        readonly TextComponent TitleText;
        /// <summary>
        /// Host entity for the header close button.
        /// </summary>
        readonly EditorEntity CloseButtonHost;
        /// <summary>
        /// Header close button component.
        /// </summary>
        readonly ButtonComponent CloseButton;
        /// <summary>
        /// Host entity for validation or empty-state text.
        /// </summary>
        readonly EditorEntity StatusHost;
        /// <summary>
        /// Validation or empty-state text shown above the footer.
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
        /// Hosts created for each platform label row.
        /// </summary>
        readonly List<EditorEntity> PlatformLabelHosts;
        /// <summary>
        /// Text components used to render the platform names.
        /// </summary>
        readonly List<TextComponent> PlatformLabelTexts;
        /// <summary>
        /// Hosts created for each platform checkbox row.
        /// </summary>
        readonly List<EditorEntity> PlatformCheckBoxHosts;
        /// <summary>
        /// Checkbox components used to select supported platforms.
        /// </summary>
        readonly List<CheckBoxComponent> PlatformCheckBoxes;
        /// <summary>
        /// Platform descriptors currently shown by the dialog.
        /// </summary>
        readonly List<AvailablePlatformDescriptor> AvailablePlatforms;
        /// <summary>
        /// Render order used for panel surfaces.
        /// </summary>
        readonly byte PanelOrder;
        /// <summary>
        /// Render order used for foreground text and controls.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms one supported-platform selection.
        /// </summary>
        public event Action<BuildSettingsSelection> ConfirmRequested;
        /// <summary>
        /// Raised when the user cancels the build-settings workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes a new build-settings dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public BuildSettingsDialog(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            PlatformLabelHosts = new List<EditorEntity>(8);
            PlatformLabelTexts = new List<TextComponent>(8);
            PlatformCheckBoxHosts = new List<EditorEntity>(8);
            PlatformCheckBoxes = new List<CheckBoxComponent>(8);
            AvailablePlatforms = new List<AvailablePlatformDescriptor>(8);

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "BuildSettingsDialog";

            PanelOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

            PanelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            AddChild(PanelRoot);

            PanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = PanelOrder,
                Size = new int2(PanelWidth, PanelHeight)
            };
            PanelRoot.AddComponent(PanelBackground);

            TitleHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(TitleHost);

            TitleText = new TextComponent {
                Font = font,
                Text = "Build Settings",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            TitleHost.AddComponent(TitleText);

            CloseButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(CloseButtonHost);

            CloseButton = new ButtonComponent("X", CloseButtonSize, font, HandleCloseClicked, 0f);
            CloseButtonHost.AddComponent(CloseButton);
            CloseButton.SetRenderOrders(TextOrder, TextOrder);
            CloseButton.UseHoverOnlyBackground();
            CloseButton.SetTextColor(ThemeManager.Colors.InputForegroundPrimary);

            StatusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(StatusHost);

            StatusText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, font, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(TextOrder, TextOrder);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", SaveButtonSize, font, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(TextOrder, TextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Shows the dialog for the provided available and currently supported platforms.
        /// </summary>
        /// <param name="availablePlatforms">Selectable platforms discovered for the current engine environment.</param>
        /// <param name="supportedPlatforms">Platforms currently written into the project file.</param>
        public void Show(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms, IReadOnlyList<string> supportedPlatforms) {
            if (availablePlatforms == null) {
                throw new ArgumentNullException(nameof(availablePlatforms));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            Enabled = true;
            StatusText.Text = string.Empty;

            RebuildPlatformRows(availablePlatforms, supportedPlatforms);
            ApplyEmptyPlatformMessage();
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            EditorInputCaptureService.ClearBlocker(this);
            Enabled = false;
            StatusText.Text = string.Empty;
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
            if (!Enabled) {
                EditorInputCaptureService.ClearBlocker(this);
                return;
            }

            int safeWidth = Math.Max(1, windowWidth);
            int safeHeight = Math.Max(1, windowHeight);
            PanelPosition = new int2(
                Math.Max(0, (safeWidth - PanelWidth) / 2),
                Math.Max(0, (safeHeight - PanelHeight) / 2));

            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
            EditorInputCaptureService.SetBlocker(this, PanelPosition, new int2(PanelWidth, PanelHeight));

            LayoutTitle();
            LayoutPlatformRows();
            LayoutStatus();
            LayoutButtons();
        }

        /// <summary>
        /// Raises the cancel action for the dialog.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Raises the cancel action from the header close button.
        /// </summary>
        void HandleCloseClicked() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Validates and raises the confirmed platform selection.
        /// </summary>
        void HandleSaveClicked() {
            List<string> selectedPlatformIds = CollectSelectedPlatformIds();
            if (selectedPlatformIds.Count == 0) {
                StatusText.Text = "Select at least one platform.";
                return;
            }

            StatusText.Text = string.Empty;
            if (ConfirmRequested != null) {
                ConfirmRequested(new BuildSettingsSelection(selectedPlatformIds));
            }
        }

        /// <summary>
        /// Rebuilds the platform rows for the current available-platform set.
        /// </summary>
        /// <param name="availablePlatforms">Selectable available platforms.</param>
        /// <param name="supportedPlatforms">Currently supported project platforms.</param>
        void RebuildPlatformRows(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms, IReadOnlyList<string> supportedPlatforms) {
            ClearPlatformRows();
            AvailablePlatforms.Clear();

            for (int platformIndex = 0; platformIndex < availablePlatforms.Count; platformIndex++) {
                AvailablePlatformDescriptor platform = availablePlatforms[platformIndex];
                AvailablePlatforms.Add(platform);
                CreatePlatformRow(platform, IsSupportedPlatform(platform.Id, supportedPlatforms));
            }
        }

        /// <summary>
        /// Applies the empty-state message when no selectable platforms are available.
        /// </summary>
        void ApplyEmptyPlatformMessage() {
            if (AvailablePlatforms.Count == 0) {
                StatusText.Text = "No installed platforms are available for this engine.";
                return;
            }

            StatusText.Text = string.Empty;
        }

        /// <summary>
        /// Removes all existing platform row entities and clears the backing lists.
        /// </summary>
        void ClearPlatformRows() {
            for (int index = 0; index < PlatformLabelHosts.Count; index++) {
                PanelRoot.RemoveChild(PlatformLabelHosts[index]);
            }

            for (int index = 0; index < PlatformCheckBoxHosts.Count; index++) {
                PanelRoot.RemoveChild(PlatformCheckBoxHosts[index]);
            }

            PlatformLabelHosts.Clear();
            PlatformLabelTexts.Clear();
            PlatformCheckBoxHosts.Clear();
            PlatformCheckBoxes.Clear();
        }

        /// <summary>
        /// Creates one platform label row and matching checkbox.
        /// </summary>
        /// <param name="platform">Platform descriptor to render.</param>
        /// <param name="isChecked">True when the platform should start selected.</param>
        void CreatePlatformRow(AvailablePlatformDescriptor platform, bool isChecked) {
            EditorEntity labelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(labelHost);
            PlatformLabelHosts.Add(labelHost);

            TextComponent labelText = new TextComponent {
                Font = Font,
                Text = platform.DisplayName,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(Font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            labelHost.AddComponent(labelText);
            PlatformLabelTexts.Add(labelText);

            EditorEntity checkBoxHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(checkBoxHost);
            PlatformCheckBoxHosts.Add(checkBoxHost);

            CheckBoxComponent checkBox = new CheckBoxComponent(CheckBoxSize, Font, isChecked);
            checkBoxHost.AddComponent(checkBox);
            checkBox.SetRenderOrders(TextOrder, TextOrder);
            PlatformCheckBoxes.Add(checkBox);
        }

        /// <summary>
        /// Determines whether one platform id exists in the currently supported list.
        /// </summary>
        /// <param name="platformId">Platform id to locate.</param>
        /// <param name="supportedPlatforms">Current supported-platform ids.</param>
        /// <returns>True when the platform id is supported.</returns>
        bool IsSupportedPlatform(string platformId, IReadOnlyList<string> supportedPlatforms) {
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                if (string.Equals(supportedPlatforms[index], platformId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Collects the selected platform ids using the visible row order.
        /// </summary>
        /// <returns>Selected platform ids in row order.</returns>
        List<string> CollectSelectedPlatformIds() {
            List<string> selectedPlatformIds = new List<string>(PlatformCheckBoxes.Count);

            for (int index = 0; index < PlatformCheckBoxes.Count; index++) {
                if (PlatformCheckBoxes[index].IsChecked) {
                    selectedPlatformIds.Add(AvailablePlatforms[index].Id);
                }
            }

            return selectedPlatformIds;
        }

        /// <summary>
        /// Positions the dialog title.
        /// </summary>
        void LayoutTitle() {
            TitleHost.Position = new float3(PanelPadding, PanelPadding, 0f);
            int closeButtonX = PanelWidth - PanelPadding - CloseButtonSize.X;
            CloseButtonHost.Position = new float3(closeButtonX, PanelPadding, 0f);
        }

        /// <summary>
        /// Positions each visible platform row.
        /// </summary>
        void LayoutPlatformRows() {
            int rowsTop = PanelPadding + GetTextHeight() + SectionSpacing;
            int checkBoxX = PanelWidth - PanelPadding - CheckBoxSize.X;
            int labelYAdjust = Math.Max(0, (PlatformRowHeight - GetTextHeight()) / 2);
            int checkBoxYAdjust = Math.Max(0, (PlatformRowHeight - CheckBoxSize.Y) / 2);

            for (int index = 0; index < PlatformLabelHosts.Count; index++) {
                int rowTop = rowsTop + (PlatformRowHeight * index);
                PlatformLabelHosts[index].Position = new float3(PanelPadding, rowTop + labelYAdjust, 0f);
                PlatformCheckBoxHosts[index].Position = new float3(checkBoxX, rowTop + checkBoxYAdjust, 0f);
            }
        }

        /// <summary>
        /// Positions the validation and empty-state text.
        /// </summary>
        void LayoutStatus() {
            int rowsTop = PanelPadding + GetTextHeight() + SectionSpacing;
            int rowsHeight = PlatformRowHeight * Math.Max(1, AvailablePlatforms.Count);
            int statusTop = rowsTop + rowsHeight + SectionSpacing;
            StatusHost.Position = new float3(PanelPadding, statusTop, 0f);
        }

        /// <summary>
        /// Positions the footer buttons.
        /// </summary>
        void LayoutButtons() {
            int footerTop = PanelHeight - PanelPadding - FooterHeight;
            int cancelX = PanelWidth - PanelPadding - SaveButtonSize.X - SectionSpacing - CancelButtonSize.X;
            int saveX = PanelWidth - PanelPadding - SaveButtonSize.X;
            int buttonY = footerTop + Math.Max(0, (FooterHeight - SaveButtonSize.Y) / 2);

            CancelButtonHost.Position = new float3(cancelX, buttonY, 0f);
            SaveButtonHost.Position = new float3(saveX, buttonY, 0f);
        }

        /// <summary>
        /// Gets the line height used by dialog text rows.
        /// </summary>
        /// <returns>Dialog text line height in pixels.</returns>
        int GetTextHeight() {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(Font.LineHeight, 1f)));
        }
    }
}
