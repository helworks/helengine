namespace helengine {
    /// <summary>
    /// Stores baked menu metadata and drives runtime navigation against the generated menu hierarchy.
    /// </summary>
    public class MenuComponent : UpdateComponent {
        /// <summary>
        /// Current payload version used by scene persistence for the baked menu root component.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id used by baked menu scene records.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.MenuComponent";

        /// <summary>
        /// Baked panels keyed by stable panel id.
        /// </summary>
        readonly Dictionary<string, MenuPanelRuntime> PanelsById;

        /// <summary>
        /// Baked panels stored in deterministic iteration order for runtime activation updates.
        /// </summary>
        readonly List<MenuPanelRuntime> PanelRuntimes;

        /// <summary>
        /// History stack used by Back actions.
        /// </summary>
        readonly List<string> PanelHistory;

        /// <summary>
        /// Backing field for the authored provider type name.
        /// </summary>
        string ProviderTypeNameValue;

        /// <summary>
        /// Backing field for the authored initial panel id.
        /// </summary>
        string InitialPanelIdValue;

        /// <summary>
        /// Active baked panel runtime.
        /// </summary>
        MenuPanelRuntime ActivePanel;

        /// <summary>
        /// Menu item that owns the active pointer press, if any.
        /// </summary>
        MenuItemRuntime PressedPointerItem;

        /// <summary>
        /// Backing field for the active panel id.
        /// </summary>
        string ActivePanelIdValue;

        /// <summary>
        /// Backing field for the selected item id.
        /// </summary>
        string SelectedItemIdValue;

        /// <summary>
        /// Initializes a new baked menu root component.
        /// </summary>
        public MenuComponent() {
            PanelsById = new Dictionary<string, MenuPanelRuntime>(StringComparer.Ordinal);
            PanelRuntimes = new List<MenuPanelRuntime>();
            PanelHistory = new List<string>();
            ProviderTypeNameValue = string.Empty;
            InitialPanelIdValue = string.Empty;
            ActivePanelIdValue = string.Empty;
            SelectedItemIdValue = string.Empty;
        }

        /// <summary>
        /// Gets or sets the assembly-qualified provider type name used when the baked scene is rebuilt in the editor.
        /// </summary>
        public string ProviderTypeName {
            get { return ProviderTypeNameValue; }
            set { ProviderTypeNameValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the initial panel id selected when the baked menu becomes active.
        /// </summary>
        public string InitialPanelId {
            get { return InitialPanelIdValue; }
            set { InitialPanelIdValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets the currently active panel id.
        /// </summary>
        public string ActivePanelId {
            get { return ActivePanelIdValue; }
        }

        /// <summary>
        /// Gets the currently selected item id.
        /// </summary>
        public string SelectedItemId {
            get { return SelectedItemIdValue; }
        }

        /// <summary>
        /// Gets a value indicating whether the baked runtime hierarchy has been bound successfully.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Binds the baked panel and item hierarchy when the scene is loaded at runtime.
        /// </summary>
        /// <param name="entity">Owning menu root entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (string.IsNullOrWhiteSpace(InitialPanelIdValue)) {
                throw new InvalidOperationException("Menu components require an initial panel id.");
            }
        }

        /// <summary>
        /// Routes keyboard and gamepad input through the baked menu hierarchy.
        /// </summary>
        public override void Update() {
            if (!IsInitialized) {
                TryInitialize();
                if (!IsInitialized) {
                    return;
                }
            }

            InputSystem inputSystem = Core.Instance.Input;
            HandleKeyboardInput(inputSystem);
            HandleMouseInput(inputSystem);
            HandleGamepadInput(inputSystem);
        }

        /// <summary>
        /// Attempts to bind the baked runtime hierarchy after the full scene subtree has been loaded.
        /// </summary>
        void TryInitialize() {
            if (Parent == null) {
                return;
            }

            Entity generatedRootEntity = FindGeneratedRootEntity(Parent);
            if (generatedRootEntity == null) {
                return;
            }

            BindPanels(Parent);
            ActivatePanel(InitialPanelIdValue, false);
            IsInitialized = true;
        }

        /// <summary>
        /// Scans the baked scene hierarchy and binds every serialized panel and item runtime record.
        /// </summary>
        /// <param name="rootEntity">Owning menu root entity.</param>
        void BindPanels(Entity rootEntity) {
            PanelsById.Clear();
            PanelRuntimes.Clear();
            PanelHistory.Clear();

            Entity generatedRootEntity = FindGeneratedRootEntity(rootEntity);
            if (generatedRootEntity == null) {
                throw new InvalidOperationException($"Menu root '{DescribeEntity(rootEntity)}' is missing the generated menu subtree.");
            }

            List<Entity> panelEntities = new List<Entity>();
            CollectEntitiesWithComponent<MenuPanelComponent>(generatedRootEntity, panelEntities);
            for (int panelIndex = 0; panelIndex < panelEntities.Count; panelIndex++) {
                Entity panelEntity = panelEntities[panelIndex];
                MenuPanelComponent panelComponent = FindRequiredComponent<MenuPanelComponent>(panelEntity);
                TextComponent selectedDescriptionText = ResolveSelectedDescriptionText(panelEntity);
                MenuItemRuntime[] itemRuntimes = BindItems(panelEntity, panelComponent.PanelId);
                ScrollComponent itemsScrollComponent = ResolveItemsScrollComponent(panelEntity, panelComponent.PanelId);
                itemsScrollComponent.ItemCount = itemRuntimes.Length;
                itemsScrollComponent.ClipOriginEntity = ResolveItemsViewportEntity(itemsScrollComponent, panelComponent.PanelId);
                MenuPanelRuntime panelRuntime = new MenuPanelRuntime(
                    panelComponent,
                    panelEntity,
                    selectedDescriptionText,
                    itemsScrollComponent.Parent,
                    itemsScrollComponent,
                    itemRuntimes);
                panelRuntime.ItemsScrollComponent.ScrollOffsetChanged += HandleItemsScrollOffsetChanged;
                ApplyItemsScrollOffset(panelRuntime.ItemsRootEntity, panelRuntime.ItemsScrollComponent.ScrollOffset);
                if (PanelsById.ContainsKey(panelComponent.PanelId)) {
                    throw new InvalidOperationException($"Duplicate baked menu panel id '{panelComponent.PanelId}' was found.");
                }

                PanelsById.Add(panelComponent.PanelId, panelRuntime);
                PanelRuntimes.Add(panelRuntime);
            }

            if (PanelsById.Count == 0) {
                throw new InvalidOperationException("The baked menu scene does not contain any panel metadata.");
            }
        }

        /// <summary>
        /// Binds the baked item metadata records contained by one panel root.
        /// </summary>
        /// <param name="panelEntity">Panel root whose baked items should be bound.</param>
        /// <param name="panelId">Stable panel id expected for every bound item.</param>
        /// <returns>Bound baked item runtime records.</returns>
        MenuItemRuntime[] BindItems(Entity panelEntity, string panelId) {
            List<Entity> itemEntities = new List<Entity>();
            CollectEntitiesWithComponent<MenuItemComponent>(panelEntity, itemEntities);
            MenuItemRuntime[] itemRuntimes = new MenuItemRuntime[itemEntities.Count];
            for (int itemIndex = 0; itemIndex < itemEntities.Count; itemIndex++) {
                Entity itemEntity = itemEntities[itemIndex];
                MenuItemComponent itemComponent = FindRequiredComponent<MenuItemComponent>(itemEntity);
                if (!string.Equals(itemComponent.PanelId, panelId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Baked menu item '{itemComponent.ItemId}' does not match panel '{panelId}'.");
                }

                RoundedRectComponent backgroundComponent = FindRequiredComponent<RoundedRectComponent>(itemEntity);
                itemRuntimes[itemIndex] = new MenuItemRuntime(itemComponent, itemIndex, itemEntity, backgroundComponent);
            }

            if (itemRuntimes.Length == 0) {
                throw new InvalidOperationException($"Baked menu panel '{panelId}' does not contain any items.");
            }

            return itemRuntimes;
        }

        /// <summary>
        /// Activates one baked panel and refreshes its selection state.
        /// </summary>
        /// <param name="panelId">Panel id to activate.</param>
        /// <param name="pushHistory">True when the current panel should be recorded in the back stack.</param>
        void ActivatePanel(string panelId, bool pushHistory) {
            if (!PanelsById.TryGetValue(panelId, out MenuPanelRuntime nextPanel)) {
                throw new InvalidOperationException($"Baked menu panel '{panelId}' was not registered.");
            }

            if (ActivePanel != null) {
                if (pushHistory && !string.Equals(ActivePanel.Definition.PanelId, panelId, StringComparison.Ordinal)) {
                    PanelHistory.Add(ActivePanel.Definition.PanelId);
                }
            }

            for (int panelIndex = 0; panelIndex < PanelRuntimes.Count; panelIndex++) {
                MenuPanelRuntime panelRuntime = PanelRuntimes[panelIndex];
                panelRuntime.RootEntity.Enabled = false;
                ClearSelectionVisuals(panelRuntime);
            }

            ActivePanel = nextPanel;
            ActivePanel.RootEntity.Enabled = true;
            ActivePanelIdValue = nextPanel.Definition.PanelId;
            PressedPointerItem = null;
            SetSelection(nextPanel, ResolveSelectedIndex(nextPanel));
        }

        /// <summary>
        /// Moves selection through the active baked panel by the supplied signed delta.
        /// </summary>
        /// <param name="delta">Signed movement amount.</param>
        void MoveSelection(int delta) {
            if (ActivePanel == null || ActivePanel.Items.Length == 0) {
                return;
            }
            if (delta == 0) {
                return;
            }

            int nextIndex = ActivePanel.SelectedItemIndex;
            if (nextIndex < 0) {
                nextIndex = 0;
            }

            nextIndex += delta;
            if (nextIndex < 0) {
                nextIndex = ActivePanel.Items.Length - 1;
            } else if (nextIndex >= ActivePanel.Items.Length) {
                nextIndex = 0;
            }

            SetSelection(ActivePanel, nextIndex);
        }

        /// <summary>
        /// Applies the selected-state visuals and description text for one baked item.
        /// </summary>
        /// <param name="panelRuntime">Panel that owns the item.</param>
        /// <param name="itemIndex">Enabled-item index to select.</param>
        void SetSelection(MenuPanelRuntime panelRuntime, int itemIndex) {
            if (panelRuntime == null) {
                throw new ArgumentNullException(nameof(panelRuntime));
            }
            if (itemIndex < 0 || itemIndex >= panelRuntime.Items.Length) {
                throw new ArgumentOutOfRangeException(nameof(itemIndex), "Selected baked menu item index must be valid.");
            }

            panelRuntime.SelectedItemIndex = itemIndex;
            for (int index = 0; index < panelRuntime.Items.Length; index++) {
                MenuItemRuntime runtimeItem = panelRuntime.Items[index];
                bool isSelected = index == itemIndex;
                ApplyItemVisualState(runtimeItem, isSelected);
            }

            MenuItemRuntime selectedItem = panelRuntime.Items[itemIndex];
            SelectedItemIdValue = selectedItem.Definition.ItemId;
            panelRuntime.SelectedDescriptionText.Text = selectedItem.Definition.Description;
            EnsureSelectedItemVisible(panelRuntime, itemIndex);
            ApplyItemsScrollOffset(panelRuntime.ItemsRootEntity, panelRuntime.ItemsScrollComponent.ScrollOffset);
        }

        /// <summary>
        /// Executes the action associated with the currently selected baked item.
        /// </summary>
        /// <param name="key">Logical activation key routed to the active item.</param>
        void ConfirmSelection(Keys key) {
            if (ActivePanel == null) {
                return;
            }
            if (ActivePanel.SelectedItemIndex < 0 || ActivePanel.SelectedItemIndex >= ActivePanel.Items.Length) {
                return;
            }

            ExecuteAction(ActivePanel.Items[ActivePanel.SelectedItemIndex].Definition);
        }

        /// <summary>
        /// Executes one baked menu action.
        /// </summary>
        /// <param name="itemComponent">Serialized item metadata whose action should be executed.</param>
        void ExecuteAction(MenuItemComponent itemComponent) {
            if (itemComponent == null) {
                throw new ArgumentNullException(nameof(itemComponent));
            }

            if (itemComponent.ActionKind == MenuActionKind.None) {
                return;
            } else if (itemComponent.ActionKind == MenuActionKind.OpenPanel) {
                ActivatePanel(itemComponent.TargetId, true);
            } else if (itemComponent.ActionKind == MenuActionKind.LoadScene) {
                LoadScene(itemComponent.TargetId);
            } else if (itemComponent.ActionKind == MenuActionKind.Back) {
                NavigateBack();
            } else {
                throw new InvalidOperationException($"Unsupported baked menu action kind '{itemComponent.ActionKind}'.");
            }
        }

        /// <summary>
        /// Returns to the previous panel recorded in the baked menu history stack.
        /// </summary>
        void NavigateBack() {
            if (PanelHistory.Count == 0) {
                return;
            }

            string previousPanelId = PanelHistory[PanelHistory.Count - 1];
            PanelHistory.RemoveAt(PanelHistory.Count - 1);
            ActivatePanel(previousPanelId, false);
        }

        /// <summary>
        /// Loads one scene targeted by the baked menu using the editor resolver path or the runtime scene manager.
        /// </summary>
        /// <param name="sceneId">Stable scene id targeted by the menu item.</param>
        void LoadScene(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new InvalidOperationException("Scene-loading baked menu items must provide a scene id.");
            }
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before loading a scene from the baked menu.");
            }
            if (Core.Instance.SceneManager == null) {
                throw new InvalidOperationException("Core scene manager must be initialized before runtime menu scene loading can occur.");
            }

            Core.Instance.SceneManager.LoadScene(sceneId, SceneLoadMode.Single);
            if (ComponentExecutionContext.CurrentMode == ComponentExecutionMode.Editor
                && Parent != null) {
                Parent.Enabled = false;
            }
        }

        /// <summary>
        /// Handles keyboard navigation and activation for the active baked panel.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        void HandleKeyboardInput(InputSystem inputSystem) {
            if (inputSystem.WasKeyPressed(Keys.Up) || inputSystem.WasKeyPressed(Keys.W)) {
                MoveSelection(-1);
            } else if (inputSystem.WasKeyPressed(Keys.Down) || inputSystem.WasKeyPressed(Keys.S)) {
                MoveSelection(1);
            } else if (inputSystem.WasKeyPressed(Keys.Enter)) {
                ConfirmSelection(Keys.Enter);
            } else if (inputSystem.WasKeyPressed(Keys.Space)) {
                ConfirmSelection(Keys.Space);
            } else if (inputSystem.WasKeyPressed(Keys.Escape) || inputSystem.WasKeyPressed(Keys.Back)) {
                NavigateBack();
            }
        }

        /// <summary>
        /// Handles pointer hover and click activation for the active baked panel.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        void HandleMouseInput(InputSystem inputSystem) {
            if (ActivePanel == null) {
                PressedPointerItem = null;
                return;
            }

            MenuItemRuntime hoveredItem = FindHoveredItem(ActivePanel, inputSystem.GetMouseX(), inputSystem.GetMouseY());
            if (hoveredItem != null
                && hoveredItem.Index != ActivePanel.SelectedItemIndex
                && IsMouseHoverSelectionUpdateRequired(inputSystem)) {
                SetSelection(ActivePanel, hoveredItem.Index);
            }

            if (inputSystem.WasMouseLeftButtonPressed()) {
                PressedPointerItem = hoveredItem;
                return;
            }

            if (inputSystem.WasMouseLeftButtonReleased()) {
                if (IsSameRuntimeItem(PressedPointerItem, hoveredItem)) {
                    ExecuteAction(hoveredItem.Definition);
                }

                PressedPointerItem = null;
            }
        }

        /// <summary>
        /// Returns whether the current frame should allow passive mouse hover to retarget selection.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        /// <returns>True when pointer movement or a new press should update hover selection.</returns>
        bool IsMouseHoverSelectionUpdateRequired(InputSystem inputSystem) {
            if (inputSystem == null) {
                throw new ArgumentNullException(nameof(inputSystem));
            }

            if (inputSystem.GetMouseDeltaX() != 0 || inputSystem.GetMouseDeltaY() != 0) {
                return true;
            }

            return inputSystem.WasMouseLeftButtonPressed();
        }

        /// <summary>
        /// Handles d-pad and face-button navigation for the primary gamepad.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        void HandleGamepadInput(InputSystem inputSystem) {
            InputGamepadState currentGamepadState = inputSystem.GetGamepadState(0);
            if (!currentGamepadState.Connected) {
                return;
            }

            if (inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.DPadUp)) {
                MoveSelection(-1);
            } else if (inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.DPadDown)) {
                MoveSelection(1);
            } else if (inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.South)) {
                ConfirmSelection(Keys.Enter);
            } else if (inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.East)
                || inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.Select)) {
                NavigateBack();
            }
        }

        /// <summary>
        /// Resolves the active baked menu item currently under the supplied pointer position.
        /// </summary>
        /// <param name="panelRuntime">Panel whose items should be tested.</param>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <returns>Hovered baked menu item, or null when the pointer is outside every row.</returns>
        MenuItemRuntime FindHoveredItem(MenuPanelRuntime panelRuntime, int pointerX, int pointerY) {
            if (panelRuntime == null) {
                throw new ArgumentNullException(nameof(panelRuntime));
            }

            for (int itemIndex = 0; itemIndex < panelRuntime.Items.Length; itemIndex++) {
                MenuItemRuntime runtimeItem = panelRuntime.Items[itemIndex];
                if (!ContainsPointer(runtimeItem, pointerX, pointerY)) {
                    continue;
                }

                return runtimeItem;
            }

            return null;
        }

        /// <summary>
        /// Returns whether the supplied pointer lies inside one baked menu row background.
        /// </summary>
        /// <param name="runtimeItem">Runtime item whose row bounds should be evaluated.</param>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <returns>True when the pointer is inside the row bounds.</returns>
        bool ContainsPointer(MenuItemRuntime runtimeItem, int pointerX, int pointerY) {
            if (runtimeItem == null) {
                throw new ArgumentNullException(nameof(runtimeItem));
            }

            float3 position = runtimeItem.Entity.Position;
            int width = runtimeItem.Background.Size.X;
            int height = runtimeItem.Background.Size.Y;
            return pointerX >= position.X
                && pointerX < position.X + width
                && pointerY >= position.Y
                && pointerY < position.Y + height;
        }

        /// <summary>
        /// Returns whether two baked runtime item references point at the same logical menu row.
        /// </summary>
        /// <param name="left">First runtime item to compare.</param>
        /// <param name="right">Second runtime item to compare.</param>
        /// <returns>True when both runtime items identify the same panel row.</returns>
        bool IsSameRuntimeItem(MenuItemRuntime left, MenuItemRuntime right) {
            if (left == null || right == null) {
                return false;
            }

            return left.Index == right.Index
                && string.Equals(left.Definition.PanelId, right.Definition.PanelId, StringComparison.Ordinal)
                && string.Equals(left.Definition.ItemId, right.Definition.ItemId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the initial selected index for one panel.
        /// </summary>
        /// <param name="panelRuntime">Panel whose initial selected index should be resolved.</param>
        /// <returns>Resolved initial selected item index.</returns>
        int ResolveSelectedIndex(MenuPanelRuntime panelRuntime) {
            if (panelRuntime.SelectedItemIndex >= 0 && panelRuntime.SelectedItemIndex < panelRuntime.Items.Length) {
                return panelRuntime.SelectedItemIndex;
            }

            return 0;
        }

        /// <summary>
        /// Applies idle or selected colors to one baked menu row background.
        /// </summary>
        /// <param name="runtimeItem">Item whose visual state should be updated.</param>
        /// <param name="isSelected">True when selected-state colors should be applied.</param>
        void ApplyItemVisualState(MenuItemRuntime runtimeItem, bool isSelected) {
            if (isSelected) {
                runtimeItem.Background.FillColor = runtimeItem.Definition.SelectedFillColor;
                runtimeItem.Background.BorderColor = runtimeItem.Definition.SelectedBorderColor;
            } else {
                runtimeItem.Background.FillColor = runtimeItem.Definition.IdleFillColor;
                runtimeItem.Background.BorderColor = runtimeItem.Definition.IdleBorderColor;
            }
        }

        /// <summary>
        /// Clears selected-state visuals for one panel before another panel becomes active.
        /// </summary>
        /// <param name="panelRuntime">Panel whose item visuals should be reset.</param>
        void ClearSelectionVisuals(MenuPanelRuntime panelRuntime) {
            for (int itemIndex = 0; itemIndex < panelRuntime.Items.Length; itemIndex++) {
                ApplyItemVisualState(panelRuntime.Items[itemIndex], false);
            }
        }

        /// <summary>
        /// Resolves the generated menu subtree attached beneath the baked menu root.
        /// </summary>
        /// <param name="rootEntity">Owning menu root entity.</param>
        /// <returns>Generated menu subtree root.</returns>
        Entity FindGeneratedRootEntity(Entity rootEntity) {
            if (rootEntity.Children == null) {
                return null;
            }

            if (rootEntity.Children.Count == 1) {
                return rootEntity.Children[0];
            }

            return null;
        }

        /// <summary>
        /// Resolves the selected-description text component hosted somewhere beneath one panel root.
        /// </summary>
        /// <param name="panelEntity">Panel root to inspect.</param>
        /// <returns>Selected-description text component for the panel.</returns>
        TextComponent ResolveSelectedDescriptionText(Entity panelEntity) {
            List<Entity> markerEntities = new List<Entity>();
            CollectEntitiesWithComponent<MenuSelectedDescriptionComponent>(panelEntity, markerEntities);
            if (markerEntities.Count != 1) {
                throw new InvalidOperationException("Each baked menu panel must contain exactly one selected-description marker.");
            }

            return FindRequiredComponent<TextComponent>(markerEntities[0]);
        }

        /// <summary>
        /// Resolves the reusable row-based scroll component hosted beneath one baked panel.
        /// </summary>
        /// <param name="panelEntity">Panel root whose item-list scroll component should be resolved.</param>
        /// <param name="panelId">Stable panel id used in diagnostics.</param>
        /// <returns>Resolved scroll component.</returns>
        ScrollComponent ResolveItemsScrollComponent(Entity panelEntity, string panelId) {
            List<Entity> scrollEntities = new List<Entity>();
            CollectEntitiesWithComponent<ScrollComponent>(panelEntity, scrollEntities);
            if (scrollEntities.Count != 1) {
                throw new InvalidOperationException($"Baked menu panel '{panelId}' must contain exactly one scroll component.");
            }

            ScrollComponent scrollComponent = FindRequiredComponent<ScrollComponent>(scrollEntities[0]);
            if (scrollComponent.VisibleItemCount < 1) {
                throw new InvalidOperationException($"Baked menu panel '{panelId}' must expose at least one visible item row.");
            }

            return scrollComponent;
        }

        /// <summary>
        /// Resolves the fixed viewport entity that clips one baked panel item list.
        /// </summary>
        /// <param name="scrollComponent">Scroll component hosted by the moving item root.</param>
        /// <param name="panelId">Stable panel id used in diagnostics.</param>
        /// <returns>Viewport entity that should anchor clip and hit-test bounds.</returns>
        Entity ResolveItemsViewportEntity(ScrollComponent scrollComponent, string panelId) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }

            Entity itemsRootEntity = scrollComponent.Parent;
            if (itemsRootEntity == null || itemsRootEntity.Parent == null) {
                throw new InvalidOperationException($"Baked menu panel '{panelId}' must parent its scroll root under a viewport entity.");
            }

            Entity viewportEntity = itemsRootEntity.Parent;
            ClipRectComponent clipComponent = FindFirstComponent<ClipRectComponent>(viewportEntity);
            if (clipComponent == null) {
                throw new InvalidOperationException($"Baked menu panel '{panelId}' must contain a clip viewport above its scroll root.");
            }

            return viewportEntity;
        }

        /// <summary>
        /// Ensures the selected menu row remains inside the currently visible scroll window.
        /// </summary>
        /// <param name="panelRuntime">Panel that owns the selected row.</param>
        /// <param name="selectedItemIndex">Selected enabled-item index.</param>
        void EnsureSelectedItemVisible(MenuPanelRuntime panelRuntime, int selectedItemIndex) {
            if (panelRuntime == null) {
                throw new ArgumentNullException(nameof(panelRuntime));
            }
            if (selectedItemIndex < 0 || selectedItemIndex >= panelRuntime.Items.Length) {
                throw new ArgumentOutOfRangeException(nameof(selectedItemIndex), "Selected baked menu item index must be valid.");
            }

            int visibleItemCount = panelRuntime.ItemsScrollComponent.VisibleItemCount;
            int scrollOffset = panelRuntime.ItemsScrollComponent.ScrollOffset;
            int visibleEndExclusive = scrollOffset + visibleItemCount;
            if (selectedItemIndex < scrollOffset) {
                panelRuntime.ItemsScrollComponent.ScrollTo(selectedItemIndex);
                return;
            }

            if (selectedItemIndex >= visibleEndExclusive) {
                panelRuntime.ItemsScrollComponent.ScrollTo(selectedItemIndex - visibleItemCount + 1);
            }
        }

        /// <summary>
        /// Applies one scroll offset to the baked item-root entity associated with the updated scroll component.
        /// </summary>
        /// <param name="scrollComponent">Scroll component whose offset changed.</param>
        /// <param name="scrollOffset">New offset in item units.</param>
        void HandleItemsScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }

            ApplyItemsScrollOffset(scrollComponent.Parent, scrollOffset);
        }

        /// <summary>
        /// Applies one row-based scroll offset to the baked item-root entity.
        /// </summary>
        /// <param name="itemsRootEntity">Item-root entity translated by the active scroll window.</param>
        /// <param name="scrollOffset">Offset in item units.</param>
        void ApplyItemsScrollOffset(Entity itemsRootEntity, int scrollOffset) {
            if (itemsRootEntity == null) {
                throw new ArgumentNullException(nameof(itemsRootEntity));
            }

            float itemStep = ResolveItemsScrollStep(itemsRootEntity);
            itemsRootEntity.LocalPosition = new float3(
                0f,
                -scrollOffset * itemStep,
                0f);
        }

        /// <summary>
        /// Resolves the row-to-row scroll step from the baked item-root subtree instead of relying on authored layout constants.
        /// </summary>
        /// <param name="itemsRootEntity">Item-root entity whose child layout should define the scroll step.</param>
        /// <returns>Resolved per-item scroll step in local pixels.</returns>
        float ResolveItemsScrollStep(Entity itemsRootEntity) {
            if (itemsRootEntity.Children == null || itemsRootEntity.Children.Count == 0) {
                return DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing;
            }

            if (itemsRootEntity.Children.Count >= 2) {
                float step = itemsRootEntity.Children[1].LocalPosition.Y - itemsRootEntity.Children[0].LocalPosition.Y;
                if (step > 0f) {
                    return step;
                }
            }

            RoundedRectComponent background = FindFirstComponent<RoundedRectComponent>(itemsRootEntity.Children[0]);
            if (background != null && background.Size.Y > 0) {
                return background.Size.Y;
            }

            return DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing;
        }

        /// <summary>
        /// Recursively collects entities that contain one required component type.
        /// </summary>
        /// <typeparam name="TComponent">Component type that marks collected entities.</typeparam>
        /// <param name="entity">Root entity to inspect.</param>
        /// <param name="entities">Destination list receiving matching entities.</param>
        void CollectEntitiesWithComponent<TComponent>(Entity entity, List<Entity> entities) where TComponent : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            if (TryFindComponent<TComponent>(entity, out TComponent component)) {
                entities.Add(entity);
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                CollectEntitiesWithComponent<TComponent>(entity.Children[childIndex], entities);
            }
        }

        /// <summary>
        /// Finds one required component on the supplied entity.
        /// </summary>
        /// <typeparam name="TComponent">Component type to resolve.</typeparam>
        /// <param name="entity">Entity that must own the component.</param>
        /// <returns>Resolved component instance.</returns>
        TComponent FindRequiredComponent<TComponent>(Entity entity) where TComponent : Component {
            if (TryFindComponent<TComponent>(entity, out TComponent component)) {
                return component;
            }

            throw new InvalidOperationException($"Entity '{DescribeEntity(entity)}' is missing required component '{typeof(TComponent).Name}'.");
        }

        /// <summary>
        /// Finds the first component of the requested type on one entity and returns null when it is absent.
        /// </summary>
        /// <typeparam name="TComponent">Component type to resolve.</typeparam>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Resolved component when present; otherwise null.</returns>
        TComponent FindFirstComponent<TComponent>(Entity entity) where TComponent : Component {
            if (TryFindComponent<TComponent>(entity, out TComponent component)) {
                return component;
            }

            return null;
        }

        /// <summary>
        /// Builds a readable identifier for one entity used in diagnostics across editor and runtime paths.
        /// </summary>
        /// <param name="entity">Entity to describe.</param>
        /// <returns>Readable entity identifier.</returns>
        string DescribeEntity(Entity entity) {
            return entity.GetType().Name;
        }

        /// <summary>
        /// Attempts to resolve one component type from the supplied entity.
        /// </summary>
        /// <typeparam name="TComponent">Component type to resolve.</typeparam>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="component">Resolved component when one exists.</param>
        /// <returns>True when the component was found.</returns>
        bool TryFindComponent<TComponent>(Entity entity, out TComponent component) where TComponent : Component {
            if (entity != null && entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is TComponent typedComponent) {
                        component = typedComponent;
                        return true;
                    }
                }
            }

            component = null;
            return false;
        }

    }
}
