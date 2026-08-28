namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to edit project-defined build environments.
    /// </summary>
    public sealed class EnvironmentsDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 500;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 430;

        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Height reserved for each environment row.
        /// </summary>
        public const int EnvironmentRowHeight = 26;

        /// <summary>
        /// Number of environment rows visible before the list scrolls.
        /// </summary>
        public const int EnvironmentVisibleRowCount = 8;

        /// <summary>
        /// Width of the environment list scrollbar.
        /// </summary>
        public const int ListScrollBarWidth = 8;

        /// <summary>
        /// Width reserved for the list scrollbar and its gap.
        /// </summary>
        public const int ListScrollBarGutter = 14;

        /// <summary>
        /// Host for the environment section label.
        /// </summary>
        readonly EditorEntity EnvironmentsLabelHost;

        /// <summary>
        /// Label above the environment list.
        /// </summary>
        readonly TextComponent EnvironmentsLabelText;

        /// <summary>
        /// Scrollable environment list root.
        /// </summary>
        readonly EditorEntity EnvironmentListRoot;

        /// <summary>
        /// Scroll controller for the environment list.
        /// </summary>
        readonly ScrollComponent EnvironmentListScrollComponent;
        /// <summary>
        /// Object manager owned by the editor session hosting this dialog.
        /// </summary>
        ObjectManager ObjectManager;

        /// <summary>
        /// Scrollbar for the environment list.
        /// </summary>
        readonly ScrollBarComponent EnvironmentListScrollBar;

        /// <summary>
        /// Pooled environment rows.
        /// </summary>
        readonly List<EnvironmentsDialogRow> EnvironmentRows;

        /// <summary>
        /// Text field used by Add and Rename.
        /// </summary>
        readonly TextBoxComponent EnvironmentIdTextBox;

        /// <summary>
        /// Add action button.
        /// </summary>
        readonly ButtonComponent AddButton;

        /// <summary>
        /// Rename action button.
        /// </summary>
        readonly ButtonComponent RenameButton;

        /// <summary>
        /// Delete action button.
        /// </summary>
        readonly ButtonComponent DeleteButton;

        /// <summary>
        /// Status text shown for validation and delete confirmation.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Cancel button.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// Save button.
        /// </summary>
        readonly ButtonComponent SaveButton;

        /// <summary>
        /// Service used for normalized document mutations.
        /// </summary>
        readonly EditorProjectEnvironmentsService EnvironmentService;

        /// <summary>
        /// Working copy shown and edited by the dialog.
        /// </summary>
        EditorProjectEnvironmentsDocument WorkingDocument;

        /// <summary>
        /// Currently selected environment index, or -1 when none is selected.
        /// </summary>
        int SelectedEnvironmentIndex;

        /// <summary>
        /// Environment awaiting the second delete click for confirmation.
        /// </summary>
        string PendingDeleteEnvironmentId;

        /// <summary>
        /// Raised when the user confirms the edited environment registry.
        /// </summary>
        public event Action<EnvironmentsDialogSelection> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the environment registry workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes an environment dialog using a non-persisting mutation service for isolated callers.
        /// </summary>
        /// <param name="font">Font used for labels and controls.</param>
        public EnvironmentsDialog(FontAsset font) : this(font, new EditorProjectEnvironmentsService(Path.GetTempPath()), EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes an environment dialog using a project environment service.
        /// </summary>
        /// <param name="font">Font used for labels and controls.</param>
        /// <param name="environmentService">Project environment service used for mutations.</param>
        /// <param name="metrics">Scaled editor UI metrics.</param>
        public EnvironmentsDialog(FontAsset font, EditorProjectEnvironmentsService environmentService, EditorUiMetrics metrics)
            : base("EnvironmentsDialog", "Environments", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (environmentService == null) {
                throw new ArgumentNullException(nameof(environmentService));
            }

            EnvironmentService = environmentService;
            DialogIsResizable = false;
            SetDialogMinimumSize(PanelWidth, PanelHeight);
            EnvironmentRows = new List<EnvironmentsDialogRow>(EnvironmentVisibleRowCount);
            SelectedEnvironmentIndex = -1;
            PendingDeleteEnvironmentId = string.Empty;

            EnvironmentsLabelHost = CreateInternalHost();
            DialogPanelRoot.AddChild(EnvironmentsLabelHost);
            EnvironmentsLabelText = CreateLabelText("Project environments");
            EnvironmentsLabelHost.AddComponent(EnvironmentsLabelText);

            EnvironmentListRoot = CreateInternalHost();
            DialogPanelRoot.AddChild(EnvironmentListRoot);
            EnvironmentListScrollComponent = new ScrollComponent();
            EnvironmentListScrollComponent.ScrollOffsetChanged += HandleEnvironmentListScrollOffsetChanged;
            EnvironmentListRoot.AddComponent(EnvironmentListScrollComponent);

            EnvironmentListScrollBar = new ScrollBarComponent(new int2(GetListScrollBarWidthPixels(), GetEnvironmentListViewportHeightPixels()));
            EnvironmentListScrollBar.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            EditorEntity scrollBarHost = CreateInternalHost();
            DialogPanelRoot.AddChild(scrollBarHost);
            scrollBarHost.AddComponent(EnvironmentListScrollBar);
            EnvironmentListScrollBar.Target = EnvironmentListScrollComponent;

            EditorEntity inputHost = CreateInternalHost();
            DialogPanelRoot.AddChild(inputHost);
            EnvironmentIdTextBox = new TextBoxComponent(GetEnvironmentIdTextBoxSize(), DialogFont, "Environment id");
            EnvironmentIdTextBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            inputHost.AddComponent(EnvironmentIdTextBox);

            AddButton = CreateActionButton("Add", HandleAddClicked);
            RenameButton = CreateActionButton("Rename", HandleRenameClicked);
            DeleteButton = CreateActionButton("Delete", HandleDeleteClicked);
            AddButtonHost = AddButton.Parent as EditorEntity;
            RenameButtonHost = RenameButton.Parent as EditorEntity;
            DeleteButtonHost = DeleteButton.Parent as EditorEntity;

            StatusHost = CreateInternalHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, GetDialogLineHeight()),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            EditorEntity footerHost = CreateInternalHost();
            DialogPanelRoot.AddChild(footerHost);
            CancelButton = new ButtonComponent("Cancel", GetFooterButtonSize(), DialogFont, HandleCancelClicked, 0f);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            footerHost.AddChild(CreateInternalHost());
            footerHost.AddComponent(CancelButton);

            EditorEntity saveHost = CreateInternalHost();
            DialogPanelRoot.AddChild(saveHost);
            SaveButton = new ButtonComponent("Save", GetFooterButtonSize(), DialogFont, HandleSaveClicked, 0f);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            saveHost.AddComponent(SaveButton);

            Enabled = false;
            IsInitialized = true;
            LayoutContent();
        }

        /// <summary>
        /// Binds list scrolling to the owning session's object manager.
        /// </summary>
        internal void SetObjectManager(ObjectManager objectManager) {
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            EnvironmentListScrollComponent.UpdateOrder = ObjectManager.GetUpdateOrderForLayer(1);
        }

        /// <summary>
        /// Host for the Add button.
        /// </summary>
        readonly EditorEntity AddButtonHost;

        /// <summary>
        /// Host for the Rename button.
        /// </summary>
        readonly EditorEntity RenameButtonHost;

        /// <summary>
        /// Host for the Delete button.
        /// </summary>
        readonly EditorEntity DeleteButtonHost;

        /// <summary>
        /// Host for status text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Tracks whether the dialog finished initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Shows the dialog using a copied environment registry document.
        /// </summary>
        /// <param name="document">Environment document to edit.</param>
        public void Show(EditorProjectEnvironmentsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            WorkingDocument = CloneDocument(document);
            SelectedEnvironmentIndex = WorkingDocument.Environments.Count > 0 ? 0 : -1;
            PendingDeleteEnvironmentId = string.Empty;
            EnvironmentIdTextBox.Text = string.Empty;
            StatusText.Text = string.Empty;
            ResetDialogPositioning();
            Enabled = true;
            UpdateEnvironmentRowsLayout();
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog and clears transient state.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
            WorkingDocument = null;
            SelectedEnvironmentIndex = -1;
            PendingDeleteEnvironmentId = string.Empty;
            EnvironmentIdTextBox.Text = string.Empty;
            StatusText.Text = string.Empty;
            DisableAllEnvironmentRows();
            EnvironmentListScrollComponent.ItemCount = 0;
            EnvironmentListScrollComponent.ResetScrollOffset();
        }

        /// <summary>
        /// Updates dialog sizing and layout for the host window.
        /// </summary>
        /// <param name="windowWidth">Current host width.</param>
        /// <param name="windowHeight">Current host height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized || !UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }

            LayoutContent();
        }

        /// <summary>
        /// Handles selection of one pooled environment row.
        /// </summary>
        /// <param name="button">Button that was activated.</param>
        void HandleEnvironmentRowClicked(ButtonComponent button) {
            EnvironmentsDialogRow row = EnvironmentRows.FirstOrDefault(candidate => candidate.SelectButton == button);
            if (row == null || row.EnvironmentIndex < 0 || WorkingDocument == null) {
                return;
            }

            SelectedEnvironmentIndex = row.EnvironmentIndex;
            EnvironmentIdTextBox.Text = row.EnvironmentId;
            PendingDeleteEnvironmentId = string.Empty;
            StatusText.Text = string.Empty;
        }

        /// <summary>
        /// Adds the entered custom environment to the working document.
        /// </summary>
        void HandleAddClicked() {
            if (WorkingDocument == null) {
                return;
            }

            try {
                EnvironmentService.Add(WorkingDocument, EnvironmentIdTextBox.Text);
                SelectedEnvironmentIndex = WorkingDocument.Environments.Count - 1;
                EnvironmentIdTextBox.Text = string.Empty;
                PendingDeleteEnvironmentId = string.Empty;
                StatusText.Text = string.Empty;
                UpdateEnvironmentRowsLayout();
            } catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) {
                StatusText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Renames the selected custom environment to the entered identifier.
        /// </summary>
        void HandleRenameClicked() {
            if (!TryGetSelectedEnvironment(out EditorProjectEnvironmentDefinition environment)) {
                StatusText.Text = "Select an environment to rename.";
                return;
            }

            try {
                string previousId = environment.Id;
                EnvironmentService.Rename(WorkingDocument, previousId, EnvironmentIdTextBox.Text);
                EnvironmentIdTextBox.Text = string.Empty;
                PendingDeleteEnvironmentId = string.Empty;
                StatusText.Text = string.Empty;
                UpdateEnvironmentRowsLayout();
            } catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) {
                StatusText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Deletes the selected custom environment after a repeated confirmation click.
        /// </summary>
        void HandleDeleteClicked() {
            if (!TryGetSelectedEnvironment(out EditorProjectEnvironmentDefinition environment)) {
                StatusText.Text = "Select an environment to delete.";
                return;
            }

            if (!string.Equals(PendingDeleteEnvironmentId, environment.Id, StringComparison.OrdinalIgnoreCase)) {
                PendingDeleteEnvironmentId = environment.Id;
                StatusText.Text = $"Click Delete again to remove '{environment.Id}'.";
                return;
            }

            try {
                EnvironmentService.Delete(WorkingDocument, environment.Id);
                SelectedEnvironmentIndex = Math.Min(SelectedEnvironmentIndex, WorkingDocument.Environments.Count - 1);
                PendingDeleteEnvironmentId = string.Empty;
                EnvironmentIdTextBox.Text = string.Empty;
                StatusText.Text = string.Empty;
                UpdateEnvironmentRowsLayout();
            } catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) {
                PendingDeleteEnvironmentId = string.Empty;
                StatusText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Raises the cancel workflow.
        /// </summary>
        void HandleCancelClicked() {
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// Raises the confirmed working document.
        /// </summary>
        void HandleSaveClicked() {
            if (WorkingDocument == null) {
                return;
            }

            if (WorkingDocument.Environments.Count == 0) {
                StatusText.Text = "At least one environment is required.";
                return;
            }

            StatusText.Text = string.Empty;
            ConfirmRequested?.Invoke(new EnvironmentsDialogSelection(CloneDocument(WorkingDocument)));
        }

        /// <summary>
        /// Returns the selected environment from the working document.
        /// </summary>
        /// <param name="environment">Selected environment when one exists.</param>
        /// <returns>True when a selected environment exists.</returns>
        bool TryGetSelectedEnvironment(out EditorProjectEnvironmentDefinition environment) {
            environment = null;
            if (WorkingDocument == null || SelectedEnvironmentIndex < 0 || SelectedEnvironmentIndex >= WorkingDocument.Environments.Count) {
                return false;
            }

            environment = WorkingDocument.Environments[SelectedEnvironmentIndex];
            return environment != null;
        }

        /// <summary>
        /// Ensures the pooled row list contains enough rows for the visible list.
        /// </summary>
        /// <param name="count">Minimum row count.</param>
        void EnsureEnvironmentRowPool(int count) {
            for (int index = EnvironmentRows.Count; index < count; index++) {
                EnvironmentsDialogRow row = new EnvironmentsDialogRow(
                    DialogFont,
                    LayerMask,
                    GetEnvironmentRowButtonSize(),
                    DialogTextOrder,
                    HandleEnvironmentRowClicked);
                EnvironmentListRoot.AddChild(row.SelectHost);
                EnvironmentListRoot.AddChild(row.ProtectedHost);
                EnvironmentRows.Add(row);
            }
        }

        /// <summary>
        /// Lays out visible environment rows for the current scroll position.
        /// </summary>
        void UpdateEnvironmentRowsLayout() {
            if (WorkingDocument == null) {
                DisableAllEnvironmentRows();
                return;
            }

            int contentWidth = GetEnvironmentListContentWidth();
            int contentHeight = GetEnvironmentListViewportHeightPixels();
            EditorScrollComponentLayout.ConfigureAutomaticVisibleItems(
                EnvironmentListScrollComponent,
                new int2(contentWidth, contentHeight),
                GetEnvironmentRowHeightPixels(),
                WorkingDocument.Environments.Count);
            EnvironmentListScrollComponent.ClampScrollOffset();
            EnvironmentListScrollBar.Refresh();

            int visibleRowCount = EnvironmentListScrollComponent.VisibleItemCount;
            EnsureEnvironmentRowPool(visibleRowCount);
            int scrollOffset = EnvironmentListScrollComponent.ScrollOffset;
            int rowHeight = GetEnvironmentRowHeightPixels();
            int markerLeft = Math.Max(0, contentWidth - DialogMetrics.ScalePixels(72));

            for (int rowIndex = 0; rowIndex < EnvironmentRows.Count; rowIndex++) {
                EnvironmentsDialogRow row = EnvironmentRows[rowIndex];
                int environmentIndex = scrollOffset + rowIndex;
                if (rowIndex >= visibleRowCount || environmentIndex >= WorkingDocument.Environments.Count) {
                    DisableEnvironmentRow(row);
                    continue;
                }

                EditorProjectEnvironmentDefinition environment = WorkingDocument.Environments[environmentIndex];
                row.EnvironmentIndex = environmentIndex;
                row.EnvironmentId = environment.Id;
                row.IsProtected = environment.IsProtected;
                row.SelectButton.SetText(environment.Id);
                row.ProtectedText.Text = environment.IsProtected ? "protected" : string.Empty;
                row.SelectHost.Enabled = true;
                row.ProtectedHost.Enabled = environment.IsProtected;
                row.SelectHost.Position = new float3(0, rowIndex * rowHeight, 0.1f);
                row.ProtectedHost.Position = new float3(markerLeft, rowIndex * rowHeight, 0.1f);
                row.ProtectedText.Size = new int2(contentWidth - markerLeft, rowHeight);
            }
        }

        /// <summary>
        /// Handles environment-list scroll changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll component that changed.</param>
        /// <param name="scrollOffset">New scroll offset.</param>
        void HandleEnvironmentListScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            UpdateEnvironmentRowsLayout();
        }

        /// <summary>
        /// Disables one pooled environment row.
        /// </summary>
        /// <param name="row">Row to disable.</param>
        void DisableEnvironmentRow(EnvironmentsDialogRow row) {
            row.EnvironmentIndex = -1;
            row.EnvironmentId = string.Empty;
            row.IsProtected = false;
            row.SelectHost.Enabled = false;
            row.ProtectedHost.Enabled = false;
        }

        /// <summary>
        /// Disables every pooled environment row.
        /// </summary>
        void DisableAllEnvironmentRows() {
            for (int index = 0; index < EnvironmentRows.Count; index++) {
                DisableEnvironmentRow(EnvironmentRows[index]);
            }
        }

        /// <summary>
        /// Configures dialog-owned layout positions and sizes.
        /// </summary>
        void LayoutContent() {
            int contentLeft = GetPanelPaddingPixels();
            int contentWidth = GetContentWidth();
            int labelTop = DialogMetrics.ScalePixels(48);
            int listTop = labelTop + GetDialogLineHeight() + GetSectionSpacingPixels();
            EnvironmentsLabelHost.Position = new float3(contentLeft, labelTop, 0.1f);
            EnvironmentsLabelText.Size = new int2(contentWidth, GetDialogLineHeight());
            EnvironmentListRoot.Position = new float3(contentLeft, listTop, 0.1f);

            int scrollBarWidth = GetListScrollBarWidthPixels();
            EditorEntity scrollBarHost = EnvironmentListScrollBar.Parent as EditorEntity;
            scrollBarHost.Position = new float3(contentLeft + contentWidth - scrollBarWidth, listTop, 0.1f);
            EnvironmentListScrollBar.Size = new int2(scrollBarWidth, GetEnvironmentListViewportHeightPixels());

            int inputTop = listTop + GetEnvironmentListViewportHeightPixels() + GetSectionSpacingPixels();
            EnvironmentIdTextBox.Parent.Position = new float3(contentLeft, inputTop, 0.1f);
            EnvironmentIdTextBox.Size = GetEnvironmentIdTextBoxSize();

            int actionTop = inputTop + GetEnvironmentIdTextBoxSize().Y + GetSectionSpacingPixels();
            AddButtonHost.Position = new float3(contentLeft, actionTop, 0.1f);
            RenameButtonHost.Position = new float3(contentLeft + GetActionButtonSize().X + GetSectionSpacingPixels(), actionTop, 0.1f);
            DeleteButtonHost.Position = new float3(contentLeft + ((GetActionButtonSize().X + GetSectionSpacingPixels()) * 2), actionTop, 0.1f);

            int footerTop = DialogHeight - GetPanelPaddingPixels() - GetFooterButtonSize().Y;
            StatusHost.Position = new float3(contentLeft, footerTop - GetDialogLineHeight() - GetSectionSpacingPixels(), 0.1f);
            StatusText.Size = new int2(contentWidth, GetDialogLineHeight());
            SaveButton.Parent.Position = new float3(DialogWidth - GetPanelPaddingPixels() - GetFooterButtonSize().X, footerTop, 0.1f);
            CancelButton.Parent.Position = new float3(DialogWidth - GetPanelPaddingPixels() - (GetFooterButtonSize().X * 2) - GetSectionSpacingPixels(), footerTop, 0.1f);
            UpdateEnvironmentRowsLayout();
        }

        /// <summary>
        /// Creates one dialog-owned action button and host.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <param name="onClick">Button callback.</param>
        /// <returns>Created action button.</returns>
        ButtonComponent CreateActionButton(string label, Action onClick) {
            EditorEntity host = CreateInternalHost();
            DialogPanelRoot.AddChild(host);
            ButtonComponent button = new ButtonComponent(label, GetActionButtonSize(), DialogFont, onClick, 0f);
            button.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            host.AddComponent(button);
            return button;
        }

        /// <summary>
        /// Creates one internal dialog host entity.
        /// </summary>
        /// <returns>Dialog-owned internal host.</returns>
        EditorEntity CreateInternalHost() {
            return new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Creates one standard dialog label.
        /// </summary>
        /// <param name="text">Initial label text.</param>
        /// <returns>Configured label.</returns>
        TextComponent CreateLabelText(string text) {
            return new TextComponent {
                Font = DialogFont,
                Text = text,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
        }

        /// <summary>
        /// Clones one environment document for modal editing.
        /// </summary>
        /// <param name="document">Document to clone.</param>
        /// <returns>Independent document copy.</returns>
        static EditorProjectEnvironmentsDocument CloneDocument(EditorProjectEnvironmentsDocument document) {
            return new EditorProjectEnvironmentsDocument {
                Environments = document.Environments == null
                    ? []
                    : document.Environments.Where(environment => environment != null).Select(environment => new EditorProjectEnvironmentDefinition {
                        Id = environment.Id,
                        IsProtected = environment.IsProtected
                    }).ToList()
            };
        }

        /// <summary>
        /// Gets the scaled content width.
        /// </summary>
        int GetContentWidth() {
            return DialogWidth - (GetPanelPaddingPixels() * 2);
        }

        /// <summary>
        /// Gets scaled panel padding.
        /// </summary>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(16);
        }

        /// <summary>
        /// Gets scaled section spacing.
        /// </summary>
        int GetSectionSpacingPixels() {
            return DialogMetrics.ScalePixels(10);
        }

        /// <summary>
        /// Gets the scaled dialog line height.
        /// </summary>
        int GetDialogLineHeight() {
            return DialogMetrics.ScalePixels(18);
        }

        /// <summary>
        /// Gets scaled environment row height.
        /// </summary>
        int GetEnvironmentRowHeightPixels() {
            return DialogMetrics.ScalePixels(EnvironmentRowHeight);
        }

        /// <summary>
        /// Gets scaled environment-list viewport height.
        /// </summary>
        int GetEnvironmentListViewportHeightPixels() {
            return GetEnvironmentRowHeightPixels() * EnvironmentVisibleRowCount;
        }

        /// <summary>
        /// Gets scaled environment list content width excluding scrollbar gutter.
        /// </summary>
        int GetEnvironmentListContentWidth() {
            return GetContentWidth() - DialogMetrics.ScalePixels(ListScrollBarGutter);
        }

        /// <summary>
        /// Gets scaled list scrollbar width.
        /// </summary>
        int GetListScrollBarWidthPixels() {
            return DialogMetrics.ScalePixels(ListScrollBarWidth);
        }

        /// <summary>
        /// Gets scaled selectable row button size.
        /// </summary>
        int2 GetEnvironmentRowButtonSize() {
            return new int2(GetEnvironmentListContentWidth(), GetEnvironmentRowHeightPixels());
        }

        /// <summary>
        /// Gets scaled environment-id text box size.
        /// </summary>
        int2 GetEnvironmentIdTextBoxSize() {
            return new int2(GetContentWidth(), DialogMetrics.ScalePixels(24));
        }

        /// <summary>
        /// Gets scaled action button size.
        /// </summary>
        int2 GetActionButtonSize() {
            return new int2(DialogMetrics.ScalePixels(92), DialogMetrics.ScalePixels(24));
        }

        /// <summary>
        /// Gets scaled footer button size.
        /// </summary>
        int2 GetFooterButtonSize() {
            return new int2(DialogMetrics.ScalePixels(88), DialogMetrics.ScalePixels(22));
        }

        /// <summary>
        /// Repositions dialog content when the shared modal frame changes.
        /// </summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutContent();
        }

        /// <summary>
        /// Raises the cancel path when the shared close button is used.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }
    }
}
