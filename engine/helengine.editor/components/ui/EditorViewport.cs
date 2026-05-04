namespace helengine.editor {
    /// <summary>
    /// Dockable editor window that hosts a camera viewport and per-viewport gizmo tool buttons.
    /// </summary>
    public class EditorViewport : DockableEntity {
        /// <summary>
        /// Horizontal padding from the toolbar edge to the first tool button.
        /// </summary>
        const int ToolbarPadding = 6;
        /// <summary>
        /// Height of the in-viewport tool toolbar.
        /// </summary>
        const int ToolbarHeight = 24;
        /// <summary>
        /// Spacing between tool buttons.
        /// </summary>
        const int ToolbarButtonSpacing = 4;
        /// <summary>
        /// Width of each tool button.
        /// </summary>
        const int ToolButtonWidth = 22;
        /// <summary>
        /// Height of each tool button.
        /// </summary>
        const int ToolButtonHeight = 18;
        /// <summary>
        /// Square size used by each tool icon sprite inside its button.
        /// </summary>
        const int ToolButtonIconSize = 14;
        /// <summary>
        /// Width used by the snap adjustment icon sprites.
        /// </summary>
        const int SnapButtonIconWidth = 16;
        /// <summary>
        /// Height used by the snap adjustment icon sprites.
        /// </summary>
        const int SnapButtonIconHeight = 16;
        /// <summary>
        /// Height used by snap label icons.
        /// </summary>
        const int SnapLabelIconHeight = 18;
        /// <summary>
        /// Horizontal gap between the magnet icon and modifier keycap inside one snap label.
        /// </summary>
        const int SnapLabelIconSpacing = 4;
        /// <summary>
        /// Horizontal gap inserted between the tool buttons and snap controls.
        /// </summary>
        const int SnapGroupSpacing = 12;
        /// <summary>
        /// Horizontal gap between a snap label and its value box.
        /// </summary>
        const int SnapLabelSpacing = 6;
        /// <summary>
        /// Width of the snap value display box.
        /// </summary>
        const int SnapValueWidth = 52;

        /// <summary>
        /// Render order used for toolbar surfaces.
        /// </summary>
        readonly byte ToolbarSurfaceOrder;
        /// <summary>
        /// Render order used for toolbar foreground content such as icons and text.
        /// </summary>
        readonly byte ToolbarForegroundOrder;
        /// <summary>
        /// Owner key used to register the toolbar input blocker.
        /// </summary>
        readonly object ToolbarInputBlockerOwner;
        /// <summary>
        /// Font used by toolbar text content such as value readouts.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Font used by the snap modifier labels so CTRL and SHIFT remain readable in the toolbar.
        /// </summary>
        readonly FontAsset SnapModifierFont;
        /// <summary>
        /// Runtime textures used by the toolbar controls on this viewport.
        /// </summary>
        readonly EditorViewportToolbarIconSet ToolbarIcons;
        /// <summary>
        /// Root entity hosting the toolbar visuals and interactables.
        /// </summary>
        readonly EditorEntity ToolbarRoot;
        /// <summary>
        /// Background sprite for the toolbar chrome.
        /// </summary>
        readonly SpriteComponent ToolbarBackground;
        /// <summary>
        /// Nested focus group that represents the viewport content region.
        /// </summary>
        readonly EditorFocusGroup ContentFocusGroup;
        /// <summary>
        /// Nested focus group that represents the viewport toolbar region.
        /// </summary>
        readonly EditorFocusGroup ToolbarFocusGroup;
        /// <summary>
        /// Focus target that owns viewport-local gizmo shortcut activation.
        /// </summary>
        readonly EditorFocusTarget ViewportContentFocusTarget;
        /// <summary>
        /// Supported tool modes displayed by this viewport toolbar.
        /// </summary>
        readonly EditorViewportToolMode[] ToolModes;
        /// <summary>
        /// Root entities for tool buttons, indexed to <see cref="ToolModes"/>.
        /// </summary>
        readonly EditorEntity[] ToolButtonRoots;
        /// <summary>
        /// Background sprites for tool buttons, indexed to <see cref="ToolModes"/>.
        /// </summary>
        readonly SpriteComponent[] ToolButtonBackgrounds;
        /// <summary>
        /// Icon sprites for tool buttons, indexed to <see cref="ToolModes"/>.
        /// </summary>
        readonly SpriteComponent[] ToolButtonIcons;
        /// <summary>
        /// Interactable regions for tool buttons, indexed to <see cref="ToolModes"/>.
        /// </summary>
        readonly InteractableComponent[] ToolButtonInteractables;
        /// <summary>
        /// Tracks hover state per tool button.
        /// </summary>
        readonly bool[] ToolButtonHoverStates;
        /// <summary>
        /// Tracks pressed state per tool button.
        /// </summary>
        readonly bool[] ToolButtonPressedStates;
        /// <summary>
        /// Focus targets bound to the toolbar tool buttons.
        /// </summary>
        readonly EditorFocusTarget[] ToolButtonFocusTargets;
        /// <summary>
        /// Tracks keyboard-focus state per tool button.
        /// </summary>
        readonly bool[] ToolButtonKeyboardFocusStates;
        /// <summary>
        /// Root entity for the viewport grid toggle button.
        /// </summary>
        EditorEntity GridButtonRoot;
        /// <summary>
        /// Background sprite for the viewport grid toggle button.
        /// </summary>
        SpriteComponent GridButtonBackground;
        /// <summary>
        /// Icon sprite for the viewport grid toggle button.
        /// </summary>
        SpriteComponent GridButtonIcon;
        /// <summary>
        /// Interactable region for the viewport grid toggle button.
        /// </summary>
        InteractableComponent GridButtonInteractable;
        /// <summary>
        /// Focus target bound to the viewport grid toggle button.
        /// </summary>
        EditorFocusTarget GridButtonFocusTarget;
        /// <summary>
        /// Tracks hover state for the viewport grid toggle button.
        /// </summary>
        bool GridButtonHoverState;
        /// <summary>
        /// Tracks pressed state for the viewport grid toggle button.
        /// </summary>
        bool GridButtonPressedState;
        /// <summary>
        /// Tracks keyboard-focus state for the viewport grid toggle button.
        /// </summary>
        bool GridButtonKeyboardFocusState;
        /// <summary>
        /// Snap slots shown by the toolbar.
        /// </summary>
        readonly TransformGizmoSnapSlot[] SnapSlots;
        /// <summary>
        /// Label entities for snap groups.
        /// </summary>
        readonly EditorEntity[] SnapLabelRoots;
        /// <summary>
        /// Magnet icons used for snap-slot labels.
        /// </summary>
        readonly SpriteComponent[] SnapLabelMagnetIcons;
        /// <summary>
        /// Modifier key text labels used for snap-slot labels.
        /// </summary>
        readonly TextComponent[] SnapLabelModifierTexts;
        /// <summary>
        /// Root entities for snap value boxes.
        /// </summary>
        readonly EditorEntity[] SnapValueRoots;
        /// <summary>
        /// Background sprites for snap value boxes.
        /// </summary>
        readonly SpriteComponent[] SnapValueBackgrounds;
        /// <summary>
        /// Text components used for snap value readouts.
        /// </summary>
        readonly TextComponent[] SnapValueTexts;
        /// <summary>
        /// Root entities for snap-increase buttons.
        /// </summary>
        readonly EditorEntity[] SnapIncreaseButtonRoots;
        /// <summary>
        /// Background sprites for snap-increase buttons.
        /// </summary>
        readonly SpriteComponent[] SnapIncreaseButtonBackgrounds;
        /// <summary>
        /// Icon sprites for snap-increase buttons.
        /// </summary>
        readonly SpriteComponent[] SnapIncreaseButtonIcons;
        /// <summary>
        /// Interactable regions for snap-increase buttons.
        /// </summary>
        readonly InteractableComponent[] SnapIncreaseButtonInteractables;
        /// <summary>
        /// Hover state tracked per snap-increase button.
        /// </summary>
        readonly bool[] SnapIncreaseButtonHoverStates;
        /// <summary>
        /// Pressed state tracked per snap-increase button.
        /// </summary>
        readonly bool[] SnapIncreaseButtonPressedStates;
        /// <summary>
        /// Focus targets bound to the snap-increase buttons.
        /// </summary>
        readonly EditorFocusTarget[] SnapIncreaseFocusTargets;
        /// <summary>
        /// Tracks keyboard-focus state per snap-increase button.
        /// </summary>
        readonly bool[] SnapIncreaseKeyboardFocusStates;
        /// <summary>
        /// Root entities for snap-decrease buttons.
        /// </summary>
        readonly EditorEntity[] SnapDecreaseButtonRoots;
        /// <summary>
        /// Background sprites for snap-decrease buttons.
        /// </summary>
        readonly SpriteComponent[] SnapDecreaseButtonBackgrounds;
        /// <summary>
        /// Icon sprites for snap-decrease buttons.
        /// </summary>
        readonly SpriteComponent[] SnapDecreaseButtonIcons;
        /// <summary>
        /// Interactable regions for snap-decrease buttons.
        /// </summary>
        readonly InteractableComponent[] SnapDecreaseButtonInteractables;
        /// <summary>
        /// Hover state tracked per snap-decrease button.
        /// </summary>
        readonly bool[] SnapDecreaseButtonHoverStates;
        /// <summary>
        /// Pressed state tracked per snap-decrease button.
        /// </summary>
        readonly bool[] SnapDecreaseButtonPressedStates;
        /// <summary>
        /// Focus targets bound to the snap-decrease buttons.
        /// </summary>
        readonly EditorFocusTarget[] SnapDecreaseFocusTargets;
        /// <summary>
        /// Tracks keyboard-focus state per snap-decrease button.
        /// </summary>
        readonly bool[] SnapDecreaseKeyboardFocusStates;
        /// <summary>
        /// Tracks whether the viewport content target is currently keyboard-focused.
        /// </summary>
        bool IsViewportContentFocused;

        /// <summary>
        /// Initializes a new dockable viewport and binds it to the provided camera.
        /// </summary>
        /// <param name="camera">Camera rendering into the viewport.</param>
        /// <param name="font">Font used by the base dockable entity title bar.</param>
        /// <param name="snapModifierFont">Font used by the snap modifier labels.</param>
        /// <param name="toolbarIcons">Runtime toolbar icon textures used by the transform and snap buttons.</param>
        public EditorViewport(CameraComponent camera, FontAsset font, FontAsset snapModifierFont, EditorViewportToolbarIconSet toolbarIcons)
            : base(font) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            SnapModifierFont = snapModifierFont ?? throw new ArgumentNullException(nameof(snapModifierFont));
            ToolbarIcons = toolbarIcons ?? throw new ArgumentNullException(nameof(toolbarIcons));
            Title = "Viewport";
            SetContentBackgroundColor(new byte4(0, 0, 0, 0));

            ToolbarSurfaceOrder = RenderOrder2D.PanelSurface;
            ToolbarForegroundOrder = RenderOrder2D.PanelForeground;
            ToolbarInputBlockerOwner = new object();
            ContentFocusGroup = new EditorFocusGroup(this, 0, () => Enabled, ContainsViewportContentPoint, HandleSubviewGroupActiveChanged);
            ToolbarFocusGroup = new EditorFocusGroup(this, 1, () => Enabled, ContainsToolbarPoint, HandleSubviewGroupActiveChanged);
            EditorKeyboardFocusService.RegisterGroup(ContentFocusGroup);
            EditorKeyboardFocusService.RegisterGroup(ToolbarFocusGroup);
            ViewportContentFocusTarget = new EditorFocusTarget(
                ContentFocusGroup,
                0,
                true,
                () => Enabled,
                ContainsViewportContentPoint,
                HandleViewportContentFocusedChanged,
                CanActivateViewportContentKey,
                ActivateViewportContentKey);
            EditorKeyboardFocusService.RegisterTarget(ViewportContentFocusTarget);
            ToolModes = new[] {
                EditorViewportToolMode.Translate,
                EditorViewportToolMode.Rotate,
                EditorViewportToolMode.Scale
            };
            ToolButtonRoots = new EditorEntity[ToolModes.Length];
            ToolButtonBackgrounds = new SpriteComponent[ToolModes.Length];
            ToolButtonIcons = new SpriteComponent[ToolModes.Length];
            ToolButtonInteractables = new InteractableComponent[ToolModes.Length];
            ToolButtonHoverStates = new bool[ToolModes.Length];
            ToolButtonPressedStates = new bool[ToolModes.Length];
            ToolButtonFocusTargets = new EditorFocusTarget[ToolModes.Length];
            ToolButtonKeyboardFocusStates = new bool[ToolModes.Length];
            SnapSlots = new[] {
                TransformGizmoSnapSlot.Snap1,
                TransformGizmoSnapSlot.Snap2
            };
            SnapLabelRoots = new EditorEntity[SnapSlots.Length];
            SnapLabelMagnetIcons = new SpriteComponent[SnapSlots.Length];
            SnapLabelModifierTexts = new TextComponent[SnapSlots.Length];
            SnapValueRoots = new EditorEntity[SnapSlots.Length];
            SnapValueBackgrounds = new SpriteComponent[SnapSlots.Length];
            SnapValueTexts = new TextComponent[SnapSlots.Length];
            SnapIncreaseButtonRoots = new EditorEntity[SnapSlots.Length];
            SnapIncreaseButtonBackgrounds = new SpriteComponent[SnapSlots.Length];
            SnapIncreaseButtonIcons = new SpriteComponent[SnapSlots.Length];
            SnapIncreaseButtonInteractables = new InteractableComponent[SnapSlots.Length];
            SnapIncreaseButtonHoverStates = new bool[SnapSlots.Length];
            SnapIncreaseButtonPressedStates = new bool[SnapSlots.Length];
            SnapIncreaseFocusTargets = new EditorFocusTarget[SnapSlots.Length];
            SnapIncreaseKeyboardFocusStates = new bool[SnapSlots.Length];
            SnapDecreaseButtonRoots = new EditorEntity[SnapSlots.Length];
            SnapDecreaseButtonBackgrounds = new SpriteComponent[SnapSlots.Length];
            SnapDecreaseButtonIcons = new SpriteComponent[SnapSlots.Length];
            SnapDecreaseButtonInteractables = new InteractableComponent[SnapSlots.Length];
            SnapDecreaseButtonHoverStates = new bool[SnapSlots.Length];
            SnapDecreaseButtonPressedStates = new bool[SnapSlots.Length];
            SnapDecreaseFocusTargets = new EditorFocusTarget[SnapSlots.Length];
            SnapDecreaseKeyboardFocusStates = new bool[SnapSlots.Length];

            ToolbarRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.2f)
            };
            AddChild(ToolbarRoot);

            ToolbarBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = ToolbarSurfaceOrder
            };
            ToolbarRoot.AddComponent(ToolbarBackground);

            InitializeToolButtons();
            InitializeGridButton();
            InitializeSnapControls();
            AddComponent(new EditorViewportCameraAngleOverlayComponent(Camera, Font, ToolbarHeight, false));
            ToolMode = EditorViewportToolService.GetToolMode(Camera);
            RefreshRenderOrderBias();
            UpdateViewport();
        }

        /// <summary>
        /// Gets the camera used to render into this viewport.
        /// </summary>
        public CameraComponent Camera { get; private set; }
        /// <summary>
        /// Gets or sets the active gizmo tool mode for this viewport.
        /// </summary>
        public EditorViewportToolMode ToolMode {
            get => EditorViewportToolService.GetToolMode(Camera);
            set => SetToolMode(value);
        }

        /// <summary>
        /// Gets or sets the viewport position, updating the underlying camera viewport rectangle.
        /// </summary>
        public override float3 Position {
            get => base.Position;
            set {
                base.Position = value;
                UpdateViewport();
            }
        }

        /// <summary>
        /// Re-applies viewport layout when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            UpdateViewport();
        }

        /// <summary>
        /// Clears input blockers owned by this viewport toolbar.
        /// </summary>
        public void ClearInputBlockers() {
            if (ToolbarInputBlockerOwner == null) {
                return;
            }

            EditorInputCaptureService.ClearBlocker(ToolbarInputBlockerOwner);
        }

        /// <summary>
        /// Refreshes input blockers owned by this viewport.
        /// </summary>
        public void RefreshInputBlockers() {
            if (!Enabled) {
                ClearInputBlockers();
                return;
            }

            UpdateToolbarInputBlocker();
        }

        /// <summary>
        /// Initializes tool-button visuals and interactions.
        /// </summary>
        void InitializeToolButtons() {
            CreateToolButton(0, EditorViewportToolMode.Translate, ToolbarIcons.GetIcon(EditorViewportToolMode.Translate));
            CreateToolButton(1, EditorViewportToolMode.Rotate, ToolbarIcons.GetIcon(EditorViewportToolMode.Rotate));
            CreateToolButton(2, EditorViewportToolMode.Scale, ToolbarIcons.GetIcon(EditorViewportToolMode.Scale));
        }

        /// <summary>
        /// Initializes the viewport grid toggle button.
        /// </summary>
        void InitializeGridButton() {
            EditorEntity buttonRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
                RenderOrder2D = ToolbarSurfaceOrder
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = ToolbarIcons.GridIcon,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(ToolButtonIconSize, ToolButtonIconSize),
                RenderOrder2D = ToolbarForegroundOrder
            };
            iconHost.AddComponent(buttonIcon);

            InteractableComponent buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            buttonInteractable.CursorEvent += (pos, delta, state) => HandleGridButtonCursor(state);
            buttonRoot.AddComponent(buttonInteractable);

            GridButtonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                ToolModes.Length,
                false,
                () => Enabled && buttonRoot.Enabled,
                ContainsGridButtonPoint,
                isFocused => {
                    GridButtonKeyboardFocusState = isFocused;
                    UpdateGridButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ToggleGridVisibility());
            EditorKeyboardFocusService.RegisterTarget(GridButtonFocusTarget);

            GridButtonRoot = buttonRoot;
            GridButtonBackground = buttonBackground;
            GridButtonIcon = buttonIcon;
            GridButtonInteractable = buttonInteractable;
            GridButtonHoverState = false;
            GridButtonPressedState = false;
            GridButtonKeyboardFocusState = false;
            UpdateGridButtonVisuals();
        }

        /// <summary>
        /// Initializes the two snap-control groups shown on the toolbar.
        /// </summary>
        void InitializeSnapControls() {
            CreateSnapControlGroup(0, TransformGizmoSnapSlot.Snap1);
            CreateSnapControlGroup(1, TransformGizmoSnapSlot.Snap2);
            UpdateSnapControlTexts();
            UpdateSnapButtonVisuals();
        }

        /// <summary>
        /// Creates one tool-button visual and interaction entry.
        /// </summary>
        /// <param name="buttonIndex">Button index into toolbar arrays.</param>
        /// <param name="toolMode">Tool mode represented by the button.</param>
        /// <param name="iconTexture">Texture drawn inside the button.</param>
        void CreateToolButton(int buttonIndex, EditorViewportToolMode toolMode, RuntimeTexture iconTexture) {
            if (buttonIndex < 0 || buttonIndex >= ToolModes.Length) {
                throw new ArgumentOutOfRangeException(nameof(buttonIndex), "Tool button index must be inside toolbar bounds.");
            }
            if (iconTexture == null) {
                throw new ArgumentNullException(nameof(iconTexture));
            }

            EditorEntity buttonRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = ToolbarSurfaceOrder
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = iconTexture,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(ToolButtonIconSize, ToolButtonIconSize),
                RenderOrder2D = ToolbarForegroundOrder
            };
            iconHost.AddComponent(buttonIcon);

            InteractableComponent buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            int capturedButtonIndex = buttonIndex;
            buttonInteractable.CursorEvent += (pos, delta, state) => HandleToolButtonCursor(capturedButtonIndex, state);
            buttonRoot.AddComponent(buttonInteractable);
            EditorFocusTarget buttonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                buttonIndex,
                false,
                () => Enabled && buttonRoot.Enabled,
                point => ContainsToolbarButtonPoint(capturedButtonIndex, point),
                isFocused => {
                    ToolButtonKeyboardFocusStates[capturedButtonIndex] = isFocused;
                    UpdateToolButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ToolMode = toolMode);
            EditorKeyboardFocusService.RegisterTarget(buttonFocusTarget);

            ToolModes[buttonIndex] = toolMode;
            ToolButtonRoots[buttonIndex] = buttonRoot;
            ToolButtonBackgrounds[buttonIndex] = buttonBackground;
            ToolButtonIcons[buttonIndex] = buttonIcon;
            ToolButtonInteractables[buttonIndex] = buttonInteractable;
            ToolButtonHoverStates[buttonIndex] = false;
            ToolButtonPressedStates[buttonIndex] = false;
            ToolButtonFocusTargets[buttonIndex] = buttonFocusTarget;
            ToolButtonKeyboardFocusStates[buttonIndex] = false;
        }

        /// <summary>
        /// Creates one snap-control label, value display, and up/down button pair.
        /// </summary>
        /// <param name="slotIndex">Snap slot index inside toolbar arrays.</param>
        /// <param name="snapSlot">Snap slot represented by the control group.</param>
        void CreateSnapControlGroup(int slotIndex, TransformGizmoSnapSlot snapSlot) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }

            EditorEntity labelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(labelRoot);

            EditorEntity magnetIconRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            labelRoot.AddChild(magnetIconRoot);

            SpriteComponent magnetIcon = new SpriteComponent {
                Texture = ToolbarIcons.MagnetIcon,
                Color = new byte4(255, 255, 255, 255),
                Size = new int2(SnapLabelIconHeight, SnapLabelIconHeight),
                RenderOrder2D = ToolbarForegroundOrder
            };
            magnetIconRoot.AddComponent(magnetIcon);

            EditorEntity modifierTextRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            labelRoot.AddChild(modifierTextRoot);

            TextComponent modifierText = new TextComponent {
                Font = SnapModifierFont,
                Text = GetSnapModifierLabel(snapSlot),
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
                RenderOrder2D = ToolbarForegroundOrder
            };
            modifierTextRoot.AddComponent(modifierText);

            EditorEntity valueRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(valueRoot);

            SpriteComponent valueBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
                RenderOrder2D = ToolbarSurfaceOrder
            };
            valueRoot.AddComponent(valueBackground);

            EditorEntity valueTextRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            valueRoot.AddChild(valueTextRoot);

            TextComponent valueText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
                RenderOrder2D = ToolbarForegroundOrder
            };
            valueTextRoot.AddComponent(valueText);

            SnapSlots[slotIndex] = snapSlot;
            SnapLabelRoots[slotIndex] = labelRoot;
            SnapLabelMagnetIcons[slotIndex] = magnetIcon;
            SnapLabelModifierTexts[slotIndex] = modifierText;
            SnapValueRoots[slotIndex] = valueRoot;
            SnapValueBackgrounds[slotIndex] = valueBackground;
            SnapValueTexts[slotIndex] = valueText;

            CreateSnapToolbarButton(slotIndex, true, ToolbarIcons.GetSnapButtonIcon(true));
            CreateSnapToolbarButton(slotIndex, false, ToolbarIcons.GetSnapButtonIcon(false));
        }

        /// <summary>
        /// Creates one snap toolbar button used to increase or decrease a snap value.
        /// </summary>
        /// <param name="slotIndex">Snap slot index that owns the button.</param>
        /// <param name="isIncreaseButton">True to build the increase button; false for the decrease button.</param>
        /// <param name="iconTexture">Texture drawn inside the button.</param>
        void CreateSnapToolbarButton(int slotIndex, bool isIncreaseButton, RuntimeTexture iconTexture) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }
            if (iconTexture == null) {
                throw new ArgumentNullException(nameof(iconTexture));
            }

            EditorEntity buttonRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
                RenderOrder2D = ToolbarSurfaceOrder
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = iconTexture,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(SnapButtonIconWidth, SnapButtonIconHeight),
                RenderOrder2D = ToolbarForegroundOrder
            };
            iconHost.AddComponent(buttonIcon);

            InteractableComponent buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            int capturedSlotIndex = slotIndex;
            bool capturedIsIncreaseButton = isIncreaseButton;
            buttonInteractable.CursorEvent += (pos, delta, state) => HandleSnapButtonCursor(capturedSlotIndex, capturedIsIncreaseButton, state);
            buttonRoot.AddComponent(buttonInteractable);
            int tabIndex = ToolModes.Length + 1 + (slotIndex * 2) + (isIncreaseButton ? 0 : 1);
            EditorFocusTarget buttonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                tabIndex,
                false,
                () => Enabled && buttonRoot.Enabled,
                point => ContainsSnapButtonPoint(capturedSlotIndex, capturedIsIncreaseButton, point),
                isFocused => {
                    SetSnapButtonKeyboardFocusState(capturedSlotIndex, capturedIsIncreaseButton, isFocused);
                    UpdateSnapButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => AdjustSnapValue(capturedSlotIndex, capturedIsIncreaseButton));
            EditorKeyboardFocusService.RegisterTarget(buttonFocusTarget);

            if (isIncreaseButton) {
                SnapIncreaseButtonRoots[slotIndex] = buttonRoot;
                SnapIncreaseButtonBackgrounds[slotIndex] = buttonBackground;
                SnapIncreaseButtonIcons[slotIndex] = buttonIcon;
                SnapIncreaseButtonInteractables[slotIndex] = buttonInteractable;
                SnapIncreaseButtonHoverStates[slotIndex] = false;
                SnapIncreaseButtonPressedStates[slotIndex] = false;
                SnapIncreaseFocusTargets[slotIndex] = buttonFocusTarget;
                SnapIncreaseKeyboardFocusStates[slotIndex] = false;
                return;
            }

            SnapDecreaseButtonRoots[slotIndex] = buttonRoot;
            SnapDecreaseButtonBackgrounds[slotIndex] = buttonBackground;
            SnapDecreaseButtonIcons[slotIndex] = buttonIcon;
            SnapDecreaseButtonInteractables[slotIndex] = buttonInteractable;
            SnapDecreaseButtonHoverStates[slotIndex] = false;
            SnapDecreaseButtonPressedStates[slotIndex] = false;
            SnapDecreaseFocusTargets[slotIndex] = buttonFocusTarget;
            SnapDecreaseKeyboardFocusStates[slotIndex] = false;
        }

        /// <summary>
        /// Handles pointer interaction state updates for one tool button.
        /// </summary>
        /// <param name="buttonIndex">Toolbar button index receiving the pointer event.</param>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleToolButtonCursor(int buttonIndex, PointerInteraction interaction) {
            if (buttonIndex < 0 || buttonIndex >= ToolModes.Length) {
                throw new ArgumentOutOfRangeException(nameof(buttonIndex), "Tool button index must be inside toolbar bounds.");
            }

            switch (interaction) {
                case PointerInteraction.Hover:
                    ToolButtonHoverStates[buttonIndex] = true;
                    break;
                case PointerInteraction.Press:
                    ToolButtonPressedStates[buttonIndex] = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = ToolButtonPressedStates[buttonIndex] && ToolButtonHoverStates[buttonIndex];
                    ToolButtonPressedStates[buttonIndex] = false;
                    if (shouldActivate) {
                        ToolMode = ToolModes[buttonIndex];
                    }
                    break;
                case PointerInteraction.Leave:
                    ToolButtonHoverStates[buttonIndex] = false;
                    ToolButtonPressedStates[buttonIndex] = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateToolButtonVisuals();
        }

        /// <summary>
        /// Handles pointer interaction state updates for the viewport grid toggle button.
        /// </summary>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleGridButtonCursor(PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    GridButtonHoverState = true;
                    break;
                case PointerInteraction.Press:
                    GridButtonPressedState = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldToggle = GridButtonPressedState && GridButtonHoverState;
                    GridButtonPressedState = false;
                    if (shouldToggle) {
                        ToggleGridVisibility();
                    }
                    break;
                case PointerInteraction.Leave:
                    GridButtonHoverState = false;
                    GridButtonPressedState = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateGridButtonVisuals();
        }

        /// <summary>
        /// Handles pointer interaction state updates for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index receiving the pointer event.</param>
        /// <param name="isIncreaseButton">True when the event targets the increase button.</param>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleSnapButtonCursor(int slotIndex, bool isIncreaseButton, PointerInteraction interaction) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }

            switch (interaction) {
                case PointerInteraction.Hover:
                    SetSnapButtonHoverState(slotIndex, isIncreaseButton, true);
                    break;
                case PointerInteraction.Press:
                    SetSnapButtonPressedState(slotIndex, isIncreaseButton, true);
                    break;
                case PointerInteraction.Release:
                    bool shouldAdjust = GetSnapButtonPressedState(slotIndex, isIncreaseButton) &&
                                        GetSnapButtonHoverState(slotIndex, isIncreaseButton);
                    SetSnapButtonPressedState(slotIndex, isIncreaseButton, false);
                    if (shouldAdjust) {
                        AdjustSnapValue(slotIndex, isIncreaseButton);
                    }
                    break;
                case PointerInteraction.Leave:
                    SetSnapButtonHoverState(slotIndex, isIncreaseButton, false);
                    SetSnapButtonPressedState(slotIndex, isIncreaseButton, false);
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateSnapButtonVisuals();
        }

        /// <summary>
        /// Applies visual state to all tool buttons based on active, hover, and pressed states.
        /// </summary>
        void UpdateToolButtonVisuals() {
            EditorViewportToolMode activeToolMode = ToolMode;
            for (int buttonIndex = 0; buttonIndex < ToolModes.Length; buttonIndex++) {
                SpriteComponent background = ToolButtonBackgrounds[buttonIndex];
                SpriteComponent icon = ToolButtonIcons[buttonIndex];
                if (background == null || icon == null) {
                    continue;
                }

                bool isActive = ToolModes[buttonIndex] == activeToolMode;
                bool isHovered = ToolButtonHoverStates[buttonIndex];
                bool isPressed = ToolButtonPressedStates[buttonIndex];
                bool isKeyboardFocused = ToolButtonKeyboardFocusStates[buttonIndex];
                if (isPressed) {
                    background.Color = ThemeManager.Colors.AccentTertiary;
                } else if (isActive) {
                    background.Color = ThemeManager.Colors.AccentPrimary;
                } else if (isKeyboardFocused || isHovered) {
                    background.Color = ThemeManager.Colors.AccentSecondary;
                } else {
                    background.Color = ThemeManager.Colors.SurfaceInput;
                }

                if (isActive || isHovered || isPressed || isKeyboardFocused) {
                    icon.Color = new byte4(255, 255, 255, 255);
                } else {
                    icon.Color = new byte4(255, 255, 255, 224);
                }
            }
        }

        /// <summary>
        /// Applies visual state to the viewport grid toggle button.
        /// </summary>
        void UpdateGridButtonVisuals() {
            if (GridButtonBackground == null || GridButtonIcon == null) {
                return;
            }

            bool isActive = IsGridVisible();
            if (GridButtonPressedState) {
                GridButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (isActive) {
                GridButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            } else if (GridButtonKeyboardFocusState || GridButtonHoverState) {
                GridButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                GridButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            }

            if (isActive || GridButtonHoverState || GridButtonPressedState || GridButtonKeyboardFocusState) {
                GridButtonIcon.Color = new byte4(255, 255, 255, 255);
            } else {
                GridButtonIcon.Color = new byte4(255, 255, 255, 224);
            }
        }

        /// <summary>
        /// Applies the snap adjustment button colors from their hover and pressed states.
        /// </summary>
        void UpdateSnapButtonVisuals() {
            for (int slotIndex = 0; slotIndex < SnapSlots.Length; slotIndex++) {
                UpdateSnapButtonVisual(slotIndex, true);
                UpdateSnapButtonVisual(slotIndex, false);
            }
        }

        /// <summary>
        /// Applies the visual state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when styling the increase button.</param>
        void UpdateSnapButtonVisual(int slotIndex, bool isIncreaseButton) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }

            SpriteComponent background = isIncreaseButton
                ? SnapIncreaseButtonBackgrounds[slotIndex]
                : SnapDecreaseButtonBackgrounds[slotIndex];
            SpriteComponent icon = isIncreaseButton
                ? SnapIncreaseButtonIcons[slotIndex]
                : SnapDecreaseButtonIcons[slotIndex];
            if (background == null || icon == null) {
                return;
            }

            bool isHovered = GetSnapButtonHoverState(slotIndex, isIncreaseButton);
            bool isPressed = GetSnapButtonPressedState(slotIndex, isIncreaseButton);
            bool isKeyboardFocused = GetSnapButtonKeyboardFocusState(slotIndex, isIncreaseButton);
            if (isPressed) {
                background.Color = ThemeManager.Colors.AccentTertiary;
            } else if (isKeyboardFocused || isHovered) {
                background.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                background.Color = ThemeManager.Colors.SurfaceInput;
            }
            if (isHovered || isPressed || isKeyboardFocused) {
                icon.Color = new byte4(255, 255, 255, 255);
            } else {
                icon.Color = new byte4(255, 255, 255, 224);
            }
        }

        /// <summary>
        /// Sets the hover state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when updating the increase button.</param>
        /// <param name="value">New hover state.</param>
        void SetSnapButtonHoverState(int slotIndex, bool isIncreaseButton, bool value) {
            if (isIncreaseButton) {
                SnapIncreaseButtonHoverStates[slotIndex] = value;
                return;
            }

            SnapDecreaseButtonHoverStates[slotIndex] = value;
        }

        /// <summary>
        /// Reads the hover state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when reading the increase button.</param>
        /// <returns>Current hover state.</returns>
        bool GetSnapButtonHoverState(int slotIndex, bool isIncreaseButton) {
            return isIncreaseButton
                ? SnapIncreaseButtonHoverStates[slotIndex]
                : SnapDecreaseButtonHoverStates[slotIndex];
        }

        /// <summary>
        /// Sets the keyboard-focus state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when updating the increase button.</param>
        /// <param name="value">New keyboard-focus state.</param>
        void SetSnapButtonKeyboardFocusState(int slotIndex, bool isIncreaseButton, bool value) {
            if (isIncreaseButton) {
                SnapIncreaseKeyboardFocusStates[slotIndex] = value;
                return;
            }

            SnapDecreaseKeyboardFocusStates[slotIndex] = value;
        }

        /// <summary>
        /// Reads the keyboard-focus state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when reading the increase button.</param>
        /// <returns>Current keyboard-focus state.</returns>
        bool GetSnapButtonKeyboardFocusState(int slotIndex, bool isIncreaseButton) {
            return isIncreaseButton
                ? SnapIncreaseKeyboardFocusStates[slotIndex]
                : SnapDecreaseKeyboardFocusStates[slotIndex];
        }

        /// <summary>
        /// Sets the pressed state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when updating the increase button.</param>
        /// <param name="value">New pressed state.</param>
        void SetSnapButtonPressedState(int slotIndex, bool isIncreaseButton, bool value) {
            if (isIncreaseButton) {
                SnapIncreaseButtonPressedStates[slotIndex] = value;
                return;
            }

            SnapDecreaseButtonPressedStates[slotIndex] = value;
        }

        /// <summary>
        /// Reads the pressed state for one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when reading the increase button.</param>
        /// <returns>Current pressed state.</returns>
        bool GetSnapButtonPressedState(int slotIndex, bool isIncreaseButton) {
            return isIncreaseButton
                ? SnapIncreaseButtonPressedStates[slotIndex]
                : SnapDecreaseButtonPressedStates[slotIndex];
        }

        /// <summary>
        /// Adjusts a snap value for the current tool mode and refreshes the toolbar text.
        /// </summary>
        /// <param name="slotIndex">Snap slot index to adjust.</param>
        /// <param name="isIncreaseButton">True to increase the value; false to decrease it.</param>
        void AdjustSnapValue(int slotIndex, bool isIncreaseButton) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }

            TransformGizmoSnapSlot snapSlot = SnapSlots[slotIndex];
            if (isIncreaseButton) {
                TransformGizmoSnapSettingsService.IncreaseSnapValue(ToolMode, snapSlot);
            } else {
                TransformGizmoSnapSettingsService.DecreaseSnapValue(ToolMode, snapSlot);
            }

            UpdateSnapControlTexts();
        }

        /// <summary>
        /// Refreshes snap value readouts to match the current tool mode configuration.
        /// </summary>
        void UpdateSnapControlTexts() {
            for (int slotIndex = 0; slotIndex < SnapSlots.Length; slotIndex++) {
                TextComponent valueText = SnapValueTexts[slotIndex];
                if (valueText == null) {
                    continue;
                }

                double snapValue = TransformGizmoSnapSettingsService.GetSnapValue(ToolMode, SnapSlots[slotIndex]);
                valueText.Text = FormatSnapValue(snapValue);
            }

            LayoutToolbar();
        }

        /// <summary>
        /// Applies a tool mode selection for this viewport camera.
        /// </summary>
        /// <param name="toolMode">Tool mode to assign.</param>
        void SetToolMode(EditorViewportToolMode toolMode) {
            EditorViewportToolService.SetToolMode(Camera, toolMode);
            if (toolMode != EditorViewportToolMode.Translate) {
                EditorGizmoHoverService.ClearHoveredHandle();
            }

            UpdateToolButtonVisuals();
            UpdateSnapControlTexts();
        }

        /// <summary>
        /// Refreshes toolbar button visuals when a nested viewport subgroup changes active state.
        /// </summary>
        /// <param name="isActive">True when the nested group is active.</param>
        void HandleSubviewGroupActiveChanged(bool isActive) {
            UpdateToolButtonVisuals();
            UpdateGridButtonVisuals();
            UpdateSnapButtonVisuals();
        }

        /// <summary>
        /// Tracks whether the viewport content target currently owns keyboard focus.
        /// </summary>
        /// <param name="isFocused">True when the content target is focused.</param>
        void HandleViewportContentFocusedChanged(bool isFocused) {
            IsViewportContentFocused = isFocused;
        }

        /// <summary>
        /// Returns true when the viewport content target should react to one activation key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when the content target should activate for the key.</returns>
        bool CanActivateViewportContentKey(Keys key) {
            InputSystem inputManager = Core.Instance.Input;
            if (inputManager != null && inputManager.GetMouseRightButtonState() == ButtonState.Pressed) {
                return false;
            }

            return key == Keys.W || key == Keys.R || key == Keys.S;
        }

        /// <summary>
        /// Applies one gizmo tool shortcut routed through the focused viewport content target.
        /// </summary>
        /// <param name="key">Activation key routed through keyboard focus.</param>
        void ActivateViewportContentKey(Keys key) {
            if (key == Keys.W) {
                ToolMode = EditorViewportToolMode.Translate;
            } else if (key == Keys.R) {
                ToolMode = EditorViewportToolMode.Rotate;
            } else if (key == Keys.S) {
                ToolMode = EditorViewportToolMode.Scale;
            }
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the viewport content region below the toolbar.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the viewport content region.</returns>
        bool ContainsViewportContentPoint(int2 point) {
            if (Camera == null) {
                return false;
            }

            float4 viewport = Camera.Viewport;
            int left = (int)Math.Round(viewport.X);
            int top = (int)Math.Round(viewport.Y);
            int width = Math.Max(1, (int)Math.Round(viewport.Z));
            int height = Math.Max(1, (int)Math.Round(viewport.W));
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the viewport toolbar region.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the toolbar bounds.</returns>
        bool ContainsToolbarPoint(int2 point) {
            int left = (int)Math.Round(Position.X + ToolbarRoot.Position.X);
            int top = (int)Math.Round(Position.Y + ToolbarRoot.Position.Y);
            int width = ToolbarBackground.Size.X;
            int height = ToolbarBackground.Size.Y;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside one toolbar tool button.
        /// </summary>
        /// <param name="buttonIndex">Toolbar tool-button index to evaluate.</param>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the tool button bounds.</returns>
        bool ContainsToolbarButtonPoint(int buttonIndex, int2 point) {
            if (buttonIndex < 0 || buttonIndex >= ToolButtonRoots.Length) {
                throw new ArgumentOutOfRangeException(nameof(buttonIndex), "Tool button index must be inside toolbar bounds.");
            }

            EditorEntity buttonRoot = ToolButtonRoots[buttonIndex];
            InteractableComponent buttonInteractable = ToolButtonInteractables[buttonIndex];
            if (buttonRoot == null || buttonInteractable == null) {
                return false;
            }

            int left = (int)Math.Round(Position.X + ToolbarRoot.Position.X + buttonRoot.Position.X);
            int top = (int)Math.Round(Position.Y + ToolbarRoot.Position.Y + buttonRoot.Position.Y);
            int width = buttonInteractable.Size.X;
            int height = buttonInteractable.Size.Y;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the viewport grid toggle button.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the grid button bounds.</returns>
        bool ContainsGridButtonPoint(int2 point) {
            if (GridButtonRoot == null || GridButtonInteractable == null) {
                return false;
            }

            int left = (int)Math.Round(Position.X + ToolbarRoot.Position.X + GridButtonRoot.Position.X);
            int top = (int)Math.Round(Position.Y + ToolbarRoot.Position.Y + GridButtonRoot.Position.Y);
            int width = GridButtonInteractable.Size.X;
            int height = GridButtonInteractable.Size.Y;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside one snap adjustment button.
        /// </summary>
        /// <param name="slotIndex">Snap slot index owning the button.</param>
        /// <param name="isIncreaseButton">True when evaluating the increase button.</param>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the snap button bounds.</returns>
        bool ContainsSnapButtonPoint(int slotIndex, bool isIncreaseButton, int2 point) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }

            EditorEntity buttonRoot = isIncreaseButton
                ? SnapIncreaseButtonRoots[slotIndex]
                : SnapDecreaseButtonRoots[slotIndex];
            InteractableComponent buttonInteractable = isIncreaseButton
                ? SnapIncreaseButtonInteractables[slotIndex]
                : SnapDecreaseButtonInteractables[slotIndex];
            if (buttonRoot == null || buttonInteractable == null) {
                return false;
            }

            int left = (int)Math.Round(Position.X + ToolbarRoot.Position.X + buttonRoot.Position.X);
            int top = (int)Math.Round(Position.Y + ToolbarRoot.Position.Y + buttonRoot.Position.Y);
            int width = buttonInteractable.Size.X;
            int height = buttonInteractable.Size.Y;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Updates the camera viewport, toolbar layout, and toolbar input-blocker bounds.
        /// </summary>
        void UpdateViewport() {
            if (Camera == null) {
                return;
            }

            if (!Enabled) {
                ClearInputBlockers();
                return;
            }

            float viewportWidth = Math.Max(1f, Size.X);
            float viewportHeight = Math.Max(1f, Size.Y - ToolbarHeight);
            float viewportTop = Position.Y + TitleBarHeight + ToolbarHeight;
            Camera.Viewport = new float4(Position.X, viewportTop, viewportWidth, viewportHeight);
            LayoutToolbar();
            RefreshInputBlockers();
        }

        /// <summary>
        /// Lays out toolbar and tool-button positions based on current panel dimensions.
        /// </summary>
        void LayoutToolbar() {
            float toolbarX = 0f;
            float toolbarY = TitleBarHeight;
            ToolbarRoot.Position = new float3(toolbarX, toolbarY, 0.2f);

            int toolbarWidth = GetToolbarWidth();
            ToolbarBackground.Size = new int2(toolbarWidth, ToolbarHeight);
            float buttonY = (float)Math.Round((ToolbarHeight - ToolButtonHeight) * 0.5);
            for (int buttonIndex = 0; buttonIndex < ToolModes.Length; buttonIndex++) {
                EditorEntity buttonRoot = ToolButtonRoots[buttonIndex];
                SpriteComponent buttonBackground = ToolButtonBackgrounds[buttonIndex];
                SpriteComponent buttonIcon = ToolButtonIcons[buttonIndex];
                InteractableComponent buttonInteractable = ToolButtonInteractables[buttonIndex];
                if (buttonRoot == null || buttonBackground == null || buttonIcon == null || buttonInteractable == null) {
                    continue;
                }

                float buttonX = ToolbarPadding + buttonIndex * (ToolButtonWidth + ToolbarButtonSpacing);
                buttonRoot.Position = new float3(buttonX, buttonY, 0.1f);
                buttonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                buttonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                LayoutToolButtonIcon(buttonIcon, ToolButtonWidth, ToolButtonHeight, ToolButtonIconSize, ToolButtonIconSize);
            }

            if (GridButtonRoot != null && GridButtonBackground != null && GridButtonIcon != null && GridButtonInteractable != null) {
                float gridButtonX = ToolbarPadding + ToolModes.Length * (ToolButtonWidth + ToolbarButtonSpacing);
                GridButtonRoot.Position = new float3(gridButtonX, buttonY, 0.1f);
                GridButtonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                GridButtonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                LayoutToolButtonIcon(GridButtonIcon, ToolButtonWidth, ToolButtonHeight, ToolButtonIconSize, ToolButtonIconSize);
            }

            LayoutSnapControls(buttonY);
        }

        /// <summary>
        /// Centers a tool-button icon inside its button background.
        /// </summary>
        /// <param name="buttonIcon">Sprite component to layout.</param>
        /// <param name="buttonWidth">Width of the button.</param>
        /// <param name="buttonHeight">Height of the button.</param>
        /// <param name="iconWidth">Width of the icon sprite.</param>
        /// <param name="iconHeight">Height of the icon sprite.</param>
        void LayoutToolButtonIcon(SpriteComponent buttonIcon, int buttonWidth, int buttonHeight, int iconWidth, int iconHeight) {
            if (buttonIcon == null) {
                throw new ArgumentNullException(nameof(buttonIcon));
            }

            float iconX = (float)Math.Round((buttonWidth - iconWidth) * 0.5);
            float iconY = (float)Math.Round((buttonHeight - iconHeight) * 0.5);
            if (buttonIcon.Parent != null) {
                buttonIcon.Parent.Position = new float3(iconX, iconY, 0.1f);
            }

            buttonIcon.Size = new int2(iconWidth, iconHeight);
        }

        /// <summary>
        /// Lays out one inline toolbar icon using its texture aspect ratio.
        /// </summary>
        /// <param name="icon">Sprite component to layout.</param>
        /// <param name="x">Left position inside the icon's parent group.</param>
        /// <param name="y">Top position inside the icon's parent group.</param>
        /// <param name="iconHeight">Target icon height in pixels.</param>
        /// <returns>Computed icon width in pixels.</returns>
        int LayoutInlineToolbarIcon(SpriteComponent icon, float x, float y, int iconHeight) {
            if (icon == null) {
                throw new ArgumentNullException(nameof(icon));
            }
            if (icon.Texture == null) {
                throw new InvalidOperationException("Toolbar icon texture must be assigned before layout.");
            }
            if (icon.Texture.Height <= 0) {
                throw new InvalidOperationException("Toolbar icon texture height must be positive.");
            }

            int iconWidth = Math.Max(1, (int)Math.Round((double)icon.Texture.Width / icon.Texture.Height * iconHeight));
            if (icon.Parent != null) {
                icon.Parent.Position = new float3(x, y, 0.1f);
            }

            icon.Size = new int2(iconWidth, iconHeight);
            return iconWidth;
        }

        /// <summary>
        /// Computes the full width consumed by one snap-slot label.
        /// </summary>
        /// <param name="magnetIcon">Magnet icon sprite.</param>
        /// <param name="modifierText">Modifier key text label.</param>
        /// <returns>Total snap label width in pixels.</returns>
        int GetSnapLabelWidth(SpriteComponent magnetIcon, TextComponent modifierText) {
            if (magnetIcon == null) {
                throw new ArgumentNullException(nameof(magnetIcon));
            }
            if (modifierText == null) {
                throw new ArgumentNullException(nameof(modifierText));
            }

            int magnetWidth = GetToolbarIconWidthForHeight(magnetIcon.Texture, SnapLabelIconHeight);
            int modifierWidth = GetToolbarInlineTextWidth(modifierText);
            return magnetWidth + SnapLabelIconSpacing + modifierWidth;
        }

        /// <summary>
        /// Computes the display width of one inline toolbar text label.
        /// </summary>
        /// <param name="text">Text component whose label width should be measured.</param>
        /// <returns>Display width in pixels.</returns>
        int GetToolbarInlineTextWidth(TextComponent text) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }
            if (text.Font == null) {
                throw new InvalidOperationException("Toolbar text font must be assigned before layout.");
            }

            FontTightMetrics metrics = text.Font.MeasureTight(text.Text);
            return Math.Max(1, (int)Math.Ceiling(metrics.Width));
        }

        /// <summary>
        /// Lays out one inline toolbar text label.
        /// </summary>
        /// <param name="text">Text component to layout.</param>
        /// <param name="x">Left position inside the label group.</param>
        /// <param name="containerHeight">Height of the containing toolbar row.</param>
        void LayoutInlineToolbarText(TextComponent text, float x, int containerHeight) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }
            if (text.Font == null) {
                throw new InvalidOperationException("Toolbar text font must be assigned before layout.");
            }

            FontTightMetrics metrics = text.Font.MeasureTight(text.Text);
            float textY = GetTextTopOffset(containerHeight, metrics);
            if (text.Parent != null) {
                text.Parent.Position = new float3(x, textY, 0.1f);
            }

            text.Size = new int2(
                Math.Max(1, (int)Math.Ceiling(metrics.Width)),
                Math.Max(1, (int)Math.Ceiling(metrics.Height)));
        }

        /// <summary>
        /// Resolves the modifier label text shown for a snap slot.
        /// </summary>
        /// <param name="snapSlot">Snap slot whose modifier label should be displayed.</param>
        /// <returns>Modifier label text for the slot.</returns>
        string GetSnapModifierLabel(TransformGizmoSnapSlot snapSlot) {
            switch (snapSlot) {
                case TransformGizmoSnapSlot.Snap1:
                    return "CTRL";
                case TransformGizmoSnapSlot.Snap2:
                    return "SHIFT";
                default:
                    throw new InvalidOperationException("Toolbar snap label text is not defined for the requested snap slot.");
            }
        }

        /// <summary>
        /// Computes the display width of one toolbar icon for a target height.
        /// </summary>
        /// <param name="texture">Runtime texture backing the icon.</param>
        /// <param name="iconHeight">Target icon height in pixels.</param>
        /// <returns>Display width in pixels that preserves the texture aspect ratio.</returns>
        int GetToolbarIconWidthForHeight(RuntimeTexture texture, int iconHeight) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }
            if (texture.Height <= 0) {
                throw new InvalidOperationException("Toolbar icon texture height must be positive.");
            }

            return Math.Max(1, (int)Math.Round((double)texture.Width / texture.Height * iconHeight));
        }

        /// <summary>
        /// Centers one text label using the toolbar font's tight metrics.
        /// </summary>
        /// <param name="buttonText">Text component to layout.</param>
        /// <param name="buttonWidth">Width of the containing box.</param>
        /// <param name="buttonHeight">Height of the containing box.</param>
        void LayoutToolButtonLabel(TextComponent buttonText, int buttonWidth, int buttonHeight) {
            if (buttonText == null) {
                throw new ArgumentNullException(nameof(buttonText));
            }
            if (buttonText.Font == null) {
                throw new InvalidOperationException("Toolbar text font must be assigned before layout.");
            }

            FontTightMetrics metrics = buttonText.Font.MeasureTight(buttonText.Text);
            float textX = (float)Math.Round((buttonWidth - metrics.Width) * 0.5);
            float textY = GetTextTopOffset(buttonHeight, metrics);
            if (buttonText.Parent != null) {
                buttonText.Parent.Position = new float3(textX, textY, 0.1f);
            }

            buttonText.Size = new int2((int)Math.Ceiling(metrics.Width), (int)Math.Ceiling(metrics.Height));
        }

        /// <summary>
        /// Lays out snap labels, value boxes, and adjustment buttons across the toolbar row.
        /// </summary>
        /// <param name="buttonY">Top offset shared by value boxes and adjustment buttons.</param>
        void LayoutSnapControls(float buttonY) {
            double currentX = ToolbarPadding + ((ToolModes.Length + 1) * (ToolButtonWidth + ToolbarButtonSpacing));
            currentX -= ToolbarButtonSpacing;
            currentX += SnapGroupSpacing;

            for (int slotIndex = 0; slotIndex < SnapSlots.Length; slotIndex++) {
                EditorEntity labelRoot = SnapLabelRoots[slotIndex];
                SpriteComponent magnetIcon = SnapLabelMagnetIcons[slotIndex];
                TextComponent modifierText = SnapLabelModifierTexts[slotIndex];
                EditorEntity valueRoot = SnapValueRoots[slotIndex];
                SpriteComponent valueBackground = SnapValueBackgrounds[slotIndex];
                TextComponent valueText = SnapValueTexts[slotIndex];
                EditorEntity increaseButtonRoot = SnapIncreaseButtonRoots[slotIndex];
                SpriteComponent increaseButtonBackground = SnapIncreaseButtonBackgrounds[slotIndex];
                SpriteComponent increaseButtonIcon = SnapIncreaseButtonIcons[slotIndex];
                InteractableComponent increaseButtonInteractable = SnapIncreaseButtonInteractables[slotIndex];
                EditorEntity decreaseButtonRoot = SnapDecreaseButtonRoots[slotIndex];
                SpriteComponent decreaseButtonBackground = SnapDecreaseButtonBackgrounds[slotIndex];
                SpriteComponent decreaseButtonIcon = SnapDecreaseButtonIcons[slotIndex];
                InteractableComponent decreaseButtonInteractable = SnapDecreaseButtonInteractables[slotIndex];
                if (labelRoot == null ||
                    magnetIcon == null ||
                    modifierText == null ||
                    valueRoot == null ||
                    valueBackground == null ||
                    valueText == null ||
                    increaseButtonRoot == null ||
                    increaseButtonBackground == null ||
                    increaseButtonIcon == null ||
                    increaseButtonInteractable == null ||
                    decreaseButtonRoot == null ||
                    decreaseButtonBackground == null ||
                    decreaseButtonIcon == null ||
                    decreaseButtonInteractable == null) {
                    continue;
                }

                float labelX = (float)Math.Round(currentX);
                labelRoot.Position = new float3(labelX, buttonY, 0.1f);
                float labelIconY = (float)Math.Round((ToolButtonHeight - SnapLabelIconHeight) * 0.5);
                int magnetWidth = LayoutInlineToolbarIcon(magnetIcon, 0f, labelIconY, SnapLabelIconHeight);
                LayoutInlineToolbarText(modifierText, magnetWidth + SnapLabelIconSpacing, ToolButtonHeight);

                currentX += GetSnapLabelWidth(magnetIcon, modifierText) + SnapLabelSpacing;

                float valueX = (float)Math.Round(currentX);
                valueRoot.Position = new float3(valueX, buttonY, 0.1f);
                valueBackground.Size = new int2(SnapValueWidth, ToolButtonHeight);
                LayoutToolButtonLabel(valueText, SnapValueWidth, ToolButtonHeight);

                currentX += SnapValueWidth + ToolbarButtonSpacing;

                float increaseButtonX = (float)Math.Round(currentX);
                increaseButtonRoot.Position = new float3(increaseButtonX, buttonY, 0.1f);
                increaseButtonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                increaseButtonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                LayoutToolButtonIcon(increaseButtonIcon, ToolButtonWidth, ToolButtonHeight, SnapButtonIconWidth, SnapButtonIconHeight);

                currentX += ToolButtonWidth + ToolbarButtonSpacing;

                float decreaseButtonX = (float)Math.Round(currentX);
                decreaseButtonRoot.Position = new float3(decreaseButtonX, buttonY, 0.1f);
                decreaseButtonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                decreaseButtonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                LayoutToolButtonIcon(decreaseButtonIcon, ToolButtonWidth, ToolButtonHeight, SnapButtonIconWidth, SnapButtonIconHeight);

                currentX += ToolButtonWidth + SnapGroupSpacing;
            }
        }

        /// <summary>
        /// Computes the top offset needed to vertically center text from tight metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container.</param>
        /// <param name="metrics">Tight text metrics.</param>
        /// <returns>Top offset that vertically centers the text.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return (float)Math.Round((containerHeight - metrics.Height) * 0.5 - metrics.MinTop);
        }

        /// <summary>
        /// Formats one snap value for compact toolbar display.
        /// </summary>
        /// <param name="value">Snap value to format.</param>
        /// <returns>Toolbar-friendly numeric string.</returns>
        string FormatSnapValue(double value) {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns true when the scene-grid layer is visible in this viewport.
        /// </summary>
        /// <returns>True when the viewport camera renders the scene grid.</returns>
        bool IsGridVisible() {
            if (Camera == null) {
                return false;
            }

            return (Camera.LayerMask & EditorLayerMasks.SceneGrid) != 0;
        }

        /// <summary>
        /// Toggles scene-grid visibility for this viewport camera.
        /// </summary>
        void ToggleGridVisibility() {
            SetGridVisible(!IsGridVisible());
        }

        /// <summary>
        /// Sets scene-grid visibility for this viewport camera.
        /// </summary>
        /// <param name="isVisible">True to include the scene-grid layer; false to hide it.</param>
        void SetGridVisible(bool isVisible) {
            if (Camera == null) {
                throw new InvalidOperationException("Viewport camera must be assigned before changing grid visibility.");
            }

            ushort layerMask = Camera.LayerMask;
            if (isVisible) {
                layerMask = (ushort)(layerMask | EditorLayerMasks.SceneGrid);
            } else {
                layerMask = (ushort)(layerMask & ~EditorLayerMasks.SceneGrid);
            }

            Camera.LayerMask = layerMask;
            UpdateGridButtonVisuals();
        }

        /// <summary>
        /// Computes the toolbar width so the bar spans the full viewport panel width.
        /// </summary>
        /// <returns>Toolbar width in pixels.</returns>
        int GetToolbarWidth() {
            return Math.Max(1, Size.X);
        }

        /// <summary>
        /// Registers or updates the toolbar area as an input-blocker region.
        /// </summary>
        void UpdateToolbarInputBlocker() {
            int2 blockerSize = ToolbarBackground.Size;
            if (blockerSize.X <= 0 || blockerSize.Y <= 0) {
                ClearInputBlockers();
                return;
            }

            int blockerX = (int)Math.Round(Position.X + ToolbarRoot.Position.X);
            int blockerY = (int)Math.Round(Position.Y + ToolbarRoot.Position.Y);
            EditorInputCaptureService.SetBlocker(ToolbarInputBlockerOwner, new int2(blockerX, blockerY), blockerSize);
        }
    }
}


