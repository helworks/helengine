namespace helengine {
    /// <summary>
    /// Materializes one reusable multi-panel menu from a user-side menu-definition provider.
    /// </summary>
    public class MenuHostComponent : UpdateComponent {
        /// <summary>
        /// Current payload version used by menu-host scene persistence.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id used by scene persistence.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.MenuHostComponent";

        /// <summary>
        /// Width of the authored menu canvas used by the first-pass demo disc layout.
        /// </summary>
        const int CanvasWidth = 1280;

        /// <summary>
        /// Height of the authored menu canvas used by the first-pass demo disc layout.
        /// </summary>
        const int CanvasHeight = 720;

        /// <summary>
        /// Width of the panel surface that contains the selectable menu items.
        /// </summary>
        const int PanelWidth = 560;

        /// <summary>
        /// Height of the panel surface that contains the selectable menu items.
        /// </summary>
        const int PanelHeight = 420;

        /// <summary>
        /// Width of one menu button row.
        /// </summary>
        const int ButtonWidth = 420;

        /// <summary>
        /// Height of one menu button row.
        /// </summary>
        const int ButtonHeight = 48;

        /// <summary>
        /// Vertical spacing inserted between adjacent menu buttons.
        /// </summary>
        const int ButtonSpacing = 14;

        /// <summary>
        /// Provider resolver used to instantiate the user-side menu-definition provider.
        /// </summary>
        MenuDefinitionProviderResolver ProviderResolverValue;

        /// <summary>
        /// Restorable theme palette active before the menu host applied its own palette.
        /// </summary>
        ThemeManager.ThemePalette PreviousTheme;

        /// <summary>
        /// Materialized definition returned by the resolved provider.
        /// </summary>
        MenuDefinition DefinitionValue;

        /// <summary>
        /// Runtime panels keyed by stable panel id.
        /// </summary>
        Dictionary<string, MenuHostPanelRuntime> PanelsById;

        /// <summary>
        /// Stack of previously active panel ids used by the Back action.
        /// </summary>
        List<string> PanelHistory;

        /// <summary>
        /// Runtime panel currently shown to the player.
        /// </summary>
        MenuHostPanelRuntime ActivePanel;

        /// <summary>
        /// Last raw gamepad state used for edge detection.
        /// </summary>
        InputGamepadState PreviousGamepadState;

        /// <summary>
        /// Font used by the large title near the top of the menu.
        /// </summary>
        FontAsset TitleFont;

        /// <summary>
        /// Font used by supporting copy and button labels.
        /// </summary>
        FontAsset BodyFont;

        /// <summary>
        /// Root canvas entity that owns the menu visuals.
        /// </summary>
        Entity CanvasEntity;

        /// <summary>
        /// Text component that renders the menu title.
        /// </summary>
        TextComponent TitleTextComponent;

        /// <summary>
        /// Text component that renders the global menu subtitle.
        /// </summary>
        TextComponent SubtitleTextComponent;

        /// <summary>
        /// Text component that renders the active panel heading.
        /// </summary>
        TextComponent PanelHeadingTextComponent;

        /// <summary>
        /// Text component that renders the active panel description.
        /// </summary>
        TextComponent PanelDescriptionTextComponent;

        /// <summary>
        /// Text component that renders the selected item description.
        /// </summary>
        TextComponent SelectedItemDescriptionTextComponent;

        /// <summary>
        /// Backing field for the currently active panel id.
        /// </summary>
        string ActivePanelIdValue;

        /// <summary>
        /// Backing field for the currently selected item id.
        /// </summary>
        string SelectedItemIdValue;

        /// <summary>
        /// Backing field for the provider type name persisted in scene data.
        /// </summary>
        string ProviderTypeNameValue;

        /// <summary>
        /// Initializes a new menu-host component with the default provider resolver.
        /// </summary>
        public MenuHostComponent() {
            ProviderResolver = new MenuDefinitionProviderResolver();
            PanelsById = new Dictionary<string, MenuHostPanelRuntime>(StringComparer.Ordinal);
            PanelHistory = new List<string>();
            ActivePanelIdValue = string.Empty;
            SelectedItemIdValue = string.Empty;
            ProviderTypeNameValue = string.Empty;
            UpdateOrder = 0;
        }

        /// <summary>
        /// Gets or sets the assembly-qualified menu-definition provider type name persisted in the scene.
        /// </summary>
        public string ProviderTypeName {
            get { return ProviderTypeNameValue; }
            set { ProviderTypeNameValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the resolver used to instantiate the user-side provider.
        /// </summary>
        public MenuDefinitionProviderResolver ProviderResolver {
            get { return ProviderResolverValue; }
            set { ProviderResolverValue = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets the active menu definition after the host has initialized.
        /// </summary>
        public MenuDefinition Definition {
            get { return DefinitionValue; }
        }

        /// <summary>
        /// Gets the stable id of the currently active panel.
        /// </summary>
        public string ActivePanelId {
            get { return ActivePanelIdValue; }
        }

        /// <summary>
        /// Gets the stable id of the currently selected item.
        /// </summary>
        public string SelectedItemId {
            get { return SelectedItemIdValue; }
        }

        /// <summary>
        /// Gets a value indicating whether the menu host has already materialized its runtime visuals.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Resolves the provider, validates the definition, applies the menu palette, and builds the runtime visuals.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (IsInitialized) {
                return;
            }

            if (entity.Children == null) {
                entity.InitChildren();
            }

            DefinitionValue = ProviderResolver.Resolve(ProviderTypeNameValue).CreateMenuDefinition();
            if (DefinitionValue == null) {
                throw new InvalidOperationException($"Menu provider '{ProviderTypeNameValue}' returned a null definition.");
            }

            ValidateDefinition(DefinitionValue);
            PreviousTheme = ThemeManager.Current;
            ThemeManager.SetTheme(BuildThemePalette(DefinitionValue));
            TitleFont = LoadFont(DefinitionValue.TitleFontPath);
            BodyFont = LoadFont(DefinitionValue.BodyFontPath);
            BuildCanvas();
            ActivatePanel(DefinitionValue.InitialPanelId, false);
            PreviousGamepadState = ReadPrimaryGamepadState();
            IsInitialized = true;
        }

        /// <summary>
        /// Restores the previously active theme when the menu host is removed.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            if (PreviousTheme != null) {
                ThemeManager.SetTheme(PreviousTheme);
            }

            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Routes keyboard and gamepad navigation to the active panel.
        /// </summary>
        public override void Update() {
            if (!IsInitialized) {
                return;
            }

            InputSystem inputSystem = ResolveInputSystem();
            HandleKeyboardInput(inputSystem);
            HandleGamepadInput(inputSystem);
        }

        /// <summary>
        /// Validates the menu definition before any runtime entities are created.
        /// </summary>
        /// <param name="definition">Definition to validate.</param>
        void ValidateDefinition(MenuDefinition definition) {
            Dictionary<string, MenuPanelDefinition> panels = new Dictionary<string, MenuPanelDefinition>(StringComparer.Ordinal);
            for (int panelIndex = 0; panelIndex < definition.Panels.Length; panelIndex++) {
                MenuPanelDefinition panelDefinition = definition.Panels[panelIndex];
                if (panels.ContainsKey(panelDefinition.PanelId)) {
                    throw new InvalidOperationException($"Menu panel id '{panelDefinition.PanelId}' is registered more than once.");
                }

                panels.Add(panelDefinition.PanelId, panelDefinition);
            }

            if (!panels.ContainsKey(definition.InitialPanelId)) {
                throw new InvalidOperationException($"Initial menu panel '{definition.InitialPanelId}' was not found.");
            }

            foreach (MenuPanelDefinition panelDefinition in definition.Panels) {
                int enabledItemCount = 0;
                for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                    MenuItemDefinition itemDefinition = panelDefinition.Items[itemIndex];
                    if (!itemDefinition.Enabled) {
                        continue;
                    }

                    enabledItemCount++;
                    ValidateAction(itemDefinition.Action, panels);
                }

                if (enabledItemCount == 0) {
                    throw new InvalidOperationException($"Menu panel '{panelDefinition.PanelId}' does not contain any enabled items.");
                }
            }
        }

        /// <summary>
        /// Validates one enabled menu action against the available panel graph.
        /// </summary>
        /// <param name="actionDefinition">Action to validate.</param>
        /// <param name="panels">Panel lookup used for target validation.</param>
        void ValidateAction(MenuActionDefinition actionDefinition, Dictionary<string, MenuPanelDefinition> panels) {
            if (actionDefinition == null) {
                throw new ArgumentNullException(nameof(actionDefinition));
            }

            switch (actionDefinition.Kind) {
                case MenuActionKind.None:
                    return;

                case MenuActionKind.OpenPanel:
                    if (!panels.ContainsKey(actionDefinition.TargetId)) {
                        throw new InvalidOperationException($"Menu action targets missing panel '{actionDefinition.TargetId}'.");
                    }
                    return;

                case MenuActionKind.LoadScene:
                    if (string.IsNullOrWhiteSpace(actionDefinition.TargetId)) {
                        throw new InvalidOperationException("Scene-loading menu actions must provide a packaged scene path.");
                    }
                    ValidatePackagedSceneExists(actionDefinition.TargetId);
                    return;

                case MenuActionKind.Back:
                    return;

                default:
                    throw new InvalidOperationException($"Unsupported menu action kind '{actionDefinition.Kind}'.");
            }
        }

        /// <summary>
        /// Creates the shared menu canvas and all materialized panel item entities.
        /// </summary>
        void BuildCanvas() {
            CanvasEntity = CreateChildEntity(Parent, new float3(0f, 0f, 0f));
            CreateBackgroundVisuals();
            TitleTextComponent = CreateTextEntity(
                CanvasEntity,
                new float3(96f, 56f, 0.1f),
                DefinitionValue.Title,
                TitleFont,
                DefinitionValue.TextColor,
                new int2(600, 64),
                40);
            SubtitleTextComponent = CreateTextEntity(
                CanvasEntity,
                new float3(100f, 118f, 0.1f),
                DefinitionValue.Subtitle,
                BodyFont,
                DefinitionValue.MutedTextColor,
                new int2(700, 36),
                41);
            PanelHeadingTextComponent = CreateTextEntity(
                CanvasEntity,
                new float3(120f, 220f, 0.1f),
                string.Empty,
                BodyFont,
                DefinitionValue.TextColor,
                new int2(420, 36),
                41);
            PanelDescriptionTextComponent = CreateTextEntity(
                CanvasEntity,
                new float3(120f, 258f, 0.1f),
                string.Empty,
                BodyFont,
                DefinitionValue.MutedTextColor,
                new int2(430, 52),
                41);
            SelectedItemDescriptionTextComponent = CreateTextEntity(
                CanvasEntity,
                new float3(120f, 600f, 0.1f),
                string.Empty,
                BodyFont,
                DefinitionValue.MutedTextColor,
                new int2(500, 64),
                41);

            PanelsById.Clear();
            for (int panelIndex = 0; panelIndex < DefinitionValue.Panels.Length; panelIndex++) {
                MenuHostPanelRuntime panelRuntime = BuildPanelRuntime(DefinitionValue.Panels[panelIndex]);
                PanelsById.Add(panelRuntime.Definition.PanelId, panelRuntime);
            }
        }

        /// <summary>
        /// Creates the decorative background and panel chrome for the first-pass menu layout.
        /// </summary>
        void CreateBackgroundVisuals() {
            Entity backgroundEntity = CreateChildEntity(CanvasEntity, new float3(0f, 0f, 0f));
            RoundedRectComponent background = new RoundedRectComponent {
                Size = new int2(CanvasWidth, CanvasHeight),
                Radius = 0f,
                BorderThickness = 0f,
                FillColor = DefinitionValue.BackgroundColor,
                BorderColor = DefinitionValue.BackgroundColor,
                RenderOrder2D = 10,
                LayerMask = (byte)Parent.LayerMask
            };
            backgroundEntity.AddComponent(background);

            Entity accentEntity = CreateChildEntity(CanvasEntity, new float3(72f, 64f, 0f));
            RoundedRectComponent accent = new RoundedRectComponent {
                Size = new int2(18, 520),
                Radius = 9f,
                BorderThickness = 0f,
                FillColor = DefinitionValue.AccentSecondaryColor,
                BorderColor = DefinitionValue.AccentSecondaryColor,
                RenderOrder2D = 20,
                LayerMask = (byte)Parent.LayerMask
            };
            accentEntity.AddComponent(accent);

            Entity panelEntity = CreateChildEntity(CanvasEntity, new float3(88f, 190f, 0f));
            RoundedRectComponent panel = new RoundedRectComponent {
                Size = new int2(PanelWidth, PanelHeight),
                Radius = 18f,
                BorderThickness = 3f,
                FillColor = DefinitionValue.SurfaceColor,
                BorderColor = DefinitionValue.SurfaceBorderColor,
                RenderOrder2D = 30,
                LayerMask = (byte)Parent.LayerMask
            };
            panelEntity.AddComponent(panel);

            Entity panelAccentEntity = CreateChildEntity(CanvasEntity, new float3(88f, 190f, 0f));
            RoundedRectComponent panelAccent = new RoundedRectComponent {
                Size = new int2(PanelWidth, 18),
                Radius = 9f,
                BorderThickness = 0f,
                FillColor = DefinitionValue.AccentColor,
                BorderColor = DefinitionValue.AccentColor,
                RenderOrder2D = 31,
                LayerMask = (byte)Parent.LayerMask
            };
            panelAccentEntity.AddComponent(panelAccent);
        }

        /// <summary>
        /// Builds one runtime panel and its enabled interactive items.
        /// </summary>
        /// <param name="panelDefinition">Panel definition to materialize.</param>
        /// <returns>Runtime panel record.</returns>
        MenuHostPanelRuntime BuildPanelRuntime(MenuPanelDefinition panelDefinition) {
            Entity panelRoot = CreateChildEntity(CanvasEntity, new float3(120f, 320f, 0f));
            panelRoot.Enabled = false;

            int enabledItemCount = CountEnabledItems(panelDefinition.Items);
            MenuHostItemRuntime[] items = new MenuHostItemRuntime[enabledItemCount];
            MenuHostPanelRuntime panelRuntime = new MenuHostPanelRuntime(panelDefinition, panelRoot, items);
            int itemInsertIndex = 0;
            for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                MenuItemDefinition itemDefinition = panelDefinition.Items[itemIndex];
                if (!itemDefinition.Enabled) {
                    continue;
                }

                Entity buttonEntity = CreateChildEntity(panelRoot, new float3(0f, 0f, 0f));
                MenuHostItemRuntime runtimeItem = null;
                ButtonComponent button = new ButtonComponent(
                    itemDefinition.Label,
                    new int2(ButtonWidth, ButtonHeight),
                    BodyFont,
                    () => ActivateItem(runtimeItem));
                button.SetRenderOrders(33, 34);
                button.SetTextColor(DefinitionValue.TextColor);
                runtimeItem = new MenuHostItemRuntime(panelRuntime, itemDefinition, itemInsertIndex, buttonEntity, button);
                button.Hovered += () => HandleItemHovered(runtimeItem);
                buttonEntity.AddComponent(button);
                items[itemInsertIndex] = runtimeItem;
                itemInsertIndex++;
            }

            panelRuntime.SelectedItemIndex = 0;
            ApplyPanelLayout(panelRuntime);
            return panelRuntime;
        }

        /// <summary>
        /// Applies runtime positions and visibility for all enabled items in one panel.
        /// </summary>
        /// <param name="panelRuntime">Panel whose item layout should be refreshed.</param>
        void ApplyPanelLayout(MenuHostPanelRuntime panelRuntime) {
            if (panelRuntime == null) {
                throw new ArgumentNullException(nameof(panelRuntime));
            }

            EnsureSelectedItemVisible(panelRuntime);
            int visibleItemCount = panelRuntime.Definition.VisibleItemCount;
            for (int itemIndex = 0; itemIndex < panelRuntime.Items.Length; itemIndex++) {
                MenuHostItemRuntime runtimeItem = panelRuntime.Items[itemIndex];
                bool isVisible = itemIndex >= panelRuntime.ScrollOffset && itemIndex < panelRuntime.ScrollOffset + visibleItemCount;
                runtimeItem.Entity.Enabled = isVisible;
                if (!isVisible) {
                    continue;
                }

                int visibleIndex = itemIndex - panelRuntime.ScrollOffset;
                runtimeItem.Entity.LocalPosition = new float3(0f, visibleIndex * (ButtonHeight + ButtonSpacing), 0f);
            }
        }

        /// <summary>
        /// Updates scroll offset so the selected item remains inside the visible row window.
        /// </summary>
        /// <param name="panelRuntime">Panel whose scroll offset should be adjusted.</param>
        void EnsureSelectedItemVisible(MenuHostPanelRuntime panelRuntime) {
            if (panelRuntime.SelectedItemIndex < panelRuntime.ScrollOffset) {
                panelRuntime.ScrollOffset = panelRuntime.SelectedItemIndex;
            } else if (panelRuntime.SelectedItemIndex >= panelRuntime.ScrollOffset + panelRuntime.Definition.VisibleItemCount) {
                panelRuntime.ScrollOffset = panelRuntime.SelectedItemIndex - panelRuntime.Definition.VisibleItemCount + 1;
            }
        }

        /// <summary>
        /// Activates one panel and refreshes all shared text and focus state.
        /// </summary>
        /// <param name="panelId">Panel id to activate.</param>
        /// <param name="pushHistory">True when the current panel should be pushed onto the back stack.</param>
        void ActivatePanel(string panelId, bool pushHistory) {
            if (!PanelsById.TryGetValue(panelId, out MenuHostPanelRuntime nextPanel)) {
                throw new InvalidOperationException($"Menu panel '{panelId}' was not registered.");
            }

            if (ActivePanel != null) {
                if (pushHistory && !string.Equals(ActivePanel.Definition.PanelId, panelId, StringComparison.Ordinal)) {
                    PanelHistory.Add(ActivePanel.Definition.PanelId);
                }

                ActivePanel.RootEntity.Enabled = false;
                ClearPanelFocus(ActivePanel);
            }

            ActivePanel = nextPanel;
            ActivePanel.RootEntity.Enabled = true;
            ActivePanelIdValue = ActivePanel.Definition.PanelId;
            PanelHeadingTextComponent.Text = ActivePanel.Definition.Heading;
            PanelDescriptionTextComponent.Text = ActivePanel.Definition.Description;
            SetSelection(ActivePanel, ActivePanel.SelectedItemIndex < 0 ? 0 : ActivePanel.SelectedItemIndex);
        }

        /// <summary>
        /// Moves selection through the current panel by the supplied signed delta.
        /// </summary>
        /// <param name="delta">Signed movement amount.</param>
        void MoveSelection(int delta) {
            if (ActivePanel == null || ActivePanel.Items.Length == 0) {
                return;
            }
            if (delta == 0) {
                return;
            }

            int itemCount = ActivePanel.Items.Length;
            int nextIndex = ActivePanel.SelectedItemIndex;
            if (nextIndex < 0) {
                nextIndex = 0;
            }

            for (int step = 0; step < itemCount; step++) {
                nextIndex += delta;
                if (nextIndex < 0) {
                    nextIndex = itemCount - 1;
                } else if (nextIndex >= itemCount) {
                    nextIndex = 0;
                }

                SetSelection(ActivePanel, nextIndex);
                return;
            }
        }

        /// <summary>
        /// Applies focus visuals and supporting copy for the selected item.
        /// </summary>
        /// <param name="panelRuntime">Panel that owns the selected item.</param>
        /// <param name="itemIndex">Enabled-item index to select.</param>
        void SetSelection(MenuHostPanelRuntime panelRuntime, int itemIndex) {
            if (panelRuntime == null) {
                throw new ArgumentNullException(nameof(panelRuntime));
            }
            if (itemIndex < 0 || itemIndex >= panelRuntime.Items.Length) {
                throw new ArgumentOutOfRangeException(nameof(itemIndex), "Selected menu item index must resolve to an enabled item.");
            }

            panelRuntime.SelectedItemIndex = itemIndex;
            for (int index = 0; index < panelRuntime.Items.Length; index++) {
                panelRuntime.Items[index].Button.SetTargetFocused(index == itemIndex);
            }

            SelectedItemIdValue = panelRuntime.Items[itemIndex].Definition.ItemId;
            SelectedItemDescriptionTextComponent.Text = panelRuntime.Items[itemIndex].Definition.Description;
            ApplyPanelLayout(panelRuntime);
        }

        /// <summary>
        /// Clears button focus visuals for one panel before a different panel becomes active.
        /// </summary>
        /// <param name="panelRuntime">Panel whose button focus visuals should be cleared.</param>
        void ClearPanelFocus(MenuHostPanelRuntime panelRuntime) {
            for (int itemIndex = 0; itemIndex < panelRuntime.Items.Length; itemIndex++) {
                panelRuntime.Items[itemIndex].Button.SetTargetFocused(false);
            }
        }

        /// <summary>
        /// Handles pointer hover notifications from button components.
        /// </summary>
        /// <param name="runtimeItem">Runtime item that received pointer hover.</param>
        void HandleItemHovered(MenuHostItemRuntime runtimeItem) {
            if (runtimeItem == null) {
                throw new ArgumentNullException(nameof(runtimeItem));
            }
            if (!ReferenceEquals(ActivePanel, runtimeItem.Panel)) {
                return;
            }

            SetSelection(runtimeItem.Panel, runtimeItem.Index);
        }

        /// <summary>
        /// Executes the action associated with one activated runtime item.
        /// </summary>
        /// <param name="runtimeItem">Activated runtime item.</param>
        void ActivateItem(MenuHostItemRuntime runtimeItem) {
            if (runtimeItem == null) {
                throw new ArgumentNullException(nameof(runtimeItem));
            }

            SetSelection(runtimeItem.Panel, runtimeItem.Index);
            ExecuteAction(runtimeItem.Definition.Action);
        }

        /// <summary>
        /// Executes one validated menu action.
        /// </summary>
        /// <param name="actionDefinition">Action to execute.</param>
        void ExecuteAction(MenuActionDefinition actionDefinition) {
            switch (actionDefinition.Kind) {
                case MenuActionKind.None:
                    return;

                case MenuActionKind.OpenPanel:
                    ActivatePanel(actionDefinition.TargetId, true);
                    return;

                case MenuActionKind.LoadScene:
                    LoadScene(actionDefinition.TargetId);
                    return;

                case MenuActionKind.Back:
                    NavigateBack();
                    return;

                default:
                    throw new InvalidOperationException($"Unsupported menu action kind '{actionDefinition.Kind}'.");
            }
        }

        /// <summary>
        /// Returns to the previous panel recorded in the host history stack.
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
        /// Loads one packaged scene and disables the menu host entity.
        /// </summary>
        /// <param name="scenePath">Project-relative packaged scene path.</param>
        void LoadScene(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new InvalidOperationException("Scene-loading menu actions must provide a packaged scene path.");
            }
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before loading a scene from the menu host.");
            }
            if (Core.Instance.SceneLoadService == null) {
                throw new InvalidOperationException("Core scene loading services must be initialized before loading a scene from the menu host.");
            }

            string resolvedScenePath = ResolveSceneContentPath(scenePath);
            SceneAsset sceneAsset = Core.Instance.ContentManager.Load<SceneAsset>(resolvedScenePath, RuntimeContentProcessorIds.SceneAsset);
            Core.Instance.SceneLoadService.Load(sceneAsset);
            Parent.Enabled = false;
        }

        /// <summary>
        /// Handles keyboard navigation and activation for the active panel.
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
        /// Handles d-pad and face-button navigation for the primary gamepad.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        void HandleGamepadInput(InputSystem inputSystem) {
            InputGamepadState currentGamepadState = inputSystem.GetGamepadState(0);
            if (!currentGamepadState.Connected) {
                PreviousGamepadState = currentGamepadState;
                return;
            }

            if (WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.DPadUp)) {
                MoveSelection(-1);
            } else if (WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.DPadDown)) {
                MoveSelection(1);
            } else if (WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.South)) {
                ConfirmSelection(Keys.Enter);
            } else if (WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.East)
                || WasGamepadButtonPressed(currentGamepadState, PreviousGamepadState, InputGamepadButton.Select)) {
                NavigateBack();
            }

            PreviousGamepadState = currentGamepadState;
        }

        /// <summary>
        /// Activates the currently selected item through the shared button activation path.
        /// </summary>
        /// <param name="key">Logical activation key routed to the selected button.</param>
        void ConfirmSelection(Keys key) {
            if (ActivePanel == null || ActivePanel.SelectedItemIndex < 0) {
                return;
            }

            ActivePanel.Items[ActivePanel.SelectedItemIndex].Button.ActivateFromKey(key);
        }

        /// <summary>
        /// Returns whether the supplied gamepad button transitioned from up to down on the current frame.
        /// </summary>
        /// <param name="currentState">Current raw gamepad state.</param>
        /// <param name="previousState">Previous raw gamepad state.</param>
        /// <param name="button">Button to test.</param>
        /// <returns>True when the button was pressed this frame.</returns>
        bool WasGamepadButtonPressed(InputGamepadState currentState, InputGamepadState previousState, InputGamepadButton button) {
            return currentState.IsButtonDown(button) && !previousState.IsButtonDown(button);
        }

        /// <summary>
        /// Counts the number of enabled menu items inside one panel definition.
        /// </summary>
        /// <param name="items">Items to inspect.</param>
        /// <returns>Number of enabled items.</returns>
        int CountEnabledItems(MenuItemDefinition[] items) {
            int enabledItemCount = 0;
            for (int itemIndex = 0; itemIndex < items.Length; itemIndex++) {
                if (items[itemIndex].Enabled) {
                    enabledItemCount++;
                }
            }

            return enabledItemCount;
        }

        /// <summary>
        /// Loads one packaged font asset using the shared runtime content manager.
        /// </summary>
        /// <param name="relativePath">Project-relative path to the packaged font asset.</param>
        /// <returns>Loaded font asset.</returns>
        FontAsset LoadFont(string relativePath) {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before loading menu fonts.");
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new InvalidOperationException("Menu font paths must be provided before the menu host initializes.");
            }

            return Core.Instance.ContentManager.Load<FontAsset>(relativePath, RuntimeContentProcessorIds.FontAsset);
        }

        /// <summary>
        /// Resolves one authored scene id into the content-relative path available in the current runtime layout.
        /// </summary>
        /// <param name="scenePath">Authored or packaged scene path requested by the menu definition.</param>
        /// <returns>Content-relative path that exists beneath the current content root.</returns>
        string ResolveSceneContentPath(string scenePath) {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before resolving menu scene paths.");
            }
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            }

            string normalizedScenePath = NormalizeRelativeContentPath(scenePath);
            string contentRootPath = Core.Instance.InitializationOptions.ContentRootPath;
            if (DoesContentFileExist(contentRootPath, normalizedScenePath)) {
                return normalizedScenePath;
            }

            string packagedScenePath = BuildPackagedSceneContentPath(normalizedScenePath);
            if (DoesContentFileExist(contentRootPath, packagedScenePath)) {
                return packagedScenePath;
            }

            throw new InvalidOperationException(
                $"Menu scene '{scenePath}' could not be found in authored form '{normalizedScenePath}' or packaged form '{packagedScenePath}'.");
        }

        /// <summary>
        /// Builds the packaged content-relative path used by player builds for one authored scene id.
        /// </summary>
        /// <param name="scenePath">Normalized authored scene id.</param>
        /// <returns>Packaged content-relative scene path.</returns>
        string BuildPackagedSceneContentPath(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            }

            if (scenePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase)) {
                return scenePath;
            }
            if (scenePath.StartsWith("cooked/", StringComparison.OrdinalIgnoreCase)) {
                return scenePath;
            }

            string changedExtensionPath = Path.ChangeExtension(scenePath, ".hasset");
            return NormalizeRelativeContentPath(Path.Combine("scenes", changedExtensionPath));
        }

        /// <summary>
        /// Returns whether the supplied content-relative path exists beneath the current content root.
        /// </summary>
        /// <param name="contentRootPath">Absolute content root path.</param>
        /// <param name="relativePath">Content-relative path to inspect.</param>
        /// <returns>True when the content file exists.</returns>
        bool DoesContentFileExist(string contentRootPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(contentRootPath)) {
                throw new ArgumentException("Content root path must be provided.", nameof(contentRootPath));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(contentRootPath, normalizedRelativePath));
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Normalizes one content-relative path to the forward-slash form used by runtime asset ids.
        /// </summary>
        /// <param name="relativePath">Relative content path to normalize.</param>
        /// <returns>Normalized content-relative path.</returns>
        string NormalizeRelativeContentPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Verifies that one packaged scene path can be resolved by the active content manager.
        /// </summary>
        /// <param name="scenePath">Project-relative packaged scene path to validate.</param>
        void ValidatePackagedSceneExists(string scenePath) {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before validating packaged scene menu actions.");
            }

            string resolvedScenePath = ResolveSceneContentPath(scenePath);
            Core.Instance.ContentManager.Load<SceneAsset>(resolvedScenePath, RuntimeContentProcessorIds.SceneAsset);
        }

        /// <summary>
        /// Creates the theme palette applied while the menu host is active.
        /// </summary>
        /// <param name="definition">Menu definition supplying theme colors.</param>
        /// <returns>Theme palette built from the menu definition.</returns>
        ThemeManager.ThemePalette BuildThemePalette(MenuDefinition definition) {
            return new ThemeManager.ThemePalette(new ThemeManager.ThemeColors {
                BackgroundPrimary = definition.BackgroundColor,
                SurfacePrimary = definition.SurfaceColor,
                SurfaceInput = definition.SurfaceColor,
                AccentPrimary = definition.AccentColor,
                AccentSecondary = definition.AccentSecondaryColor,
                AccentTertiary = definition.SurfaceBorderColor,
                AccentQuaternary = definition.MutedTextColor,
                StateDanger = new byte4(255, 112, 142, 255),
                StateWarning = new byte4(255, 196, 118, 255),
                StateSuccess = new byte4(124, 241, 176, 255),
                InputForegroundPrimary = definition.TextColor,
                InputForegroundSecondary = definition.MutedTextColor,
                TextPrimary = definition.TextColor,
                TextSecondary = definition.MutedTextColor,
                TextOnAccent = definition.TextColor
            });
        }

        /// <summary>
        /// Creates one child entity at a fixed local position.
        /// </summary>
        /// <param name="parent">Parent entity that will own the child.</param>
        /// <param name="localPosition">Local position assigned to the child.</param>
        /// <returns>New child entity attached to the supplied parent.</returns>
        Entity CreateChildEntity(Entity parent, float3 localPosition) {
            if (parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }

            Entity child = new Entity();
            child.LayerMask = parent.LayerMask;
            child.LocalPosition = localPosition;
            child.InitComponents();
            child.InitChildren();
            parent.AddChild(child);
            return child;
        }

        /// <summary>
        /// Creates one text component hosted on a fresh child entity.
        /// </summary>
        /// <param name="parent">Parent entity that will own the text entity.</param>
        /// <param name="localPosition">Local position assigned to the text entity.</param>
        /// <param name="text">Visible text content.</param>
        /// <param name="font">Font used to render the text.</param>
        /// <param name="color">Color applied to the rendered text.</param>
        /// <param name="size">Layout size used by the text renderer.</param>
        /// <param name="renderOrder">2D render order assigned to the text.</param>
        /// <returns>Created text component.</returns>
        TextComponent CreateTextEntity(
            Entity parent,
            float3 localPosition,
            string text,
            FontAsset font,
            byte4 color,
            int2 size,
            byte renderOrder) {
            Entity textEntity = CreateChildEntity(parent, localPosition);
            TextComponent textComponent = new TextComponent {
                Text = text ?? string.Empty,
                Font = font,
                Color = color,
                Size = size,
                WrapText = true,
                RenderOrder2D = renderOrder,
                LayerMask = (byte)Parent.LayerMask,
                SelectionEnabled = false
            };
            textEntity.AddComponent(textComponent);
            return textComponent;
        }

        /// <summary>
        /// Resolves the active core input system.
        /// </summary>
        /// <returns>Current input system.</returns>
        InputSystem ResolveInputSystem() {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before the menu host can read input.");
            }

            return Core.Instance.InputSystem;
        }

        /// <summary>
        /// Reads the primary gamepad state from the active input system.
        /// </summary>
        /// <returns>Current primary gamepad state.</returns>
        InputGamepadState ReadPrimaryGamepadState() {
            return ResolveInputSystem().GetGamepadState(0);
        }
    }
}
