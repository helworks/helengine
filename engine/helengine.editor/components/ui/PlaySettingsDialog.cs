using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Modal dialog that edits the Play settings of the selected Play platform: a Common section and a section for the
    /// platform's own Play settings, rendered with the same rows and controls as the profiles dialog. Presentation only:
    /// the values, validation, reset and save live in <see cref="EditorPlaySettingsDialogController"/>. The shared rows are
    /// bound to throwaway copies so they can never coerce a stored value; every control change is routed to the controller,
    /// a stored choice the declarations reject is shown blank with its label highlighted, and the status line describes the
    /// rejected values, the stored ids Save removes, and a save failure naming play.json.
    /// </summary>
    public class PlaySettingsDialog : EditorDialogBase {
        /// <summary>Unscaled panel width.</summary>
        public const int PanelWidth = 540;

        /// <summary>Unscaled minimum panel height.</summary>
        public const int PanelHeight = 440;

        /// <summary>Unscaled padding inside the panel.</summary>
        public const int PanelPadding = 16;

        /// <summary>Unscaled height of the draggable title bar.</summary>
        public const int HeaderHeight = 32;

        /// <summary>Unscaled height reserved for the footer buttons.</summary>
        public const int FooterHeight = 28;

        /// <summary>Unscaled spacing between the sections, the status line and the footer.</summary>
        public const int SectionSpacing = 12;

        /// <summary>Unscaled height of the status text area.</summary>
        public const int StatusHeight = 36;

        /// <summary>Unscaled label column width of the setting rows.</summary>
        const int LabelColumnWidth = 200;

        /// <summary>Unscaled value column width of the setting rows.</summary>
        const int ValueColumnWidth = 280;

        /// <summary>Unscaled gap between neighbouring footer buttons.</summary>
        const int ButtonSpacing = 8;

        /// <summary>Unscaled size of the Save and Cancel buttons.</summary>
        static readonly int2 ButtonSize = new int2(88, 22);

        /// <summary>Unscaled size of the Reset Invalid button.</summary>
        static readonly int2 ResetButtonSize = new int2(112, 22);

        /// <summary>Host of the common section.</summary>
        readonly EditorEntity CommonContentHost;

        /// <summary>Rows of the common Play settings.</summary>
        readonly EditorPlatformSettingsSection CommonSection;

        /// <summary>Host of the platform section.</summary>
        readonly EditorEntity PlatformContentHost;

        /// <summary>Rows of the platform's own Play settings.</summary>
        readonly EditorPlatformSettingsSection PlatformSection;

        /// <summary>Host of the status text.</summary>
        readonly EditorEntity StatusHost;

        /// <summary>Status text describing rejected values, stored ids Save removes and save failures.</summary>
        readonly TextComponent StatusText;

        /// <summary>Host of the Reset Invalid button.</summary>
        readonly EditorEntity ResetButtonHost;

        /// <summary>Button that puts every rejected value back to its declared default.</summary>
        readonly ButtonComponent ResetButton;

        /// <summary>Host of the Save button.</summary>
        readonly EditorEntity SaveButtonHost;

        /// <summary>Button that asks the session to save the settings.</summary>
        readonly ButtonComponent SaveButton;

        /// <summary>Host of the Cancel button.</summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>Button that closes the dialog without saving.</summary>
        readonly ButtonComponent CancelButton;

        /// <summary>Controller of the open dialog; null while hidden.</summary>
        EditorPlaySettingsDialogController Controller;

        /// <summary>Whether the controls are being set from the controller, so their change events are not user edits.</summary>
        bool IsSyncingControls;

        /// <summary>Whether construction finished, so layout requests are honoured.</summary>
        bool IsDialogConstructed;

        /// <summary>Raised with the model when the user presses Save; the handler saves through <see cref="TrySave"/>.</summary>
        public event Action<EditorPlaySettingsDialogModel> SaveRequested;

        /// <summary>Raised when the user cancels or closes the dialog.</summary>
        public event Action CancelRequested;

        /// <summary>
        /// Creates the hidden dialog.
        /// </summary>
        /// <param name="ownerCore">Core that owns the dialog entities.</param>
        /// <param name="interactionServices">Editor interaction services.</param>
        /// <param name="font">Dialog font.</param>
        /// <param name="metrics">Scaled editor UI metrics.</param>
        public PlaySettingsDialog(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font, EditorUiMetrics metrics)
            : base(ownerCore, interactionServices, "PlaySettingsDialog", "Play Settings", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            CommonContentHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CommonContentHost);
            CommonSection = new EditorPlatformSettingsSection(CommonContentHost, LayerMask, DialogFont, DialogMetrics.ScalePixels(LabelColumnWidth), DialogMetrics.ScalePixels(ValueColumnWidth));

            PlatformContentHost = CreateDialogHost();
            DialogPanelRoot.AddChild(PlatformContentHost);
            PlatformSection = new EditorPlatformSettingsSection(PlatformContentHost, LayerMask, DialogFont, DialogMetrics.ScalePixels(LabelColumnWidth), DialogMetrics.ScalePixels(ValueColumnWidth));

            StatusHost = CreateDialogHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, DialogMetrics.ScalePixels(StatusHeight))
            };
            StatusHost.AddComponent(StatusText);

            ResetButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(ResetButtonHost);
            ResetButton = new ButtonComponent("Reset Invalid", ScaleSize(ResetButtonSize), DialogFont, HandleResetClicked, 0f);
            ResetButtonHost.AddComponent(ResetButton);

            SaveButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", ScaleSize(ButtonSize), DialogFont, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);

            CancelButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", ScaleSize(ButtonSize), DialogFont, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);

            Enabled = false;
            IsDialogConstructed = true;
        }

        /// <summary>
        /// Shows the dialog for one platform's Play settings, highlighting stored values the declarations reject.
        /// </summary>
        /// <param name="model">View-model of the platform's Play settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public void Show(EditorPlaySettingsDialogModel model) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }

            Controller = new EditorPlaySettingsDialogController(model);
            CommonSection.Rebuild("Common", model.CommonDefinitions, Controller.CreateBindingValues(EditorPlaySettingScope.Common));
            PlatformSection.Rebuild(
                model.PlatformDefinitions.Length == 0 ? model.PlatformDisplayName + " (no platform Play settings)" : model.PlatformDisplayName,
                model.PlatformDefinitions,
                Controller.CreateBindingValues(EditorPlaySettingScope.Platform));
            SyncControls();
            AttachRowHandlers(CommonSection, EditorPlaySettingScope.Common);
            AttachRowHandlers(PlatformSection, EditorPlaySettingScope.Platform);
            RefreshPresentation();
            SetDialogSize(DialogWidth, ComputePanelHeightPixels());
            ResetDialogPositioning();
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog and forgets the controller.
        /// </summary>
        public void Hide() {
            Controller = null;
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Saves the open dialog's values through the store and presents the outcome: a rejected value or a file that
        /// cannot be written (named in the status line) leaves the dialog open.
        /// </summary>
        /// <param name="store">Store of the file the dialog was opened on.</param>
        /// <returns>True when the settings were saved.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the dialog is not open.</exception>
        public bool TrySave(EditorPlaySettingsStore store) {
            if (Controller == null) {
                throw new InvalidOperationException("The Play Settings dialog is not open.");
            }

            bool saved = Controller.TrySave(store);
            RefreshPresentation();
            return saved;
        }

        /// <summary>
        /// Updates sizing and layout for the current editor window size.
        /// </summary>
        /// <param name="windowWidth">Editor window width.</param>
        /// <param name="windowHeight">Editor window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsDialogConstructed) {
                return;
            }

            UpdateDialogFrame(windowWidth, windowHeight);
        }

        /// <summary>Treats the close button as Cancel.</summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>Repositions the content when the shell moves or resizes.</summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutContent();
        }

        /// <summary>
        /// Positions the two sections, the status text and the footer buttons.
        /// </summary>
        void LayoutContent() {
            int padding = DialogMetrics.ScalePixels(PanelPadding);
            int spacing = DialogMetrics.ScalePixels(SectionSpacing);
            float commonTop = DialogMetrics.ScalePixels(HeaderHeight) + padding;
            CommonContentHost.Position = new float3(padding, commonTop, 0.1f);
            CommonSection.Root.Position = float3.Zero;
            CommonSection.Layout();
            float platformTop = commonTop + CommonSection.ContentHeight + spacing;
            PlatformContentHost.Position = new float3(padding, platformTop, 0.1f);
            PlatformSection.Root.Position = float3.Zero;
            PlatformSection.Layout();
            float statusTop = platformTop + PlatformSection.ContentHeight + spacing;
            StatusHost.Position = new float3(padding, statusTop, 0f);
            StatusText.Size = new int2(Math.Max(1, AnchorBounds.X - padding * 2), DialogMetrics.ScalePixels(StatusHeight));
            int footerY = AnchorBounds.Y - padding - DialogMetrics.ScalePixels(FooterHeight);
            int buttonSpacing = DialogMetrics.ScalePixels(ButtonSpacing);
            int cancelX = AnchorBounds.X - padding - ScaleSize(ButtonSize).X;
            int saveX = cancelX - buttonSpacing - ScaleSize(ButtonSize).X;
            ResetButtonHost.Position = new float3(padding, footerY, 0f);
            SaveButtonHost.Position = new float3(saveX, footerY, 0f);
            CancelButtonHost.Position = new float3(cancelX, footerY, 0f);
        }

        /// <summary>
        /// Computes the panel height that fits both sections, the status text and the footer.
        /// </summary>
        /// <returns>Panel height in pixels.</returns>
        int ComputePanelHeightPixels() {
            float height = DialogMetrics.ScalePixels(HeaderHeight)
                + DialogMetrics.ScalePixels(PanelPadding) * 2
                + CommonSection.ContentHeight
                + PlatformSection.ContentHeight
                + DialogMetrics.ScalePixels(SectionSpacing) * 3
                + DialogMetrics.ScalePixels(StatusHeight)
                + DialogMetrics.ScalePixels(FooterHeight);
            return (int)Math.Ceiling(height);
        }

        /// <summary>
        /// Routes every row's control changes to the controller. The rows are rebuilt on each show, so the handlers go
        /// away with the rows' controls.
        /// </summary>
        /// <param name="section">Section whose rows are wired.</param>
        /// <param name="scope">Scope of the section.</param>
        void AttachRowHandlers(EditorPlatformSettingsSection section, EditorPlaySettingScope scope) {
            for (int index = 0; index < section.Items.Count; index++) {
                EditorPlatformSettingBinding row = section.Items[index];
                string settingId = row.Setting.SettingId;
                if (row.ComboBox != null) {
                    row.ComboBox.SelectionChanged += (selectedIndex, value) => HandleRowValueEdited(scope, settingId, value);
                } else if (row.CheckBox != null) {
                    row.CheckBox.CheckedChanged += (checkBox, isChecked) => HandleRowCheckedEdited(scope, settingId, isChecked);
                } else if (row.TextBox != null) {
                    row.TextBox.TextChanged += textBox => HandleRowValueEdited(scope, settingId, textBox.Text);
                }
            }
        }

        /// <summary>
        /// Sets every row's control from the controller's values. A choice the declarations reject is shown with no
        /// selection instead of an allowed value it does not hold.
        /// </summary>
        void SyncControls() {
            IsSyncingControls = true;
            try {
                SyncSectionControls(CommonSection, EditorPlaySettingScope.Common);
                SyncSectionControls(PlatformSection, EditorPlaySettingScope.Platform);
            } finally {
                IsSyncingControls = false;
            }
        }

        /// <summary>
        /// Sets one section's controls from the controller's values.
        /// </summary>
        /// <param name="section">Section whose controls are set.</param>
        /// <param name="scope">Scope of the section.</param>
        void SyncSectionControls(EditorPlatformSettingsSection section, EditorPlaySettingScope scope) {
            for (int index = 0; index < section.Items.Count; index++) {
                EditorPlatformSettingBinding row = section.Items[index];
                string value = Controller.GetValue(scope, row.Setting.SettingId);
                if (row.ComboBox != null) {
                    row.ComboBox.SelectedIndex = Array.IndexOf(row.Setting.AllowedValues, value);
                } else if (row.CheckBox != null) {
                    row.CheckBox.IsChecked = string.Equals(value, "true", StringComparison.Ordinal);
                } else if (row.TextBox != null) {
                    row.TextBox.Text = value;
                }
            }
        }

        /// <summary>
        /// Colours each row label by whether its current value is rejected and shows the controller's status line.
        /// </summary>
        void RefreshPresentation() {
            RefreshSectionLabels(CommonSection, EditorPlaySettingScope.Common);
            RefreshSectionLabels(PlatformSection, EditorPlaySettingScope.Platform);
            StatusText.Text = Controller.StatusText;
        }

        /// <summary>
        /// Colours one section's row labels by whether their current value is rejected.
        /// </summary>
        /// <param name="section">Section whose labels are coloured.</param>
        /// <param name="scope">Scope of the section.</param>
        void RefreshSectionLabels(EditorPlatformSettingsSection section, EditorPlaySettingScope scope) {
            for (int index = 0; index < section.Items.Count; index++) {
                EditorPlatformSettingBinding row = section.Items[index];
                row.LabelText.Color = Controller.IsRowFlagged(scope, row.Setting.SettingId)
                    ? ThemeManager.Colors.StateWarning
                    : ThemeManager.Colors.InputForegroundPrimary;
            }
        }

        /// <summary>
        /// Applies a picked or typed value to the controller and refreshes the highlights and status line.
        /// </summary>
        /// <param name="scope">Section of the edited row.</param>
        /// <param name="settingId">Setting id of the edited row.</param>
        /// <param name="value">New value.</param>
        void HandleRowValueEdited(EditorPlaySettingScope scope, string settingId, string value) {
            if (IsSyncingControls || Controller == null) {
                return;
            }

            Controller.SetValue(scope, settingId, value);
            RefreshPresentation();
        }

        /// <summary>
        /// Applies a checkbox state to the controller (stored as true/false) and refreshes the highlights and status line.
        /// </summary>
        /// <param name="scope">Section of the edited row.</param>
        /// <param name="settingId">Setting id of the edited row.</param>
        /// <param name="isChecked">New checkbox state.</param>
        void HandleRowCheckedEdited(EditorPlaySettingScope scope, string settingId, bool isChecked) {
            if (IsSyncingControls || Controller == null) {
                return;
            }

            Controller.SetBooleanValue(scope, settingId, isChecked);
            RefreshPresentation();
        }

        /// <summary>
        /// Puts every rejected value back to its declared default and shows the result.
        /// </summary>
        void HandleResetClicked() {
            if (Controller == null) {
                return;
            }

            Controller.ResetInvalidToDefaults();
            SyncControls();
            RefreshPresentation();
        }

        /// <summary>Raises <see cref="SaveRequested"/> for the open dialog.</summary>
        void HandleSaveClicked() {
            if (Controller == null) {
                return;
            }

            SaveRequested?.Invoke(Controller.Model);
        }

        /// <summary>Raises <see cref="CancelRequested"/>.</summary>
        void HandleCancelClicked() {
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// Creates one dialog content host.
        /// </summary>
        /// <returns>Host entity.</returns>
        EditorEntity CreateDialogHost() {
            return new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Scales an unscaled button size by the dialog metrics.
        /// </summary>
        /// <param name="size">Unscaled size.</param>
        /// <returns>Size in pixels.</returns>
        int2 ScaleSize(int2 size) {
            return new int2(DialogMetrics.ScalePixels(size.X), DialogMetrics.ScalePixels(size.Y));
        }
    }
}
