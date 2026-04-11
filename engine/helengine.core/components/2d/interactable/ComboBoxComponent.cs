namespace helengine {
    /// <summary>
    /// Renders a selectable combo box with a drop-down list of items.
    /// </summary>
    public class ComboBoxComponent : Component {
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
        readonly List<string> items;
        /// <summary>
        /// Cached visuals for each item row.
        /// </summary>
        readonly List<ComboBoxItemVisual> itemVisuals;

        /// <summary>
        /// Font used to render text in the control.
        /// </summary>
        FontAsset font;
        /// <summary>
        /// Cached size of the combo box control.
        /// </summary>
        int2 size;
        /// <summary>
        /// Height of each item row.
        /// </summary>
        int itemHeight;
        /// <summary>
        /// Index of the currently selected item.
        /// </summary>
        int selectedIndex;
        /// <summary>
        /// Tracks whether the drop-down list is open.
        /// </summary>
        bool isOpen;
        /// <summary>
        /// Tracks whether the main control is hovered.
        /// </summary>
        bool isHovering;
        /// <summary>
        /// Tracks whether the main control is pressed.
        /// </summary>
        bool isPressed;

        /// <summary>
        /// Background shape for the main control.
        /// </summary>
        RoundedRectComponent background;
        /// <summary>
        /// Text component for the selected item label.
        /// </summary>
        TextComponent labelText;
        /// <summary>
        /// Text component for the arrow glyph.
        /// </summary>
        TextComponent arrowText;
        /// <summary>
        /// Interactable region for the main control.
        /// </summary>
        InteractableComponent interactable;

        /// <summary>
        /// Entity hosting the selected item label.
        /// </summary>
        Entity labelEntity;
        /// <summary>
        /// Entity hosting the arrow glyph.
        /// </summary>
        Entity arrowEntity;
        /// <summary>
        /// Root entity for the drop-down list.
        /// </summary>
        Entity listRoot;
        /// <summary>
        /// Background for the drop-down list.
        /// </summary>
        RoundedRectComponent listBackground;

        /// <summary>
        /// Render order for the main background.
        /// </summary>
        byte backgroundOrder;
        /// <summary>
        /// Render order for the main text elements.
        /// </summary>
        byte textOrder;
        /// <summary>
        /// Render order for the list background.
        /// </summary>
        byte listBackgroundOrder;
        /// <summary>
        /// Render order for item labels.
        /// </summary>
        byte listTextOrder;

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

            this.size = size;
            this.font = font;
            this.items = new List<string>(items.Count);
            itemVisuals = new List<ComboBoxItemVisual>(items.Count);
            itemHeight = size.Y;

            CopyItems(items);
            this.selectedIndex = ValidateSelectedIndex(this.items.Count, selectedIndex);
        }

        /// <summary>
        /// Gets or sets the size of the combo box control.
        /// </summary>
        public int2 Size {
            get { return size; }
            set {
                if (value.X <= 0 || value.Y <= 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "ComboBox size must be positive.");
                }

                size = value;
                itemHeight = size.Y;
                UpdateLayout();
            }
        }

        /// <summary>
        /// Gets or sets the font used to render labels.
        /// </summary>
        public FontAsset Font {
            get { return font; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                font = value;
                UpdateLabelText();
                UpdateLayout();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the drop-down list is open.
        /// </summary>
        public bool IsOpen {
            get { return isOpen; }
            set {
                if (value && items.Count == 0) {
                    isOpen = false;
                    UpdateDropdownVisibility();
                    return;
                }
                if (isOpen == value) {
                    return;
                }

                isOpen = value;
                UpdateDropdownVisibility();
            }
        }

        /// <summary>
        /// Gets the current list of items.
        /// </summary>
        public IReadOnlyList<string> Items => items;

        /// <summary>
        /// Gets a value indicating whether the combo box has a selection.
        /// </summary>
        public bool HasSelection => selectedIndex >= 0 && selectedIndex < items.Count;

        /// <summary>
        /// Gets or sets the selected index, or -1 for no selection.
        /// </summary>
        public int SelectedIndex {
            get { return selectedIndex; }
            set { SetSelectedIndexInternal(value, true); }
        }

        /// <summary>
        /// Gets the selected item text.
        /// </summary>
        public string SelectedItem {
            get {
                if (!HasSelection) {
                    throw new InvalidOperationException("ComboBox has no selected item.");
                }

                return items[selectedIndex];
            }
        }

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
            bool selectionChanged = this.selectedIndex != validatedIndex;

            this.items.Clear();
            for (int i = 0; i < items.Count; i++) {
                this.items.Add(items[i]);
            }

            this.selectedIndex = validatedIndex;
            if (this.items.Count == 0 && isOpen) {
                isOpen = false;
            }

            UpdateLabelText();
            UpdateLayout();
            UpdateDropdownVisibility();

            if (selectionChanged && HasSelection && SelectionChanged != null) {
                SelectionChanged(this.selectedIndex, this.items[this.selectedIndex]);
            }
        }

        /// <summary>
        /// Builds the visual tree and hooks up input handlers.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            backgroundOrder = RenderOrder2D.PanelSurface;
            textOrder = RenderOrder2D.PanelForeground;
            listBackgroundOrder = RenderOrder2D.OverlayBackground;
            listTextOrder = RenderOrder2D.OverlayForeground;

            background = new RoundedRectComponent();
            background.Size = size;
            background.Radius = GetCornerRadius(size);
            background.BorderThickness = 2f;
            background.FillColor = ThemeManager.Colors.SurfaceInput;
            background.BorderColor = ThemeManager.Colors.AccentTertiary;
            background.RenderOrder2D = backgroundOrder;
            entity.AddComponent(background);

            interactable = new InteractableComponent();
            interactable.Size = size;
            interactable.CursorEvent += HandleMainCursorEvent;
            entity.AddComponent(interactable);

            if (entity.Children == null) {
                entity.InitChildren();
            }

            labelEntity = new Entity();
            labelEntity.LayerMask = entity.LayerMask;
            labelEntity.Enabled = true;
            labelEntity.InitComponents();
            entity.AddChild(labelEntity);

            labelText = new TextComponent();
            labelText.Font = font;
            labelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            labelText.RenderOrder2D = textOrder;
            labelEntity.AddComponent(labelText);

            arrowEntity = new Entity();
            arrowEntity.LayerMask = entity.LayerMask;
            arrowEntity.Enabled = true;
            arrowEntity.InitComponents();
            entity.AddChild(arrowEntity);

            arrowText = new TextComponent();
            arrowText.Font = font;
            arrowText.Color = ThemeManager.Colors.InputForegroundSecondary;
            arrowText.RenderOrder2D = textOrder;
            arrowEntity.AddComponent(arrowText);

            listRoot = new Entity();
            listRoot.LayerMask = entity.LayerMask;
            listRoot.InitComponents();
            listRoot.InitChildren();
            entity.AddChild(listRoot);

            listBackground = new RoundedRectComponent();
            listBackground.RenderOrder2D = listBackgroundOrder;
            listBackground.BorderThickness = 1f;
            listBackground.FillColor = ThemeManager.Colors.SurfacePrimary;
            listBackground.BorderColor = ThemeManager.Colors.AccentTertiary;
            listRoot.AddComponent(listBackground);

            var updateComponent = new ComboBoxUpdateComponent(this);
            updateComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            entity.AddComponent(updateComponent);

            EnsureItemVisuals(items.Count);
            UpdateLabelText();
            UpdateLayout();
            UpdateDropdownVisibility();
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
                items.Add(source[i]);
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
            int validated = ValidateSelectedIndex(items.Count, index);
            if (selectedIndex == validated) {
                return;
            }

            selectedIndex = validated;
            UpdateLabelText();
            UpdateAllItemStates();

            if (raiseEvent && HasSelection && SelectionChanged != null) {
                SelectionChanged(selectedIndex, items[selectedIndex]);
            }
        }

        /// <summary>
        /// Updates the selected item label and arrow glyph text.
        /// </summary>
        void UpdateLabelText() {
            if (labelText == null || arrowText == null) {
                return;
            }

            string displayText = HasSelection ? items[selectedIndex] : string.Empty;
            labelText.Text = displayText;
            labelText.Color = HasSelection
                ? ThemeManager.Colors.InputForegroundPrimary
                : ThemeManager.Colors.InputForegroundSecondary;

            arrowText.Text = ArrowGlyph;
            arrowText.Color = ThemeManager.Colors.InputForegroundSecondary;

            UpdateLabelLayout();
        }

        /// <summary>
        /// Updates input handling for the combo box when open.
        /// </summary>
        public void Update() {
            if (!isOpen || Parent == null || listRoot == null) {
                return;
            }

            InputManager inputManager = Core.Instance.InputManager;
            if (!inputManager.WasMouseLeftButtonPressed()) {
                return;
            }

            int2 mousePosition = inputManager.GetMousePosition();
            if (IsPointerInsideCombo(mousePosition)) {
                return;
            }

            IsOpen = false;
        }

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
            if (background == null || interactable == null) {
                return;
            }

            background.Size = size;
            background.Radius = GetCornerRadius(size);
            interactable.Size = size;
            UpdateLabelLayout();
        }

        /// <summary>
        /// Positions and sizes the selected label and arrow glyph.
        /// </summary>
        void UpdateLabelLayout() {
            if (labelEntity == null || labelText == null || arrowEntity == null || arrowText == null || font == null) {
                return;
            }

            double lineHeight = Math.Max((double)font.LineHeight, 1.0);
            double labelY = Math.Round((size.Y - lineHeight) / 2.0, MidpointRounding.AwayFromZero);

            FontTightMetrics labelMetrics = font.MeasureTight(labelText.Text);
            int labelWidth = (int)Math.Ceiling(labelMetrics.Width);
            int labelHeight = (int)Math.Ceiling(Math.Max((double)labelMetrics.Height, 1.0));
            labelText.Size = new int2(labelWidth, labelHeight);
            labelEntity.Position = new float3(TextPaddingX, (float)labelY, 0.1f);

            FontTightMetrics arrowMetrics = font.MeasureTight(ArrowGlyph);
            int arrowWidth = (int)Math.Ceiling(arrowMetrics.Width);
            int arrowHeight = (int)Math.Ceiling(Math.Max((double)arrowMetrics.Height, 1.0));
            arrowText.Size = new int2(arrowWidth, arrowHeight);

            double arrowX = size.X - ArrowPaddingX - arrowMetrics.Width;
            if (arrowX < TextPaddingX) {
                arrowX = TextPaddingX;
            }
            arrowX = Math.Round(arrowX, MidpointRounding.AwayFromZero);
            arrowEntity.Position = new float3((float)arrowX, (float)labelY, 0.1f);
        }

        /// <summary>
        /// Updates the layout of the drop-down list and its items.
        /// </summary>
        void UpdateListLayout() {
            if (listRoot == null || listBackground == null) {
                return;
            }

            listRoot.Position = new float3(0f, size.Y + ListGap, 0.2f);
            int listHeight = itemHeight * items.Count;
            if (listHeight <= 0) {
                listHeight = 1;
            }
            listBackground.Size = new int2(size.X, listHeight);
            if (background != null) {
                listBackground.Radius = background.Radius;
            }

            EnsureItemVisuals(items.Count);

            double lineHeight = Math.Max((double)font.LineHeight, 1.0);
            bool shouldShow = isOpen && items.Count > 0;
            for (int i = 0; i < itemVisuals.Count; i++) {
                ComboBoxItemVisual entry = itemVisuals[i];
                bool isActive = i < items.Count;
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
                entry.Root.Position = new float3(0f, itemHeight * i, 0.1f);
                entry.Background.Size = new int2(size.X, itemHeight);
                entry.Background.Radius = 0f;
                entry.Background.BorderColor = ThemeManager.Colors.AccentTertiary;
                entry.Interactable.Size = new int2(size.X, itemHeight);

                string itemText = items[i];
                entry.Label.Text = itemText;
                entry.Label.Font = font;
                entry.Label.Color = ThemeManager.Colors.InputForegroundPrimary;

                FontTightMetrics itemMetrics = font.MeasureTight(itemText);
                entry.Label.Size = new int2(
                    (int)Math.Ceiling(itemMetrics.Width),
                    (int)Math.Ceiling(Math.Max((double)itemMetrics.Height, 1.0))
                );

                double textY = Math.Round((itemHeight - lineHeight) / 2.0, MidpointRounding.AwayFromZero);
                entry.LabelHost.Position = new float3(TextPaddingX, (float)textY, 0.1f);
                UpdateItemVisualState(entry, i == selectedIndex);
            }
        }

        /// <summary>
        /// Ensures item visuals are created up to the requested count.
        /// </summary>
        /// <param name="count">Number of visuals required.</param>
        void EnsureItemVisuals(int count) {
            if (listRoot == null) {
                return;
            }

            for (int i = itemVisuals.Count; i < count; i++) {
                ComboBoxItemVisual entry = CreateItemVisual();
                entry.CursorEvent += HandleItemCursorEvent;
                listRoot.AddChild(entry.Root);
                itemVisuals.Add(entry);
            }
        }

        /// <summary>
        /// Creates a new item visual with the current styling.
        /// </summary>
        /// <returns>Newly created item visual.</returns>
        ComboBoxItemVisual CreateItemVisual() {
            ComboBoxItemVisual entry = new ComboBoxItemVisual(font, listRoot.LayerMask, listBackgroundOrder, listTextOrder);
            entry.Background.FillColor = ThemeManager.Colors.SurfaceInput;
            entry.Background.BorderColor = ThemeManager.Colors.AccentTertiary;
            entry.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            return entry;
        }

        /// <summary>
        /// Updates the visuals for all active item rows.
        /// </summary>
        void UpdateAllItemStates() {
            int count = Math.Min(items.Count, itemVisuals.Count);
            for (int i = 0; i < count; i++) {
                UpdateItemVisualState(itemVisuals[i], i == selectedIndex);
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
            if (listRoot == null) {
                return;
            }

            bool shouldShow = isOpen && items.Count > 0;
            listRoot.Enabled = shouldShow;
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
            for (int i = 0; i < itemVisuals.Count; i++) {
                ComboBoxItemVisual entry = itemVisuals[i];
                entry.IsHovering = false;
                entry.IsPressed = false;
            }
        }

        /// <summary>
        /// Disables item visuals and clears their label content.
        /// </summary>
        void HideItemVisuals() {
            for (int i = 0; i < itemVisuals.Count; i++) {
                ComboBoxItemVisual entry = itemVisuals[i];
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
            if (background == null) {
                return;
            }

            if (isPressed || isOpen) {
                background.FillColor = ThemeManager.Colors.AccentSecondary;
            } else if (isHovering) {
                background.FillColor = ThemeManager.Colors.AccentPrimary;
            } else {
                background.FillColor = ThemeManager.Colors.SurfaceInput;
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
                    if (!isHovering) {
                        isHovering = true;
                        UpdateMainVisual();
                    }
                    break;
                case PointerInteraction.Press:
                    isPressed = true;
                    UpdateMainVisual();
                    break;
                case PointerInteraction.Release:
                    bool shouldToggle = isPressed && isHovering;
                    isPressed = false;
                    UpdateMainVisual();
                    if (shouldToggle && items.Count > 0) {
                        IsOpen = !isOpen;
                    }
                    break;
                case PointerInteraction.Leave:
                    if (isHovering || isPressed) {
                        isHovering = false;
                        isPressed = false;
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
                    UpdateItemVisualState(entry, entry.Index == selectedIndex);
                    break;
                case PointerInteraction.Press:
                    entry.IsPressed = true;
                    UpdateItemVisualState(entry, entry.Index == selectedIndex);
                    break;
                case PointerInteraction.Release:
                    bool shouldSelect = entry.IsPressed && entry.IsHovering;
                    entry.IsPressed = false;
                    UpdateItemVisualState(entry, entry.Index == selectedIndex);
                    if (shouldSelect) {
                        SetSelectedIndexInternal(entry.Index, true);
                        IsOpen = false;
                    }
                    break;
                case PointerInteraction.Leave:
                    entry.IsHovering = false;
                    entry.IsPressed = false;
                    UpdateItemVisualState(entry, entry.Index == selectedIndex);
                    break;
                case PointerInteraction.None:
                    break;
            }
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
        /// <param name="mousePosition">Pointer position in window coordinates.</param>
        /// <returns>True when the pointer is inside the combo box bounds.</returns>
        bool IsPointerInsideCombo(int2 mousePosition) {
            ICamera camera = FindTopmostCameraAt(mousePosition.X, mousePosition.Y, Parent.LayerMask);
            if (camera == null) {
                return false;
            }

            float4 viewport = camera.Viewport;
            double localX = mousePosition.X - viewport.X;
            double localY = mousePosition.Y - viewport.Y;

            float3 origin = Parent.Position;
            if (GeometryUtils.IsPointInsideRect(localX, localY, origin, size.X, size.Y)) {
                return true;
            }

            if (!isOpen) {
                return false;
            }

            int listHeight = itemHeight * items.Count;
            if (listHeight <= 0) {
                return false;
            }

            float3 listPosition = listRoot.Position;
            return GeometryUtils.IsPointInsideRect(localX, localY, listPosition, size.X, listHeight);
        }

        /// <summary>
        /// Finds the topmost camera containing the given screen coordinates and layer mask.
        /// </summary>
        /// <param name="x">Screen X coordinate.</param>
        /// <param name="y">Screen Y coordinate.</param>
        /// <param name="layerMask">Layer mask the camera must include.</param>
        /// <returns>Camera containing the point, or null if none are found.</returns>
        ICamera FindTopmostCameraAt(int x, int y, ushort layerMask) {
            List<ICamera> cameras = Core.Instance.ObjectManager.Cameras;
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
