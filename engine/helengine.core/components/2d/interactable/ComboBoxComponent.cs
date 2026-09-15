namespace helengine {
    /// <summary>
    /// Renders a selectable combo box with a drop-down list of items.
    /// </summary>
    public class ComboBoxComponent : Component, IFocusTarget {
        /// <summary>
        /// Horizontal padding applied to label text.
        /// </summary>
        const int TextPaddingX = 8;
        /// <summary>
        /// Horizontal padding between the arrow glyph and the right edge.
        /// </summary>
        const int ArrowPaddingX = 8;
        /// <summary>
        /// Vertical gap between the main control and the drop-down list.
        /// </summary>
        const int ListGap = 2;
        /// <summary>
        /// ASCII glyph used to indicate the drop-down arrow.
        /// </summary>
        const string ArrowGlyph = "v";

        /// <summary>
        /// Backing list of items displayed by the combo box.
        /// </summary>
        [NativeOwnedMember]
        List<string> ItemsValue;
        /// <summary>
        /// Cached visuals for each item row.
        /// </summary>
        [NativeOwnedMember]
        List<ComboBoxItemVisual> ItemVisuals;
        /// <summary>
        /// Tracks whether custom render orders were supplied for the combo-box visuals.
        /// </summary>
        bool HasRenderOrderOverrides;

        /// <summary>
        /// Font used to render text in the control.
        /// </summary>
        FontAsset FontValue;
        /// <summary>
        /// Cached size of the combo box control.
        /// </summary>
        int2 SizeValue;
        /// <summary>
        /// Height of each item row.
        /// </summary>
        int ItemHeight;
        /// <summary>
        /// Index of the currently selected item.
        /// </summary>
        int SelectedIndexValue;
        /// <summary>
        /// Tracks whether the drop-down list is open.
        /// </summary>
        bool IsOpenValue;
        /// <summary>
        /// Tracks whether the main control is hovered.
        /// </summary>
        bool IsHovering;
        /// <summary>
        /// Tracks whether the main control is pressed.
        /// </summary>
        bool IsPressed;

        /// <summary>
        /// Background shape for the main control.
        /// </summary>
        RoundedRectComponent Background;
        /// <summary>
        /// Text component for the selected item label.
        /// </summary>
        TextComponent LabelText;
        /// <summary>
        /// Text component for the arrow glyph.
        /// </summary>
        TextComponent ArrowText;
        /// <summary>
        /// Interactable region for the main control.
        /// </summary>
        InteractableComponent Interactable;

        /// <summary>
        /// Entity hosting the selected item label.
        /// </summary>
        Entity LabelEntity;
        /// <summary>
        /// Entity hosting the arrow glyph.
        /// </summary>
        Entity ArrowEntity;
        /// <summary>
        /// Root entity for the drop-down list.
        /// </summary>
        Entity ListRoot;
        /// <summary>
        /// Background for the drop-down list.
        /// </summary>
        RoundedRectComponent ListBackground;

        /// <summary>
        /// Render order for the main background.
        /// </summary>
        byte BackgroundOrder;
        /// <summary>
        /// Render order for the main text elements.
        /// </summary>
        byte TextOrder;
        /// <summary>
        /// Render order for the list background.
        /// </summary>
        byte ListBackgroundOrder;
        /// <summary>
        /// Render order for item labels.
        /// </summary>
        byte ListTextOrder;

        /// <summary>
        /// Raised when a new item is selected.
        /// </summary>
        public event Action<int, string> SelectionChanged;

        /// <summary>
        /// Creates a new combo box with the provided items and selection.
        /// </summary>
        /// <param name="size">Size of the combo box control.</param>
        /// <param name="font">Font used to render labels.</param>
        /// <param name="items">Items available to select from.</param>
        /// <param name="selectedIndex">Initial selected index, or -1 for no selection.</param>
        public ComboBoxComponent(int2 size, FontAsset font, IReadOnlyList<string> items, int selectedIndex) {
            if (size.X <= 0 || size.Y <= 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "ComboBox size must be positive.");
            } else if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }

            this.SizeValue = size;
            this.FontValue = font;
            this.ItemsValue = new List<string>(items.Count);
            ItemVisuals = new List<ComboBoxItemVisual>(items.Count);
            ItemHeight = size.Y;

            CopyItems(items);
            this.SelectedIndexValue = ValidateSelectedIndex(this.ItemsValue.Count, selectedIndex);
        }

        /// <summary>
        /// Gets or sets the size of the combo box control.
        /// </summary>
        public int2 Size {
            get { return SizeValue; }
            set {
                if (value.X <= 0 || value.Y <= 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "ComboBox size must be positive.");
                }

                SizeValue = value;
                ItemHeight = SizeValue.Y;
                UpdateLayout();
            }
        }

        /// <summary>
        /// Gets or sets the font used to render labels.
        /// </summary>
        public FontAsset Font {
            get { return FontValue; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                FontValue = value;
                UpdateLabelText();
                UpdateLayout();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the drop-down list is open.
        /// </summary>
        public bool IsOpen {
            get { return IsOpenValue; }
            set {
                if (value && ItemsValue.Count == 0) {
                    IsOpenValue = false;
                    UpdateDropdownVisibility();
                    return;
                }
                if (IsOpenValue == value) {
                    return;
                }

                IsOpenValue = value;
                UpdateDropdownVisibility();
            }
        }

        /// <summary>
        /// Gets the current list of items.
        /// </summary>
        public IReadOnlyList<string> Items => ItemsValue;

        /// <summary>
        /// Gets a value indicating whether the combo box has a selection.
        /// </summary>
        public bool HasSelection => SelectedIndexValue >= 0 && SelectedIndexValue < ItemsValue.Count;

        /// <summary>
        /// Gets or sets the selected index, or -1 for no selection.
        /// </summary>
        public int SelectedIndex {
            get { return SelectedIndexValue; }
            set { SetSelectedIndexInternal(value, true); }
        }

        /// <summary>
        /// Overrides the render order used for the combo-box control and drop-down visuals.
        /// </summary>
        /// <param name="backgroundOrder">Render order for the main control background.</param>
        /// <param name="textOrder">Render order for the main control text.</param>
        /// <param name="listBackgroundOrder">Render order for the drop-down background and item backgrounds.</param>
        /// <param name="listTextOrder">Render order for the drop-down item labels.</param>
        public void SetRenderOrders(byte backgroundOrder, byte textOrder, byte listBackgroundOrder, byte listTextOrder) {
            HasRenderOrderOverrides = true;
            this.BackgroundOrder = backgroundOrder;
            this.TextOrder = textOrder;
            this.ListBackgroundOrder = listBackgroundOrder;
            this.ListTextOrder = listTextOrder;
            ApplyRenderOrders();
        }

        /// <summary>
        /// Applies the standard docked-panel presentation used by editor and runtime tool panels.
        /// </summary>
        public void UsePanelPresentation() {
            SetRenderOrders(RenderOrder2D.PanelSurface, RenderOrder2D.PanelForeground, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
        }

        /// <summary>
        /// Applies the modal presentation used by dialog-hosted combo boxes and keeps the drop-down above other modal controls.
        /// </summary>
        public void UseModalPresentation() {
            SetRenderOrders(RenderOrder2D.ModalBackground, RenderOrder2D.ModalForeground, RenderOrder2D.ModalOverlayBackground, RenderOrder2D.ModalOverlayForeground);
        }

        /// <summary>
        /// Gets the selected item text.
        /// </summary>
        public string SelectedItem {
            get {
                if (!HasSelection) {
                    throw new InvalidOperationException("ComboBox has no selected item.");
                }

                return ItemsValue[SelectedIndexValue];
            }
        }

        /// <summary>
        /// Gets or sets the focus group that owns this combo box during keyboard traversal.
        /// </summary>
        public IFocusGroup FocusGroup { get; set; }

        /// <summary>
        /// Gets or sets the traversal order of this combo box within its focus group.
        /// </summary>
        public int TabIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this combo box is the preferred entry target for its root group.
        /// </summary>
        public bool IsDefaultTarget { get; set; }

        /// <summary>
        /// Gets whether this combo box can currently receive keyboard focus.
        /// </summary>
        public bool CanReceiveFocus => Parent != null && Parent.IsHierarchyEnabled && Interactable != null;

        /// <summary>
        /// Gets a value indicating whether the main combo-box control is currently keyboard-focused.
        /// </summary>
        public bool IsKeyboardFocused { get; private set; }

        /// <summary>
        /// Sets the available items and selection for the combo box.
        /// </summary>
        /// <param name="items">New item list.</param>
        /// <param name="selectedIndex">Selected index, or -1 for no selection.</param>
        public void SetItems(IReadOnlyList<string> items, int selectedIndex) {
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }

            ValidateItems(items);
            int validatedIndex = ValidateSelectedIndex(items.Count, selectedIndex);
            bool selectionChanged = this.SelectedIndexValue != validatedIndex;

            this.ItemsValue.Clear();
            for (int i = 0; i < items.Count; i++) {
                this.ItemsValue.Add(items[i]);
            }

            this.SelectedIndexValue = validatedIndex;
            if (this.ItemsValue.Count == 0 && IsOpenValue) {
                IsOpenValue = false;
            }

            UpdateLabelText();
            UpdateLayout();
            UpdateDropdownVisibility();

            if (selectionChanged && HasSelection && SelectionChanged != null) {
                SelectionChanged(this.SelectedIndexValue, this.ItemsValue[this.SelectedIndexValue]);
            }
        }

        /// <summary>
        /// Builds the visual tree and hooks up input handlers.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (!HasRenderOrderOverrides) {
                UsePanelPresentation();
            }

            Background = new RoundedRectComponent();
            Background.Size = SizeValue;
            Background.Radius = GetCornerRadius(SizeValue);
            Background.BorderThickness = 2f;
            Background.FillColor = ThemeManager.Colors.SurfaceInput;
            Background.BorderColor = ThemeManager.Colors.AccentTertiary;
            Background.RenderOrder2D = BackgroundOrder;
            entity.AddComponent(Background);

            Interactable = new InteractableComponent();
            Interactable.Size = SizeValue;
            Interactable.CursorEvent += HandleMainCursorEvent;
            entity.AddComponent(Interactable);

            if (entity.Children == null) {
                entity.InitChildren();
            }

            LabelEntity = new Entity(OwnerCore ?? throw new InvalidOperationException("Combo-box visuals require an owning core."));
            LabelEntity.LayerMask = entity.LayerMask;
            LabelEntity.Enabled = true;
            LabelEntity.InitComponents();
            entity.AddChild(LabelEntity);

            LabelText = new TextComponent();
            LabelText.Font = FontValue;
            LabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            LabelText.RenderOrder2D = TextOrder;
            LabelEntity.AddComponent(LabelText);

            ArrowEntity = new Entity(OwnerCore ?? throw new InvalidOperationException("Combo-box visuals require an owning core."));
            ArrowEntity.LayerMask = entity.LayerMask;
            ArrowEntity.Enabled = true;
            ArrowEntity.InitComponents();
            entity.AddChild(ArrowEntity);

            ArrowText = new TextComponent();
            ArrowText.Font = FontValue;
            ArrowText.Color = ThemeManager.Colors.InputForegroundSecondary;
            ArrowText.RenderOrder2D = TextOrder;
            ArrowEntity.AddComponent(ArrowText);

            ListRoot = new Entity(OwnerCore ?? throw new InvalidOperationException("Combo-box visuals require an owning core."));
            ListRoot.LayerMask = entity.LayerMask;
            ListRoot.InitComponents();
            ListRoot.InitChildren();
            entity.AddChild(ListRoot);

            ListBackground = new RoundedRectComponent();
            ListBackground.RenderOrder2D = ListBackgroundOrder;
            ListBackground.BorderThickness = 1f;
            ListBackground.FillColor = ThemeManager.Colors.SurfacePrimary;
            ListBackground.BorderColor = ThemeManager.Colors.AccentTertiary;
            ListRoot.AddComponent(ListBackground);

            var updateComponent = new ComboBoxUpdateComponent(this);
            updateComponent.UpdateOrder = OwnerCore.ObjectManager.GetUpdateOrderForLayer(1);
            entity.AddComponent(updateComponent);

            EnsureItemVisuals(ItemsValue.Count);
            UpdateLabelText();
            UpdateLayout();
            UpdateDropdownVisibility();
        }

        /// <summary>
        /// Applies the currently configured render orders to all constructed visuals.
        /// </summary>
        void ApplyRenderOrders() {
            if (Background != null) {
                Background.RenderOrder2D = BackgroundOrder;
            }

            if (LabelText != null) {
                LabelText.RenderOrder2D = TextOrder;
            }

            if (ArrowText != null) {
                ArrowText.RenderOrder2D = TextOrder;
            }

            if (ListBackground != null) {
                ListBackground.RenderOrder2D = ListBackgroundOrder;
            }

            for (int i = 0; i < ItemVisuals.Count; i++) {
                ComboBoxItemVisual entry = ItemVisuals[i];
                entry.Background.RenderOrder2D = ListBackgroundOrder;
                entry.Label.RenderOrder2D = ListTextOrder;
            }
        }

        /// <summary>
        /// Clears focus and transient interaction state when the combo box parent is disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                IsHovering = false;
                IsPressed = false;
                ResetItemStates();
                SetTargetFocused(false);
            }
        }

        /// <summary>
        /// Clears focus and transient interaction state when the combo box is removed.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);

            IsHovering = false;
            IsPressed = false;
            ResetItemStates();
            SetTargetFocused(false);
        }

        /// <summary>
        /// Releases item-visual wrappers and list containers owned by this combo-box component.
        /// </summary>
        public override void Dispose() {
            if (ItemVisuals != null) {
                for (int itemIndex = 0; itemIndex < ItemVisuals.Count; itemIndex++) {
                    ItemVisuals[itemIndex].CursorEvent -= HandleItemCursorEvent;
                    NativeOwnership.Delete(ItemVisuals[itemIndex]);
                }

                ItemVisuals.Clear();
            }
            NativeOwnership.Release(ref ItemVisuals);

            if (ItemsValue != null) {
                ItemsValue.Clear();
            }
            NativeOwnership.Release(ref ItemsValue);

            base.Dispose();
        }

        /// <summary>
        /// Validates that item entries are non-null.
        /// </summary>
        /// <param name="items">Item list to validate.</param>
        void ValidateItems(IReadOnlyList<string> items) {
            for (int i = 0; i < items.Count; i++) {
                if (items[i] == null) {
                    throw new ArgumentException("ComboBox items must not contain null entries.", nameof(items));
                }
            }
        }

        /// <summary>
        /// Copies the provided items into the internal list.
        /// </summary>
        /// <param name="source">Source list to copy.</param>
        void CopyItems(IReadOnlyList<string> source) {
            ValidateItems(source);

            for (int i = 0; i < source.Count; i++) {
                ItemsValue.Add(source[i]);
            }
        }

        /// <summary>
        /// Checks whether a selected index is valid for the provided count.
        /// </summary>
        /// <param name="itemCount">Number of available items.</param>
        /// <param name="index">Selected index to validate.</param>
        /// <returns>A validated index value.</returns>
        int ValidateSelectedIndex(int itemCount, int index) {
            if (index < -1 || index >= itemCount) {
                throw new ArgumentOutOfRangeException(nameof(index), "SelectedIndex must be -1 or within the item range.");
            }

            return index;
        }

        /// <summary>
        /// Updates the selected index and raises events when requested.
        /// </summary>
        /// <param name="index">New selected index.</param>
        /// <param name="raiseEvent">True to raise the selection changed event.</param>
        void SetSelectedIndexInternal(int index, bool raiseEvent) {
            int validated = ValidateSelectedIndex(ItemsValue.Count, index);
            if (SelectedIndexValue == validated) {
                return;
            }

            SelectedIndexValue = validated;
            UpdateLabelText();
            UpdateAllItemStates();

            if (raiseEvent && HasSelection && SelectionChanged != null) {
                SelectionChanged(SelectedIndexValue, ItemsValue[SelectedIndexValue]);
            }
        }

        /// <summary>
        /// Updates the selected item label and arrow glyph text.
        /// </summary>
        void UpdateLabelText() {
            if (LabelText == null || ArrowText == null) {
                return;
            }

            string displayText = HasSelection ? ItemsValue[SelectedIndexValue] : string.Empty;
            LabelText.Text = displayText;
            LabelText.Color = HasSelection
                ? ThemeManager.Colors.InputForegroundPrimary
                : ThemeManager.Colors.InputForegroundSecondary;

            ArrowText.Text = ArrowGlyph;
            ArrowText.Color = ThemeManager.Colors.InputForegroundSecondary;

            UpdateLabelLayout();
        }

        /// <summary>
        /// Updates input handling for the combo box when open.
        /// </summary>
        public void Update() {
#if DESKTOP_PLATFORM
            if (!IsOpenValue || Parent == null || ListRoot == null) {
                return;
            }

            InputSystem inputManager = OwnerCore.Input;
            if (!inputManager.WasMouseLeftButtonPressed()) {
                return;
            }

            int mouseX = inputManager.GetMouseX();
            int mouseY = inputManager.GetMouseY();
            if (IsPointerInsideCombo(mouseX, mouseY)) {
                return;
            }

            IsOpen = false;
#endif
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the main combo-box control.
        /// </summary>
        /// <param name="x">Screen-space X coordinate to evaluate.</param>
        /// <param name="y">Screen-space Y coordinate to evaluate.</param>
        /// <returns>True when the point is inside the main control.</returns>
        public bool ContainsScreenPoint(int x, int y) {
            if (Parent == null) {
                return false;
            }

            float3 origin = Parent.Position;
            return x >= origin.X &&
                   x < origin.X + SizeValue.X &&
                   y >= origin.Y &&
                   y < origin.Y + SizeValue.Y;
        }

        /// <summary>
        /// Applies or clears the keyboard-focused visual state for the combo-box main control.
        /// </summary>
        /// <param name="isFocused">True when the combo box should render as focused.</param>
        public void SetTargetFocused(bool isFocused) {
            IsKeyboardFocused = isFocused;
            if (!isFocused && IsOpenValue) {
                IsOpen = false;
            }

            UpdateMainVisual();
        }

        /// <summary>
        /// Returns true when the combo box should activate for the provided key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when Enter or Space should toggle the drop-down.</returns>
#if DESKTOP_PLATFORM
        public bool CanActivateWithKey(Keys key) {
            return key == Keys.Enter || key == Keys.Space;
        }

        /// <summary>
        /// Toggles the main drop-down list for supported keyboard activation keys.
        /// </summary>
        /// <param name="key">Activation key routed to the combo box.</param>
        public void ActivateFromKey(Keys key) {
            if (!CanActivateWithKey(key) || ItemsValue.Count == 0) {
                return;
            }

            IsOpen = !IsOpenValue;
        }
#endif

        /// <summary>
        /// Rebuilds layout for the main control and drop-down list.
        /// </summary>
        void UpdateLayout() {
            UpdateMainLayout();
            UpdateListLayout();
        }

        /// <summary>
        /// Updates the layout of the main control visuals.
        /// </summary>
        void UpdateMainLayout() {
            if (Background == null || Interactable == null) {
                return;
            }

            Background.Size = SizeValue;
            Background.Radius = GetCornerRadius(SizeValue);
            Interactable.Size = SizeValue;
            UpdateLabelLayout();
        }

        /// <summary>
        /// Positions and sizes the selected label and arrow glyph.
        /// </summary>
        void UpdateLabelLayout() {
            if (LabelEntity == null || LabelText == null || ArrowEntity == null || ArrowText == null || FontValue == null) {
                return;
            }

            double lineHeight = Math.Max((double)FontValue.LineHeight, 1.0);
            double labelY = Math.Round((SizeValue.Y - lineHeight) / 2.0, MidpointRounding.AwayFromZero);

            FontTightMetrics labelMetrics = FontValue.MeasureTight(LabelText.Text);
            int labelWidth = (int)Math.Ceiling(labelMetrics.Width);
            int labelHeight = (int)Math.Ceiling(Math.Max((double)labelMetrics.Height, 1.0));
            LabelText.Size = new int2(labelWidth, labelHeight);
            LabelEntity.Position = new float3(TextPaddingX, (float)labelY, 0.1f);

            FontTightMetrics arrowMetrics = FontValue.MeasureTight(ArrowGlyph);
            int arrowWidth = (int)Math.Ceiling(arrowMetrics.Width);
            int arrowHeight = (int)Math.Ceiling(Math.Max((double)arrowMetrics.Height, 1.0));
            ArrowText.Size = new int2(arrowWidth, arrowHeight);

            double arrowX = SizeValue.X - ArrowPaddingX - arrowMetrics.Width;
            if (arrowX < TextPaddingX) {
                arrowX = TextPaddingX;
            }
            arrowX = Math.Round(arrowX, MidpointRounding.AwayFromZero);
            ArrowEntity.Position = new float3((float)arrowX, (float)labelY, 0.1f);
        }

        /// <summary>
        /// Updates the layout of the drop-down list and its items.
        /// </summary>
        void UpdateListLayout() {
            if (ListRoot == null || ListBackground == null) {
                return;
            }

            int listHeight = ItemHeight * ItemsValue.Count;
            if (listHeight <= 0) {
                listHeight = 1;
            }

            // Open upward when the downward list would leave the nearest ancestor clip region: clipped list
            // items are both invisible and rejected by pointer hit-testing, so they could never be selected.
            float listOffsetY = SizeValue.Y + ListGap;
            if (Parent != null && TryFindNearestAncestorClipRect(out float4 clipRect)) {
                float3 comboOrigin = Parent.Position;
                bool overflowsBelow = comboOrigin.Y + SizeValue.Y + ListGap + listHeight > clipRect.Y + clipRect.W;
                bool fitsAbove = comboOrigin.Y - ListGap - listHeight >= clipRect.Y;
                if (overflowsBelow && fitsAbove) {
                    listOffsetY = -(ListGap + listHeight);
                }
            }

            ListRoot.Position = new float3(0f, listOffsetY, 0.2f);
            ListBackground.Size = new int2(SizeValue.X, listHeight);
            if (Background != null) {
                ListBackground.Radius = Background.Radius;
            }

            EnsureItemVisuals(ItemsValue.Count);

            double lineHeight = Math.Max((double)FontValue.LineHeight, 1.0);
            bool shouldShow = IsOpenValue && ItemsValue.Count > 0;
            for (int i = 0; i < ItemVisuals.Count; i++) {
                ComboBoxItemVisual entry = ItemVisuals[i];
                bool isActive = i < ItemsValue.Count;
                bool isVisible = isActive && shouldShow;
                entry.Root.Enabled = isVisible;
                entry.LabelHost.Enabled = isVisible;
                if (!isActive) {
                    entry.Label.Text = string.Empty;
                    entry.Label.Size = new int2(0, 0);
                    continue;
                }

                if (!shouldShow) {
                    entry.Label.Text = string.Empty;
                    entry.Label.Size = new int2(0, 0);
                    continue;
                }

                entry.Index = i;
                entry.Root.Position = new float3(0f, ItemHeight * i, 0.1f);
                entry.Background.Size = new int2(SizeValue.X, ItemHeight);
                entry.Background.Radius = 0f;
                entry.Background.BorderColor = ThemeManager.Colors.AccentTertiary;
                entry.Interactable.Size = new int2(SizeValue.X, ItemHeight);

                string itemText = ItemsValue[i];
                entry.Label.Text = itemText;
                entry.Label.Font = FontValue;
                entry.Label.Color = ThemeManager.Colors.InputForegroundPrimary;

                FontTightMetrics itemMetrics = FontValue.MeasureTight(itemText);
                entry.Label.Size = new int2(
                    (int)Math.Ceiling(itemMetrics.Width),
                    (int)Math.Ceiling(Math.Max((double)itemMetrics.Height, 1.0))
                );

                double textY = Math.Round((ItemHeight - lineHeight) / 2.0, MidpointRounding.AwayFromZero);
                entry.LabelHost.Position = new float3(TextPaddingX, (float)textY, 0.1f);
                UpdateItemVisualState(entry, i == SelectedIndexValue);
            }
        }

        /// <summary>
        /// Ensures item visuals are created up to the requested count.
        /// </summary>
        /// <param name="count">Number of visuals required.</param>
        void EnsureItemVisuals(int count) {
            if (ListRoot == null) {
                return;
            }

            for (int i = ItemVisuals.Count; i < count; i++) {
                AppendItemVisual();
            }
        }

        /// <summary>
        /// Creates, attaches, and transfers one item-visual wrapper outside the collection-growth loop's ownership state.
        /// </summary>
        void AppendItemVisual() {
            ComboBoxItemVisual entry = CreateItemVisual();
            entry.CursorEvent += HandleItemCursorEvent;
            ListRoot.AddChild(entry.Root);
            AddOwnedItemVisual(entry);
        }

        /// <summary>
        /// Transfers one item-visual wrapper into the collection released by this component.
        /// </summary>
        /// <param name="entry">Item-visual wrapper whose cleanup responsibility moves to this component.</param>
        void AddOwnedItemVisual([NativeTakesOwnership] ComboBoxItemVisual entry) {
            ItemVisuals.Add(entry);
        }

        /// <summary>
        /// Creates a new item visual with the current styling.
        /// </summary>
        /// <returns>Newly created item visual.</returns>
        ComboBoxItemVisual CreateItemVisual() {
            ComboBoxItemVisual entry = new ComboBoxItemVisual(OwnerCore ?? throw new InvalidOperationException("Combo-box visuals require an owning core."), FontValue, ListRoot.LayerMask, ListBackgroundOrder, ListTextOrder);
            entry.Background.FillColor = ThemeManager.Colors.SurfaceInput;
            entry.Background.BorderColor = ThemeManager.Colors.AccentTertiary;
            entry.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            return entry;
        }

        /// <summary>
        /// Updates the visuals for all active item rows.
        /// </summary>
        void UpdateAllItemStates() {
            int count = Math.Min(ItemsValue.Count, ItemVisuals.Count);
            for (int i = 0; i < count; i++) {
                UpdateItemVisualState(ItemVisuals[i], i == SelectedIndexValue);
            }
        }

        /// <summary>
        /// Updates the background color for a single item row.
        /// </summary>
        /// <param name="entry">Item visual entry to update.</param>
        /// <param name="isSelected">True when the item is selected.</param>
        void UpdateItemVisualState(ComboBoxItemVisual entry, bool isSelected) {
            if (entry.IsPressed) {
                entry.Background.FillColor = ThemeManager.Colors.AccentSecondary;
            } else if (entry.IsHovering) {
                entry.Background.FillColor = ThemeManager.Colors.AccentPrimary;
            } else if (isSelected) {
                entry.Background.FillColor = ThemeManager.Colors.AccentTertiary;
            } else {
                entry.Background.FillColor = ThemeManager.Colors.SurfaceInput;
            }
        }

        /// <summary>
        /// Updates visibility and state for the drop-down list.
        /// </summary>
        void UpdateDropdownVisibility() {
            if (ListRoot == null) {
                return;
            }

            bool shouldShow = IsOpenValue && ItemsValue.Count > 0;
            ListRoot.Enabled = shouldShow;
            UpdateListLayout();
            if (!shouldShow) {
                HideItemVisuals();
                ResetItemStates();
            }

            UpdateMainVisual();
        }

        /// <summary>
        /// Resets hover and press state for all items.
        /// </summary>
        void ResetItemStates() {
            for (int i = 0; i < ItemVisuals.Count; i++) {
                ComboBoxItemVisual entry = ItemVisuals[i];
                entry.IsHovering = false;
                entry.IsPressed = false;
            }
        }

        /// <summary>
        /// Disables item visuals and clears their label content.
        /// </summary>
        void HideItemVisuals() {
            for (int i = 0; i < ItemVisuals.Count; i++) {
                ComboBoxItemVisual entry = ItemVisuals[i];
                entry.Root.Enabled = false;
                entry.LabelHost.Enabled = false;
                entry.Label.Text = string.Empty;
                entry.Label.Size = new int2(0, 0);
            }
        }

        /// <summary>
        /// Updates the main control fill color based on interaction state.
        /// </summary>
        void UpdateMainVisual() {
            if (Background == null) {
                return;
            }

            Background.BorderColor = IsKeyboardFocused
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.AccentTertiary;
            if (ListBackground != null) {
                ListBackground.BorderColor = Background.BorderColor;
            }

            if (IsPressed || IsOpenValue) {
                Background.FillColor = ThemeManager.Colors.AccentSecondary;
            } else if (IsHovering) {
                Background.FillColor = ThemeManager.Colors.AccentPrimary;
            } else {
                Background.FillColor = ThemeManager.Colors.SurfaceInput;
            }
        }

        /// <summary>
        /// Handles cursor interaction for the main control.
        /// </summary>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer delta since the last event.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleMainCursorEvent(int2 relPos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    if (!IsHovering) {
                        IsHovering = true;
                        UpdateMainVisual();
                    }
                    break;
                case PointerInteraction.Press:
                    IsPressed = true;
                    UpdateMainVisual();
                    break;
                case PointerInteraction.Release:
                    bool shouldToggle = IsPressed && IsHovering;
                    IsPressed = false;
                    UpdateMainVisual();
                    if (shouldToggle && ItemsValue.Count > 0) {
                        IsOpen = !IsOpenValue;
                    }
                    break;
                case PointerInteraction.Leave:
                    if (IsHovering || IsPressed) {
                        IsHovering = false;
                        IsPressed = false;
                        UpdateMainVisual();
                    }
                    break;
                case PointerInteraction.None:
                    break;
            }
        }

        /// <summary>
        /// Handles cursor interaction for a drop-down item row.
        /// </summary>
        /// <param name="entry">Item entry receiving the interaction.</param>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer delta since the last event.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleItemCursorEvent(ComboBoxItemVisual entry, int2 relPos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    entry.IsHovering = true;
                    UpdateItemVisualState(entry, entry.Index == SelectedIndexValue);
                    break;
                case PointerInteraction.Press:
                    entry.IsPressed = true;
                    UpdateItemVisualState(entry, entry.Index == SelectedIndexValue);
                    break;
                case PointerInteraction.Release:
                    bool shouldSelect = entry.IsPressed && entry.IsHovering;
                    entry.IsPressed = false;
                    UpdateItemVisualState(entry, entry.Index == SelectedIndexValue);
                    if (shouldSelect) {
                        SetSelectedIndexInternal(entry.Index, true);
                        IsOpen = false;
                    }
                    break;
                case PointerInteraction.Leave:
                    entry.IsHovering = false;
                    entry.IsPressed = false;
                    UpdateItemVisualState(entry, entry.Index == SelectedIndexValue);
                    break;
                case PointerInteraction.None:
                    break;
            }
        }

        /// <summary>
        /// Finds the clip rectangle of the nearest ancestor clip region constraining this combo box.
        /// </summary>
        /// <param name="clipRect">Receives the nearest ancestor clip rectangle when one exists.</param>
        /// <returns>True when an ancestor clip region was found.</returns>
        bool TryFindNearestAncestorClipRect(out float4 clipRect) {
            Entity current = Parent;
            while (current != null) {
                if (current.Components != null) {
                    for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                        if (current.Components[componentIndex] is IClipRegion2D clipRegion) {
                            clipRect = clipRegion.GetClipRect();
                            return true;
                        }
                    }
                }

                current = current.Parent;
            }

            clipRect = default;
            return false;
        }

        /// <summary>
        /// Calculates a rounded corner radius based on the control size.
        /// </summary>
        /// <param name="size">Size used to derive the radius.</param>
        /// <returns>Rounded corner radius.</returns>
        float GetCornerRadius(int2 size) {
            double minAxis = Math.Min(size.X, size.Y);
            return (float)(minAxis * 0.15);
        }

        /// <summary>
        /// Determines whether the pointer is inside the combo box or its drop-down list.
        /// </summary>
        /// <param name="mouseX">Pointer X coordinate in window space.</param>
        /// <param name="mouseY">Pointer Y coordinate in window space.</param>
        /// <returns>True when the pointer is inside the combo box bounds.</returns>
        bool IsPointerInsideCombo(int mouseX, int mouseY) {
            if (Interactable == null) {
                return false;
            }

            ICamera camera = FindTopmostCameraAt(mouseX, mouseY, Parent.LayerMask);
            if (camera == null) {
                return false;
            }

            // Route the pointer through the shared hit-resolver conversion instead of subtracting the camera
            // viewport directly: panel-content cameras position their world content at screen coordinates, so
            // a raw viewport subtraction misjudged clicks inside the open list as outside and closed the
            // drop-down on press before the item release could apply the selection.
            PointerInteractableHitResolver.GetRelativePointerForInteractable(Interactable, mouseX, mouseY, camera, out int relativeX, out int relativeY);
            if (relativeX >= 0 && relativeX < SizeValue.X && relativeY >= 0 && relativeY < SizeValue.Y) {
                return true;
            }

            if (!IsOpenValue) {
                return false;
            }

            int listHeight = ItemHeight * ItemsValue.Count;
            if (listHeight <= 0) {
                return false;
            }

            float3 comboOrigin = Parent.Position;
            float3 listOrigin = ListRoot.Position;
            double listRelativeX = relativeX - (listOrigin.X - comboOrigin.X);
            double listRelativeY = relativeY - (listOrigin.Y - comboOrigin.Y);
            return listRelativeX >= 0 && listRelativeX < SizeValue.X && listRelativeY >= 0 && listRelativeY < listHeight;
        }

        /// <summary>
        /// Finds the topmost camera containing the given screen coordinates and layer mask.
        /// </summary>
        /// <param name="x">Screen X coordinate.</param>
        /// <param name="y">Screen Y coordinate.</param>
        /// <param name="layerMask">Layer mask the camera must include.</param>
        /// <returns>Camera containing the point, or null if none are found.</returns>
        ICamera FindTopmostCameraAt(int x, int y, ushort layerMask) {
            List<ICamera> cameras = OwnerCore.ObjectManager.Cameras;
            for (int i = cameras.Count - 1; i >= 0; i--) {
                ICamera camera = cameras[i];
                if ((camera.LayerMask & layerMask) == 0) {
                    continue;
                }

                float4 vp = camera.Viewport;
                if (x >= vp.X && x < vp.X + vp.Z && y >= vp.Y && y < vp.Y + vp.W) {
                    return camera;
                }
            }

            return null;
        }

    }
}


