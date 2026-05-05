namespace helengine.editor {
    /// <summary>
    /// Modal picker that lets the user search and add one editor component to the selected entity.
    /// </summary>
    public class ComponentAddDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the modal.
        /// </summary>
        public const int PanelWidth = 560;

        /// <summary>
        /// Fixed panel height used by the modal.
        /// </summary>
        public const int PanelHeight = 600;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height reserved for the modal title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Height of the search field.
        /// </summary>
        public const int SearchFieldHeight = 24;

        /// <summary>
        /// Height of each component row in the list.
        /// </summary>
        public const int RowHeight = ContextMenu.RowHeight;

        /// <summary>
        /// Vertical gap between component rows.
        /// </summary>
        public const int RowSpacing = 4;

        /// <summary>
        /// Height used by the footer Add button.
        /// </summary>
        public const int FooterButtonHeight = 22;

        /// <summary>
        /// Maximum time in milliseconds between row activations that counts as a double-click.
        /// </summary>
        const int RowDoubleClickMs = 350;

        /// <summary>
        /// Horizontal padding inside each component row.
        /// </summary>
        const int RowHorizontalPadding = 12;

        /// <summary>
        /// Placeholder text shown in the search field.
        /// </summary>
        const string SearchPlaceholder = "Search components";

        /// <summary>
        /// Font used for the search field and component labels.
        /// </summary>
        readonly FontAsset SearchFont;

        /// <summary>
        /// Host entity for the search field.
        /// </summary>
        readonly EditorEntity SearchFieldHost;

        /// <summary>
        /// Search text box used to filter the component list.
        /// </summary>
        readonly TextBoxComponent SearchField;

        /// <summary>
        /// Host entity for the component rows.
        /// </summary>
        readonly EditorEntity ListHost;

        /// <summary>
        /// Wheel-scrolling controller for the filtered component list.
        /// </summary>
        readonly ScrollComponent ListScrollComponent;

        /// <summary>
        /// Host entity for the footer action button.
        /// </summary>
        readonly EditorEntity FooterHost;

        /// <summary>
        /// Button used to confirm the current selection.
        /// </summary>
        readonly ButtonComponent AddButton;

        /// <summary>
        /// Text shown when no component matches the active filter.
        /// </summary>
        readonly TextComponent EmptyStateText;

        /// <summary>
        /// Host entity for the empty-state message.
        /// </summary>
        readonly EditorEntity EmptyStateHost;

        /// <summary>
        /// Pool of reusable rows shown in the picker.
        /// </summary>
        readonly List<ContextMenuRow> Rows;

        /// <summary>
        /// Components available for the current entity before filtering.
        /// </summary>
        readonly List<EditorComponentAddDescriptor> AvailableDescriptors;

        /// <summary>
        /// Components matching the active search text.
        /// </summary>
        readonly List<EditorComponentAddDescriptor> FilteredDescriptors;

        /// <summary>
        /// Script component descriptors discovered from the currently loaded game assembly.
        /// </summary>
        readonly List<EditorComponentAddDescriptor> ScriptDescriptors;

        /// <summary>
        /// Currently targeted editor entity.
        /// </summary>
        EditorEntity TargetEntity;

        /// <summary>
        /// Currently selected component descriptor.
        /// </summary>
        EditorComponentAddDescriptor SelectedDescriptor;

        /// <summary>
        /// Currently selected row.
        /// </summary>
        ContextMenuRow SelectedRow;

        /// <summary>
        /// Row that was most recently activated by the pointer.
        /// </summary>
        ContextMenuRow LastActivatedRow;

        /// <summary>
        /// Descriptor that was most recently activated by the pointer.
        /// </summary>
        EditorComponentAddDescriptor LastActivatedDescriptor;

        /// <summary>
        /// Timestamp of the most recent row activation.
        /// </summary>
        long LastActivatedTicks;

        /// <summary>
        /// Tracks whether the modal has finished initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user chooses one component from the modal.
        /// </summary>
        public event Action<EditorComponentAddDescriptor> ComponentSelected;

        /// <summary>
        /// Initializes a new searchable component picker modal.
        /// </summary>
        /// <param name="font">Font used for labels and the search box.</param>
        public ComponentAddDialog(FontAsset font)
            : base("Add Component Dialog", "Add Component", font, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            SearchFont = font;
            Rows = new List<ContextMenuRow>(16);
            AvailableDescriptors = new List<EditorComponentAddDescriptor>(16);
            FilteredDescriptors = new List<EditorComponentAddDescriptor>(16);
            ScriptDescriptors = new List<EditorComponentAddDescriptor>(16);
            LastActivatedTicks = 0;

            SearchFieldHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SearchFieldHost);

            SearchField = new TextBoxComponent(new int2(PanelWidth - (PanelPadding * 2), SearchFieldHeight), SearchFont, SearchPlaceholder);
            SearchField.TextChanged += HandleSearchFieldChanged;
            SearchFieldHost.AddComponent(SearchField);
            SearchField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);

            ListHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(ListHost);

            ListScrollComponent = new ScrollComponent();
            ListScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            ListScrollComponent.ScrollOffsetChanged += HandleScrollOffsetChanged;
            ListHost.AddComponent(ListScrollComponent);

            EmptyStateHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(EmptyStateHost);

            FooterHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(FooterHost);

            AddButton = new ButtonComponent("Add", new int2(PanelWidth - (PanelPadding * 2), FooterButtonHeight), SearchFont, HandleAddClicked, 0f);
            AddButton.UseSquareCorners();
            AddButton.SetHoverCursor(PointerCursorKind.Hand);
            FooterHost.AddComponent(AddButton);
            AddButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);

            EmptyStateText = new TextComponent {
                Font = SearchFont,
                Text = "No components match the search.",
                Color = ThemeManager.Colors.AccentQuaternary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(SearchFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            EmptyStateHost.AddComponent(EmptyStateText);
            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the modal currently has a target entity.
        /// </summary>
        public bool HasTargetEntity => TargetEntity != null;

        /// <summary>
        /// Shows the modal for the supplied editor entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the selected component.</param>
        public void Show(EditorEntity entity) {
            Show(entity, null);
        }

        /// <summary>
        /// Shows the modal for the supplied editor entity and a set of script-discovered components.
        /// </summary>
        /// <param name="entity">Entity that will receive the selected component.</param>
        /// <param name="scriptDescriptors">Descriptors discovered from the current game script assembly.</param>
        public void Show(EditorEntity entity, IReadOnlyList<EditorComponentAddDescriptor> scriptDescriptors) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            TargetEntity = entity;
            ScriptDescriptors.Clear();
            if (scriptDescriptors != null) {
                for (int i = 0; i < scriptDescriptors.Count; i++) {
                    EditorComponentAddDescriptor descriptor = scriptDescriptors[i];
                    if (descriptor != null) {
                        ScriptDescriptors.Add(descriptor);
                    }
                }
            }
            ResetDialogPositioning();
            ListScrollComponent.ResetScrollOffset();
            ResetActivationTracking();
            Enabled = true;
            SearchField.Text = string.Empty;
            RefreshAvailableComponents();
            ClearSelection();
            SearchField.IsFocused = true;
        }

        /// <summary>
        /// Hides the modal and clears the pending target entity.
        /// </summary>
        public void Hide() {
            TargetEntity = null;
            ListScrollComponent.ResetScrollOffset();
            SearchField.Text = string.Empty;
            SearchField.IsFocused = false;
            ResetDialogPositioning();
            ResetActivationTracking();
            Enabled = false;
            ClearSelection();
            HideRows();
        }

        /// <summary>
        /// Updates the modal layout to fit the host window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                HideRows();
                return;
            }

            UpdateSearchLayout();
            UpdateListLayout();
            UpdateFooterLayout();
        }

        /// <summary>
        /// Handles search text changes by rebuilding the filtered component list.
        /// </summary>
        /// <param name="textBox">Search field that changed.</param>
        void HandleSearchFieldChanged(TextBoxComponent textBox) {
            if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            ListScrollComponent.ResetScrollOffset();
            RefreshAvailableComponents();
        }

        /// <summary>
        /// Refreshes the list layout after the scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll controller that triggered the update.</param>
        /// <param name="scrollOffset">Current scroll offset.</param>
        void HandleScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            if (!IsInitialized || scrollComponent == null) {
                return;
            }

            UpdateListLayout();
        }

        /// <summary>
        /// Rebuilds the available list for the current target entity and reapplies the active search query.
        /// </summary>
        void RefreshAvailableComponents() {
            AvailableDescriptors.Clear();
            if (TargetEntity == null) {
                FilteredDescriptors.Clear();
                HideRows();
                return;
            }

            IReadOnlyList<EditorComponentAddDescriptor> availableComponents = EditorComponentAddCatalog.GetAvailableComponents(TargetEntity);
            for (int i = 0; i < availableComponents.Count; i++) {
                EditorComponentAddDescriptor descriptor = availableComponents[i];
                if (descriptor != null) {
                    AvailableDescriptors.Add(descriptor);
                }
            }

            for (int i = 0; i < ScriptDescriptors.Count; i++) {
                EditorComponentAddDescriptor descriptor = ScriptDescriptors[i];
                if (descriptor == null) {
                    continue;
                }

                AvailableDescriptors.Add(descriptor);
            }

            RebuildFilteredDescriptors();
            UpdateListLayout();
        }

        /// <summary>
        /// Applies the active search query to the available descriptors.
        /// </summary>
        void RebuildFilteredDescriptors() {
            FilteredDescriptors.Clear();

            string query = SearchField.Text;
            for (int i = 0; i < AvailableDescriptors.Count; i++) {
                EditorComponentAddDescriptor descriptor = AvailableDescriptors[i];
                if (descriptor == null) {
                    continue;
                }

                if (!MatchesQuery(descriptor, query)) {
                    continue;
                }

                FilteredDescriptors.Add(descriptor);
            }

            ListScrollComponent.ItemCount = FilteredDescriptors.Count;
            ListScrollComponent.ClampScrollOffset();

            if (SelectedDescriptor != null && !IsFilteredDescriptorVisible(SelectedDescriptor)) {
                ClearSelection();
            }
        }

        /// <summary>
        /// Determines whether one component descriptor matches the active search query.
        /// </summary>
        /// <param name="descriptor">Descriptor to test.</param>
        /// <param name="query">Current search query.</param>
        /// <returns>True when the descriptor should remain visible.</returns>
        bool MatchesQuery(EditorComponentAddDescriptor descriptor, string query) {
            if (descriptor == null) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(query)) {
                return true;
            }

            return descriptor.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.ComponentType.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Positions the search field inside the modal.
        /// </summary>
        void UpdateSearchLayout() {
            int contentWidth = Math.Max(0, PanelWidth - (PanelPadding * 2));
            int searchTop = PanelPadding + HeaderHeight + SectionSpacing;

            SearchFieldHost.Position = new float3(PanelPadding, searchTop, 0.2f);
            SearchField.Size = new int2(contentWidth, SearchFieldHeight);
        }

        /// <summary>
        /// Positions the filtered component rows inside the modal.
        /// </summary>
        void UpdateListLayout() {
            int contentWidth = Math.Max(0, PanelWidth - (PanelPadding * 2));
            int listTop = PanelPadding + HeaderHeight + SectionSpacing + SearchFieldHeight + SectionSpacing;
            int footerTop = PanelHeight - PanelPadding - FooterButtonHeight;
            int listHeight = Math.Max(0, footerTop - listTop - SectionSpacing);
            int rowStride = RowHeight + RowSpacing;

            int visibleRowCount = Math.Max(1, rowStride == 0 ? 1 : (listHeight / rowStride));
            ListScrollComponent.VisibleItemCount = visibleRowCount;
            ListScrollComponent.Size = new int2(contentWidth, listHeight);
            RebuildFilteredDescriptors();

            ListHost.Position = new float3(PanelPadding, listTop, 0.2f);
            EmptyStateHost.Position = new float3(PanelPadding, listTop + SectionSpacing, 0.2f);
            EmptyStateHost.Enabled = FilteredDescriptors.Count == 0;
            EmptyStateText.Size = new int2(contentWidth, RowHeight);

            EnsureRowCount(visibleRowCount);
            ContextMenuRow visibleSelectedRow = null;
            int scrollOffset = ListScrollComponent.ScrollOffset;

            for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++) {
                ContextMenuRow row = Rows[rowIndex];
                int descriptorIndex = scrollOffset + rowIndex;
                if (descriptorIndex >= FilteredDescriptors.Count) {
                    DisableRow(row);
                    continue;
                }

                EditorComponentAddDescriptor descriptor = FilteredDescriptors[descriptorIndex];
                row.Entity.Enabled = true;
                if (row.CurrentDescriptor != descriptor) {
                    row.CurrentDescriptor = descriptor;
                    row.ResetState();
                }
                row.Item = new ContextMenuItem(descriptor.DisplayName, () => { }, null, false);
                row.BaseColor = descriptor == SelectedDescriptor ? ThemeManager.Colors.AccentPrimary : rowIndex % 2 == 1 ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                row.HoverColor = ThemeManager.Colors.AccentSecondary;
                row.PressedColor = ThemeManager.Colors.AccentPrimary;
                row.Entity.Position = new float3(0f, rowIndex * rowStride, 0.1f);
                row.Background.Size = new int2(contentWidth, RowHeight);
                row.Interactable.Size = new int2(contentWidth, RowHeight);
                row.Interactable.HoverCursor = PointerCursorKind.Hand;
                row.LabelHost.Position = new float3(RowHorizontalPadding, (float)Math.Round((RowHeight - Math.Max(SearchFont.LineHeight, 1f)) * 0.5f), 0.2f);
                row.Label.Text = descriptor.DisplayName;
                row.Label.Size = new int2(Math.Max(0, contentWidth - (RowHorizontalPadding * 2)), RowHeight);
                row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
                if (descriptor == SelectedDescriptor) {
                    visibleSelectedRow = row;
                    row.BaseColor = ThemeManager.Colors.AccentPrimary;
                    row.UpdateBackground();
                }
            }

            SelectedRow = visibleSelectedRow;

            if (FilteredDescriptors.Count == 0) {
                HideRows();
                EmptyStateText.Text = "No components match the search.";
                EmptyStateHost.Enabled = true;
                return;
            }

            EmptyStateText.Text = string.Empty;
            EmptyStateHost.Enabled = false;
        }

        /// <summary>
        /// Positions the footer action button beneath the component list.
        /// </summary>
        void UpdateFooterLayout() {
            int contentWidth = Math.Max(0, PanelWidth - (PanelPadding * 2));
            int footerTop = PanelHeight - PanelPadding - FooterButtonHeight;

            FooterHost.Position = new float3(PanelPadding, footerTop, 0.2f);
            AddButton.SetSize(new int2(contentWidth, FooterButtonHeight));
        }

        /// <summary>
        /// Ensures enough pooled rows are available for the visible list.
        /// </summary>
        /// <param name="count">Number of rows required.</param>
        void EnsureRowCount(int count) {
            for (int i = Rows.Count; i < count; i++) {
                Rows.Add(CreateRow());
            }
        }

        /// <summary>
        /// Creates one pooled row for the component picker list.
        /// </summary>
        /// <returns>Newly created row container.</returns>
        ContextMenuRow CreateRow() {
            EditorEntity rowEntity = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = false
            };

            SpriteComponent background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = DialogPanelOrder
            };
            rowEntity.AddComponent(background);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(0, 0),
                HoverCursor = PointerCursorKind.Hand
            };
            rowEntity.AddComponent(interactable);

            EditorEntity labelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            rowEntity.AddChild(labelHost);

            TextComponent label = new TextComponent {
                Font = SearchFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, RowHeight),
                RenderOrder2D = DialogTextOrder
            };
            labelHost.AddComponent(label);

            ContextMenuRow row = new ContextMenuRow(rowEntity, background, labelHost, label, interactable);
            row.Pressed += HandleRowPressed;
            row.Activated += HandleRowActivated;
            ListHost.AddChild(rowEntity);
            return row;
        }

        /// <summary>
        /// Handles pointer press on one component row by selecting it.
        /// </summary>
        /// <param name="row">Pressed component row.</param>
        void HandleRowPressed(ContextMenuRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (TargetEntity == null) {
                return;
            }

            int scrollOffset = ListScrollComponent.ScrollOffset;
            for (int i = 0; i < Rows.Count; i++) {
                if (Rows[i] != row) {
                    continue;
                }

                int descriptorIndex = scrollOffset + i;
                if (descriptorIndex < 0 || descriptorIndex >= FilteredDescriptors.Count) {
                    return;
                }

                SelectDescriptor(FilteredDescriptors[descriptorIndex], row);
                return;
            }
        }

        /// <summary>
        /// Handles click activation on a component row and confirms it on a double-click.
        /// </summary>
        /// <param name="row">Activated row.</param>
        void HandleRowActivated(ContextMenuRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (TargetEntity == null) {
                return;
            }

            int scrollOffset = ListScrollComponent.ScrollOffset;
            for (int i = 0; i < Rows.Count; i++) {
                if (Rows[i] != row) {
                    continue;
                }

                int descriptorIndex = scrollOffset + i;
                if (descriptorIndex < 0 || descriptorIndex >= FilteredDescriptors.Count) {
                    return;
                }

                EditorComponentAddDescriptor descriptor = FilteredDescriptors[descriptorIndex];
                long now = Environment.TickCount64;
                bool isDoubleClick = row == LastActivatedRow &&
                                     descriptor == LastActivatedDescriptor &&
                                     now - LastActivatedTicks <= RowDoubleClickMs;

                LastActivatedRow = row;
                LastActivatedDescriptor = descriptor;
                LastActivatedTicks = now;

                if (isDoubleClick && ConfirmSelectedDescriptor()) {
                    Hide();
                }

                return;
            }
        }

        /// <summary>
        /// Confirms the current selection using the footer Add button and closes the modal on success.
        /// </summary>
        void HandleAddClicked() {
            if (ConfirmSelectedDescriptor()) {
                Hide();
            }
        }

        /// <summary>
        /// Clears all pooled rows when the list is empty or hidden.
        /// </summary>
        void HideRows() {
            for (int i = 0; i < Rows.Count; i++) {
                DisableRow(Rows[i]);
            }

            ResetActivationTracking();
            EmptyStateText.Text = string.Empty;
            EmptyStateHost.Enabled = false;
        }

        /// <summary>
        /// Clears the cached row activation state used for double-click detection.
        /// </summary>
        void ResetActivationTracking() {
            LastActivatedRow = null;
            LastActivatedDescriptor = null;
            LastActivatedTicks = 0;
        }

        /// <summary>
        /// Disables one pooled row and clears its transient interaction state.
        /// </summary>
        /// <param name="row">Row to disable.</param>
        void DisableRow(ContextMenuRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            if (row == LastActivatedRow) {
                ResetActivationTracking();
            }

            row.Entity.Enabled = false;
            row.Item = null;
            row.CurrentDescriptor = null;
            row.ResetState();
            row.Label.Text = string.Empty;
        }

        /// <summary>
        /// Selects one descriptor and updates the visible row highlight state.
        /// </summary>
        /// <param name="descriptor">Descriptor to select.</param>
        /// <param name="row">Owning row.</param>
        void SelectDescriptor(EditorComponentAddDescriptor descriptor, ContextMenuRow row) {
            SelectedDescriptor = descriptor;
            SelectedRow = row;
            UpdateSelectedRowVisuals();
        }

        /// <summary>
        /// Clears the current selection and footer state.
        /// </summary>
        void ClearSelection() {
            SelectedDescriptor = null;
            SelectedRow = null;
            ResetActivationTracking();
            UpdateSelectedRowVisuals();
        }

        /// <summary>
        /// Reapplies selection tinting across the visible rows.
        /// </summary>
        void UpdateSelectedRowVisuals() {
            for (int i = 0; i < Rows.Count; i++) {
                ContextMenuRow row = Rows[i];
                if (!row.Entity.Enabled || row.Item == null) {
                    continue;
                }

                if (row == SelectedRow) {
                    row.BaseColor = ThemeManager.Colors.AccentPrimary;
                } else {
                    row.BaseColor = i % 2 == 1 ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                }

                row.UpdateBackground();
            }
        }

        /// <summary>
        /// Confirms the current selection by notifying listeners.
        /// </summary>
        /// <returns>True when a descriptor was available and the selection event was raised.</returns>
        bool ConfirmSelectedDescriptor() {
            EditorComponentAddDescriptor descriptor = SelectedDescriptor;
            if (descriptor == null || TargetEntity == null) {
                return false;
            }

            if (ComponentSelected != null) {
                ComponentSelected(descriptor);
            }

            return true;
        }

        /// <summary>
        /// Returns true when the active selection is still visible after filtering.
        /// </summary>
        /// <param name="descriptor">Descriptor to search for.</param>
        /// <returns>True when the descriptor is still visible.</returns>
        bool IsFilteredDescriptorVisible(EditorComponentAddDescriptor descriptor) {
            if (descriptor == null) {
                return false;
            }

            for (int i = 0; i < FilteredDescriptors.Count; i++) {
                if (ReferenceEquals(FilteredDescriptors[i], descriptor)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Closes the dialog when the shared title-bar close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            Hide();
        }
    }
}

