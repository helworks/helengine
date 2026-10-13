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
        /// Smallest width of the Play platform selector, so a very short platform name still reads as a drop-down.
        /// </summary>
        const int PlayPlatformSelectorMinimumWidth = 40;
        /// <summary>
        /// Horizontal padding the combo box draws left of its label and right of its arrow glyph.
        /// </summary>
        const int PlayPlatformSelectorEdgePadding = 8;
        /// <summary>
        /// Gap kept between the longest platform name and the drop-down arrow glyph.
        /// </summary>
        const int PlayPlatformSelectorArrowGap = 6;
        /// <summary>
        /// Arrow glyph the combo box draws at its right edge, measured to size the selector.
        /// </summary>
        const string PlayPlatformSelectorArrowGlyph = "v";

        /// <summary>
        /// Owner key used to register the toolbar input blocker.
        /// </summary>
        readonly object ToolbarInputBlockerOwner;
        /// <summary>
        /// Font used by toolbar text content such as value readouts.
        /// </summary>
        FontAsset Font;
        /// <summary>
        /// Font used by the snap modifier labels so CTRL and SHIFT remain readable in the toolbar.
        /// </summary>
        FontAsset SnapModifierFont;
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
        /// Root entity for the viewport settings button.
        /// </summary>
        EditorEntity SettingsButtonRoot;
        /// <summary>
        /// Background sprite for the viewport settings button.
        /// </summary>
        SpriteComponent SettingsButtonBackground;
        /// <summary>
        /// Icon sprite for the viewport settings button.
        /// </summary>
        SpriteComponent SettingsButtonIcon;
        /// <summary>
        /// Interactable region for the viewport settings button.
        /// </summary>
        InteractableComponent SettingsButtonInteractable;
        /// <summary>
        /// Focus target bound to the viewport settings button.
        /// </summary>
        EditorFocusTarget SettingsButtonFocusTarget;
        /// <summary>
        /// Tracks hover state for the viewport settings button.
        /// </summary>
        bool SettingsButtonHoverState;
        /// <summary>
        /// Tracks pressed state for the viewport settings button.
        /// </summary>
        bool SettingsButtonPressedState;
        /// <summary>
        /// Tracks keyboard-focus state for the viewport settings button.
        /// </summary>
        bool SettingsButtonKeyboardFocusState;
        /// <summary>
        /// Overlay component that hosts viewport-specific settings controls.
        /// </summary>
        EditorViewportSettingsOverlayComponent SettingsOverlayComponent;
        /// <summary>
        /// Root entity for the viewport stats toggle button.
        /// </summary>
        EditorEntity StatsButtonRoot;
        /// <summary>
        /// Background sprite for the viewport stats toggle button.
        /// </summary>
        SpriteComponent StatsButtonBackground;
        /// <summary>
        /// Icon sprite rendered inside the stats toggle button.
        /// </summary>
        SpriteComponent StatsButtonIcon;
        /// <summary>
        /// Interactable region for the stats toggle button.
        /// </summary>
        InteractableComponent StatsButtonInteractable;
        /// <summary>
        /// Tracks pointer hover over the stats toggle button.
        /// </summary>
        bool StatsButtonHoverState;
        /// <summary>
        /// Tracks pointer press on the stats toggle button.
        /// </summary>
        bool StatsButtonPressedState;
        /// <summary>
        /// Tracks keyboard focus on the stats toggle button.
        /// </summary>
        bool StatsButtonKeyboardFocusState;
        /// <summary>
        /// Focus target bound to the stats toggle button.
        /// </summary>
        EditorFocusTarget StatsButtonFocusTarget;
        /// <summary>
        /// Overlay component that renders the toggleable viewport stats box.
        /// </summary>
        EditorViewportStatsOverlayComponent StatsOverlayComponent;
        /// <summary>
        /// Root entity for the Play button that launches the desktop player.
        /// </summary>
        EditorEntity PlayButtonRoot;
        /// <summary>
        /// Background sprite for the Play button.
        /// </summary>
        SpriteComponent PlayButtonBackground;
        /// <summary>
        /// Icon sprite rendered inside the Play button, or null when no Play glyph was loaded.
        /// </summary>
        SpriteComponent PlayButtonIcon;
        /// <summary>
        /// Interactable region for the Play button.
        /// </summary>
        InteractableComponent PlayButtonInteractable;
        /// <summary>
        /// Focus target bound to the Play button.
        /// </summary>
        EditorFocusTarget PlayButtonFocusTarget;
        /// <summary>
        /// Tracks pointer hover over the Play button.
        /// </summary>
        bool PlayButtonHoverState;
        /// <summary>
        /// Tracks pointer press on the Play button.
        /// </summary>
        bool PlayButtonPressedState;
        /// <summary>
        /// Tracks keyboard focus on the Play button.
        /// </summary>
        bool PlayButtonKeyboardFocusState;
        /// <summary>
        /// Root entity for the Stop button that terminates the running player.
        /// </summary>
        EditorEntity StopButtonRoot;
        /// <summary>
        /// Background sprite for the Stop button.
        /// </summary>
        SpriteComponent StopButtonBackground;
        /// <summary>
        /// Icon sprite rendered inside the Stop button, or null when no Stop glyph was loaded.
        /// </summary>
        SpriteComponent StopButtonIcon;
        /// <summary>
        /// Interactable region for the Stop button.
        /// </summary>
        InteractableComponent StopButtonInteractable;
        /// <summary>
        /// Focus target bound to the Stop button.
        /// </summary>
        EditorFocusTarget StopButtonFocusTarget;
        /// <summary>
        /// Tracks pointer hover over the Stop button.
        /// </summary>
        bool StopButtonHoverState;
        /// <summary>
        /// Tracks pointer press on the Stop button.
        /// </summary>
        bool StopButtonPressedState;
        /// <summary>
        /// Tracks keyboard focus on the Stop button.
        /// </summary>
        bool StopButtonKeyboardFocusState;
        /// <summary>
        /// Play session phase last presented by the Play and Stop buttons.
        /// </summary>
        EditorPlaySessionState PresentedPlayState;
        /// <summary>
        /// Whether the selected Play platform supports Play; when false the Play button is dimmed and inert.
        /// </summary>
        bool PresentedPlayAvailable;
        /// <summary>
        /// Toolbar entity hosting the Play platform selector between the Play and Stop buttons.
        /// </summary>
        EditorEntity PlayPlatformComboHost;
        /// <summary>
        /// Drop-down that shows the selected Play platform's display name and lets the user pick another one.
        /// </summary>
        ComboBoxComponent PlayPlatformComboBox;
        /// <summary>
        /// Platform ids of the presented selector items, index-aligned with the combo-box display names.
        /// </summary>
        readonly List<string> PresentedPlayPlatformIds = new List<string>();
        /// <summary>
        /// Platform id the session last presented as selected; empty when no platform is playable.
        /// </summary>
        string PresentedPlayPlatformId = string.Empty;
        /// <summary>
        /// Current width of the Play platform selector, fitted to the longest offered platform name in the toolbar font.
        /// </summary>
        int PlayPlatformSelectorWidth = PlayPlatformSelectorMinimumWidth;
        /// <summary>
        /// Overlay component that owns viewport-local world-space gizmo labels.
        /// </summary>
        EditorViewportCameraAngleOverlayComponent CameraAngleOverlayComponentValue;
        /// <summary>
        /// Viewport-local navigation controller shared by the overlay and workspace state.
        /// </summary>
        EditorViewportNavigationController NavigationControllerValue;
        /// <summary>
        /// View that renders and routes input for the navigation cube.
        /// </summary>
        EditorViewportNavigationCube NavigationCubeValue;
        /// <summary>
        /// Updater that owns the navigation view's editor lifecycle.
        /// </summary>
        EditorViewportNavigationCubeUpdateComponent NavigationCubeUpdateComponentValue;
        /// <summary>
        /// Scene-owned canvas profile state used by viewport previews.
        /// </summary>
        readonly EditorSceneCanvasProfileState SceneCanvasProfileStateValue;
        /// <summary>Session-owned built-in shader library used by viewport overlays.</summary>
        readonly EditorBuiltInShaderAssetLibrary BuiltInShaderLibrary;
        /// <summary>Renderer resources owned by the editor session that owns this viewport.</summary>
        readonly EditorSessionRendererResources RendererResources;
        /// <summary>
        /// Viewport-local simulated canvas settings used by the world-space 2D preview plane.
        /// </summary>
        readonly EditorViewportCanvasPreviewSettings CanvasPreviewSettingsValue;
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
        /// Textboxes used to display and edit snap values.
        /// </summary>
        readonly TextBoxComponent[] SnapValueTextBoxes;
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
        /// Raised when the viewport content focus target changes focus state.
        /// </summary>
        public event Action<EditorViewport, bool> ViewportContentFocusedChanged;

        /// <summary>
        /// Raised when the user activates the Play button while the selected Play platform supports Play. The argument is true
        /// when Shift was held during the activation, asking for the player to wait for a debugger before it starts.
        /// </summary>
        public event Action<bool> PlayRequested;

        /// <summary>
        /// Raised when the user activates the Stop button while a Play session is preparing or running.
        /// </summary>
        public event Action StopRequested;

        /// <summary>
        /// Raised when the user picks a different platform in the Play platform selector. The argument is the picked
        /// platform id; selections presented by the session through <see cref="SetPlayPlatformOptions"/> do not raise it.
        /// </summary>
        public event Action<string> PlayPlatformSelected;

        /// <summary>
        /// Initializes a new dockable viewport and binds it to the provided camera.
        /// </summary>
        /// <param name="camera">Camera rendering into the viewport.</param>
        /// <param name="font">Font used by the base dockable entity title bar.</param>
        /// <param name="snapModifierFont">Font used by the snap modifier labels.</param>
        /// <param name="toolbarIcons">Runtime toolbar icon textures used by the transform and snap buttons.</param>
        /// <summary>
        /// Initializes a new dockable viewport and binds it to the provided camera using one shared scene canvas state and scaled dock metrics.
        /// </summary>
        /// <param name="camera">Camera rendering into the viewport.</param>
        /// <param name="font">Font used by the base dockable entity title bar.</param>
        /// <param name="snapModifierFont">Font used by the snap modifier labels.</param>
        /// <param name="toolbarIcons">Runtime toolbar icon textures used by the transform and snap buttons.</param>
        /// <param name="sceneCanvasProfileState">Scene-owned canvas profile used by viewport previews.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock title bar.</param>
        /// <summary>
        /// Initializes a viewport against an explicit owning core so every
        /// toolbar entity is registered with the same session graph even when
        /// another core was constructed more recently.
        /// </summary>
        public EditorViewport(Core ownerCore, CameraComponent camera, FontAsset font, FontAsset snapModifierFont, EditorViewportToolbarIconSet toolbarIcons, EditorSceneCanvasProfileState sceneCanvasProfileState, EditorUiMetrics metrics, EditorBuiltInShaderAssetLibrary builtInShaderLibrary, EditorSessionRendererResources rendererResources)
            : base(ownerCore, rendererResources?.InteractionServices ?? throw new ArgumentNullException(nameof(rendererResources)), font, metrics) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            SnapModifierFont = snapModifierFont ?? throw new ArgumentNullException(nameof(snapModifierFont));
            ToolbarIcons = toolbarIcons ?? throw new ArgumentNullException(nameof(toolbarIcons));
            SceneCanvasProfileStateValue = sceneCanvasProfileState ?? throw new ArgumentNullException(nameof(sceneCanvasProfileState));
            BuiltInShaderLibrary = builtInShaderLibrary ?? throw new ArgumentNullException(nameof(builtInShaderLibrary));
            RendererResources = rendererResources ?? throw new ArgumentNullException(nameof(rendererResources));
            CanvasPreviewSettingsValue = new EditorViewportCanvasPreviewSettings();
            Title = "Viewport";
            SetContentBackgroundColor(new byte4(0, 0, 0, 0));
            ToolbarInputBlockerOwner = new object();
            ContentFocusGroup = new EditorFocusGroup(this, 0, () => Enabled, ContainsViewportContentPoint, HandleSubviewGroupActiveChanged);
            ToolbarFocusGroup = new EditorFocusGroup(this, 1, () => Enabled, ContainsToolbarPoint, HandleSubviewGroupActiveChanged);
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterGroup(ContentFocusGroup);
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterGroup(ToolbarFocusGroup);
            ViewportContentFocusTarget = new EditorFocusTarget(
                ContentFocusGroup,
                0,
                true,
                () => Enabled,
                ContainsViewportContentPoint,
                HandleViewportContentFocusedChanged,
                CanActivateViewportContentKey,
                ActivateViewportContentKey);
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(ViewportContentFocusTarget);
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
            SnapValueTextBoxes = new TextBoxComponent[SnapSlots.Length];
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

            ToolbarRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.2f)
            };
            AddChild(ToolbarRoot);

            ToolbarBackground = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
            };
            ToolbarRoot.AddComponent(ToolbarBackground);

            InitializeToolButtons();
            InitializeSettingsButton();
            InitializeSettingsOverlay();
            InitializeStatsButton();
            InitializeStatsOverlay();
            InitializePlayButtons();
            InitializeSnapControls();
            CameraAngleOverlayComponentValue = new EditorViewportCameraAngleOverlayComponent(Camera, Font, ToolbarHeight, false, BuiltInShaderLibrary, RendererResources);
            AddComponent(CameraAngleOverlayComponentValue);
            ToolMode = EditorSessionInteractionServices.From(this).ViewportTool.GetToolMode(Camera);
            UpdateViewport();
        }

        /// <summary>
        /// Gets the camera used to render into this viewport.
        /// </summary>
        public CameraComponent Camera { get; private set; }
        /// <summary>
        /// Gets or sets the viewport-local camera controller that owns editor navigation state.
        /// </summary>
        public EditorViewportCameraController CameraController { get; set; }
        /// <summary>
        /// Gets the camera navigation controller associated with this viewport's cube overlay.
        /// </summary>
        public EditorViewportNavigationController NavigationController => NavigationControllerValue;
        /// <summary>
        /// Gets the camera-facing navigation cube overlay owned by this viewport.
        /// </summary>
        public EditorViewportNavigationCube NavigationCube => NavigationCubeValue;
        /// <summary>
        /// Gets the shared scene-owned canvas profile state used by viewport previews.
        /// </summary>
        public EditorSceneCanvasProfileState SceneCanvasProfileState => SceneCanvasProfileStateValue;
        /// <summary>
        /// Gets or sets the callback invoked when the viewport content requests selection framing.
        /// </summary>
        public Action FocusSelectionRequested { get; set; }
        /// <summary>
        /// Gets the viewport-local simulated canvas settings used by the world-space 2D preview plane.
        /// </summary>
        public EditorViewportCanvasPreviewSettings CanvasPreviewSettings => CanvasPreviewSettingsValue;
        /// <summary>
        /// Gets or sets how this viewport derives effective camera speed.
        /// </summary>
        public byte CameraSpeedMode {
            get {
                if (CameraController == null) {
                    return EditorViewportCameraSpeedMode.AutoFromSelection;
                }

                return CameraController.SpeedMode;
            }
            set {
                if (CameraController == null) {
                    throw new InvalidOperationException("Viewport camera speed mode cannot be assigned before a camera controller is attached.");
                }

                CameraController.SpeedMode = value;
            }
        }
        /// <summary>
        /// Gets or sets the viewport-local authored manual movement speed override.
        /// </summary>
        public double ManualCameraSpeedOverride {
            get {
                if (CameraController == null) {
                    return EditorViewportCameraController.DefaultMoveSpeed;
                }

                return CameraController.ManualSpeedOverride;
            }
            set {
                if (CameraController == null) {
                    throw new InvalidOperationException("Viewport manual camera speed cannot be assigned before a camera controller is attached.");
                }

                CameraController.ManualSpeedOverride = value;
            }
        }
        /// <summary>
        /// Returns the world-space scene-gizmo entities owned by this viewport, such as axis-label billboards.
        /// </summary>
        /// <returns>Viewport-owned world-space scene-gizmo entities.</returns>
        public IReadOnlyList<EditorEntity> GetOwnedSceneGizmoEntities() {
            if (CameraAngleOverlayComponentValue == null) {
                return Array.Empty<EditorEntity>();
            }

            return CameraAngleOverlayComponentValue.GetAxisLabelEntities();
        }
        /// <summary>
        /// Gets or sets the active gizmo tool mode for this viewport.
        /// </summary>
        public EditorViewportToolMode ToolMode {
            get => EditorSessionInteractionServices.From(this).ViewportTool.GetToolMode(Camera);
            set => SetToolMode(value);
        }
        /// <summary>
        /// Gets whether the viewport settings overlay is currently open.
        /// </summary>
        public bool IsSettingsOverlayVisible => IsSettingsOverlayOpen();

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
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            base.ApplyUiMetrics(font, metrics);
            Font = font ?? throw new ArgumentNullException(nameof(font));
            NavigationCubeValue?.ApplyUiMetrics(font, metrics);
        }

        /// <summary>
        /// Reapplies scaled dock metrics and viewport-toolbar fonts after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title and toolbar value font.</param>
        /// <param name="snapModifierFont">Updated snap modifier font used by CTRL and SHIFT labels.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public void ApplyUiMetrics(FontAsset font, FontAsset snapModifierFont, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (snapModifierFont == null) {
                throw new ArgumentNullException(nameof(snapModifierFont));
            }

            Font = font;
            SnapModifierFont = snapModifierFont;
            base.ApplyUiMetrics(font, metrics);
            UpdateToolbarTextFonts();
            LayoutToolbar();
            NavigationCubeValue?.ApplyUiMetrics(font, metrics);
        }

        /// <summary>
        /// Installs the viewport-local navigation controller and creates its UI overlay updater.
        /// </summary>
        /// <param name="navigationController">Controller that owns this viewport's camera transitions.</param>
        public void AttachNavigationCube(EditorViewportNavigationController navigationController) {
            if (navigationController == null) {
                throw new ArgumentNullException(nameof(navigationController));
            }
            if (NavigationCubeUpdateComponentValue != null) {
                throw new InvalidOperationException("A navigation cube is already attached to this viewport.");
            }
            if (CameraController == null || !ReferenceEquals(navigationController.ViewportCameraController, CameraController)) {
                throw new InvalidOperationException("The navigation controller must own this viewport's camera controller.");
            }

            NavigationControllerValue = navigationController;
            NavigationCubeValue = new EditorViewportNavigationCube(
                this,
                Camera,
                navigationController,
                RendererResources.RenderManager2D,
                RendererResources.Input,
                Font,
                UiMetrics,
                EditorSessionInteractionServices.From(this),
                RendererResources.FrameDeltaSecondsProvider,
                BuiltInShaderLibrary);
            NavigationCubeUpdateComponentValue = new EditorViewportNavigationCubeUpdateComponent(NavigationCubeValue);
            NavigationCubeUpdateComponentValue.UpdateOrder = 0;
            AddComponent(NavigationCubeUpdateComponentValue);
            UpdateViewport();
        }

        /// <summary>
        /// Removes and disposes the navigation overlay when the workspace closes its viewport stack.
        /// </summary>
        public void DetachNavigationCube() {
            if (NavigationCubeUpdateComponentValue == null) {
                return;
            }

            if (Components.Contains(NavigationCubeUpdateComponentValue)) {
                RemoveComponent(NavigationCubeUpdateComponentValue);
            }
            NavigationCubeUpdateComponentValue = null;
            NavigationCubeValue = null;
            NavigationControllerValue = null;
        }

        /// <summary>
        /// Refreshes viewport-local layout after the shared dock title-bar metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            UpdateViewport();
        }

        /// <summary>
        /// Clears input blockers owned by this viewport toolbar.
        /// </summary>
        public void ClearInputBlockers() {
            if (ToolbarInputBlockerOwner == null) {
                return;
            }

            EditorSessionInteractionServices.From(this).InputCapture.ClearBlocker(ToolbarInputBlockerOwner);
        }

        /// <summary>
        /// Refreshes input blockers owned by this viewport.
        /// </summary>
        public void RefreshInputBlockers() {
            if (!Enabled) {
                ClearInputBlockers();
                if (NavigationCubeValue != null) {
                    NavigationCubeValue.CancelInteraction();
                    NavigationCubeValue.Resize(new int2(0, 0), (float)UiMetrics.Scale);
                }
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
        /// Initializes the viewport settings button.
        /// </summary>
        void InitializeSettingsButton() {
            EditorEntity buttonRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = ToolbarIcons.SettingsIcon,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(ToolButtonIconSize, ToolButtonIconSize),
            };
            iconHost.AddComponent(buttonIcon);

            InteractableComponent buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            buttonInteractable.CursorEvent += (pos, delta, state) => HandleSettingsButtonCursor(state);
            buttonRoot.AddComponent(buttonInteractable);

            SettingsButtonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                ToolModes.Length,
                false,
                () => Enabled && buttonRoot.Enabled,
                ContainsSettingsButtonPoint,
                isFocused => {
                    SettingsButtonKeyboardFocusState = isFocused;
                    UpdateSettingsButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ToggleSettingsOverlay());
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(SettingsButtonFocusTarget);

            SettingsButtonRoot = buttonRoot;
            SettingsButtonBackground = buttonBackground;
            SettingsButtonIcon = buttonIcon;
            SettingsButtonInteractable = buttonInteractable;
            SettingsButtonHoverState = false;
            SettingsButtonPressedState = false;
            SettingsButtonKeyboardFocusState = false;
            UpdateSettingsButtonVisuals();
        }

        /// <summary>
        /// Initializes the viewport settings overlay component and wires it to the toolbar settings button.
        /// </summary>
        void InitializeSettingsOverlay() {
            SettingsOverlayComponent = new EditorViewportSettingsOverlayComponent(
                Camera,
                Font,
                LayerMask,
                CanvasPreviewSettingsValue,
                SetGridVisible,
                IsGridVisible);
            SettingsOverlayComponent.SetInput(RendererResources.Input);
            SettingsOverlayComponent.OpenStateChanged += HandleSettingsOverlayOpenStateChanged;
            AddComponent(SettingsOverlayComponent);
            SettingsOverlayComponent.SetSettingsButtonFocusTarget(SettingsButtonFocusTarget);
        }

        /// <summary>
        /// Initializes the viewport stats toggle button on the toolbar.
        /// </summary>
        void InitializeStatsButton() {
            EditorEntity buttonRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = ToolbarIcons.StatsIcon,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(ToolButtonIconSize, ToolButtonIconSize),
            };
            iconHost.AddComponent(buttonIcon);

            InteractableComponent buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            buttonInteractable.CursorEvent += (pos, delta, state) => HandleStatsButtonCursor(state);
            buttonRoot.AddComponent(buttonInteractable);

            StatsButtonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                ToolModes.Length + 1,
                false,
                () => Enabled && buttonRoot.Enabled,
                ContainsStatsButtonPoint,
                isFocused => {
                    StatsButtonKeyboardFocusState = isFocused;
                    UpdateStatsButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ToggleStatsOverlay());
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(StatsButtonFocusTarget);

            StatsButtonRoot = buttonRoot;
            StatsButtonBackground = buttonBackground;
            StatsButtonIcon = buttonIcon;
            StatsButtonInteractable = buttonInteractable;
            StatsButtonHoverState = false;
            StatsButtonPressedState = false;
            StatsButtonKeyboardFocusState = false;
            UpdateStatsButtonVisuals();
        }

        /// <summary>
        /// Initializes the viewport stats overlay component toggled by the stats button.
        /// </summary>
        void InitializeStatsOverlay() {
            StatsOverlayComponent = new EditorViewportStatsOverlayComponent(Camera, Font, ToolbarHeight, RendererResources.ObjectManager, RendererResources.FrameDeltaSecondsProvider);
            AddComponent(StatsOverlayComponent);
        }

        /// <summary>
        /// Handles pointer interaction state updates for the stats toggle button.
        /// </summary>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleStatsButtonCursor(PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    StatsButtonHoverState = true;
                    break;
                case PointerInteraction.Press:
                    StatsButtonPressedState = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldToggle = StatsButtonPressedState && StatsButtonHoverState;
                    StatsButtonPressedState = false;
                    if (shouldToggle) {
                        ToggleStatsOverlay();
                    }
                    break;
                case PointerInteraction.Leave:
                    StatsButtonHoverState = false;
                    StatsButtonPressedState = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateStatsButtonVisuals();
        }

        /// <summary>
        /// Toggles the viewport stats box visibility.
        /// </summary>
        void ToggleStatsOverlay() {
            if (StatsOverlayComponent == null) {
                return;
            }

            StatsOverlayComponent.SetVisible(!StatsOverlayComponent.IsVisible);
            UpdateStatsButtonVisuals();
        }

        /// <summary>
        /// Applies the stats button colors from its toggle, hover, pressed, and focus states.
        /// </summary>
        void UpdateStatsButtonVisuals() {
            if (StatsButtonBackground == null || StatsButtonIcon == null) {
                return;
            }

            bool statsVisible = StatsOverlayComponent != null && StatsOverlayComponent.IsVisible;
            if (StatsButtonPressedState) {
                StatsButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (statsVisible) {
                StatsButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            } else if (StatsButtonKeyboardFocusState || StatsButtonHoverState) {
                StatsButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                StatsButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            }

            if (statsVisible || StatsButtonHoverState || StatsButtonPressedState || StatsButtonKeyboardFocusState) {
                StatsButtonIcon.Color = new byte4(255, 255, 255, 255);
            } else {
                StatsButtonIcon.Color = new byte4(255, 255, 255, 224);
            }
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the viewport stats button.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the stats button bounds.</returns>
        bool ContainsStatsButtonPoint(int2 point) {
            if (StatsButtonRoot == null || StatsButtonInteractable == null) {
                return false;
            }

            int left = (int)Math.Round(StatsButtonRoot.Position.X);
            int top = (int)Math.Round(StatsButtonRoot.Position.Y);
            int width = StatsButtonInteractable.Size.X;
            int height = StatsButtonInteractable.Size.Y;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Presents the Play session phase on the Play and Stop buttons. Play is dimmed and inert while the selected Play
        /// platform cannot be played; Stop is highlighted while a session is preparing or running and dimmed otherwise.
        /// The toolbar has no tooltip system, so availability is conveyed by dimming alone.
        /// </summary>
        /// <param name="state">Current Play session phase.</param>
        /// <param name="playAvailable">Whether the platform chosen in the Play platform selector supports Play.</param>
        public void SetPlayState(EditorPlaySessionState state, bool playAvailable) {
            PresentedPlayState = state;
            PresentedPlayAvailable = playAvailable;
            if (!playAvailable) {
                PlayButtonPressedState = false;
            }
            if (state == EditorPlaySessionState.Stopped) {
                StopButtonPressedState = false;
            }

            UpdatePlayButtonVisuals();
            UpdateStopButtonVisuals();
        }

        /// <summary>
        /// Gets the Play session phase currently presented by the Play and Stop buttons.
        /// </summary>
        public EditorPlaySessionState PresentedPlaySessionState => PresentedPlayState;

        /// <summary>
        /// Gets whether the Play button currently presents the active platform as playable.
        /// </summary>
        public bool IsPlayButtonAvailable => PresentedPlayAvailable;

        /// <summary>
        /// Initializes the Play and Stop buttons. Both start in the stopped, unavailable presentation until the session
        /// reports the active platform's Play capability through <see cref="SetPlayState"/>.
        /// </summary>
        void InitializePlayButtons() {
            PresentedPlayState = EditorPlaySessionState.Stopped;
            PresentedPlayAvailable = false;
            int firstTabIndex = ToolModes.Length + 1 + (SnapSlots.Length * 2);

            PlayButtonRoot = CreateToolbarActionButton(
                ToolbarIcons.PlayIcon,
                HandlePlayButtonCursor,
                out SpriteComponent playBackground,
                out SpriteComponent playIcon,
                out InteractableComponent playInteractable);
            PlayButtonBackground = playBackground;
            PlayButtonIcon = playIcon;
            PlayButtonInteractable = playInteractable;
            EditorEntity playButtonRoot = PlayButtonRoot;
            PlayButtonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                firstTabIndex,
                false,
                () => Enabled && playButtonRoot.Enabled,
                point => ContainsToolbarActionButtonPoint(PlayButtonRoot, PlayButtonInteractable, point),
                isFocused => {
                    PlayButtonKeyboardFocusState = isFocused;
                    UpdatePlayButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ActivatePlayButton());
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(PlayButtonFocusTarget);

            InitializePlayPlatformSelector(firstTabIndex + 1);

            StopButtonRoot = CreateToolbarActionButton(
                ToolbarIcons.StopIcon,
                HandleStopButtonCursor,
                out SpriteComponent stopBackground,
                out SpriteComponent stopIcon,
                out InteractableComponent stopInteractable);
            StopButtonBackground = stopBackground;
            StopButtonIcon = stopIcon;
            StopButtonInteractable = stopInteractable;
            EditorEntity stopButtonRoot = StopButtonRoot;
            StopButtonFocusTarget = new EditorFocusTarget(
                ToolbarFocusGroup,
                firstTabIndex + 2,
                false,
                () => Enabled && stopButtonRoot.Enabled,
                point => ContainsToolbarActionButtonPoint(StopButtonRoot, StopButtonInteractable, point),
                isFocused => {
                    StopButtonKeyboardFocusState = isFocused;
                    UpdateStopButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ActivateStopButton());
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(StopButtonFocusTarget);

            PlayButtonHoverState = false;
            PlayButtonPressedState = false;
            PlayButtonKeyboardFocusState = false;
            StopButtonHoverState = false;
            StopButtonPressedState = false;
            StopButtonKeyboardFocusState = false;
            UpdatePlayButtonVisuals();
            UpdateStopButtonVisuals();
        }

        /// <summary>
        /// Creates one toolbar button that runs an action instead of selecting a tool mode, using the same background,
        /// icon and interactable layering as the tool buttons.
        /// </summary>
        /// <param name="iconTexture">Glyph drawn inside the button; when null the button has no icon sprite.</param>
        /// <param name="cursorHandler">Receives the button's pointer interaction states.</param>
        /// <param name="buttonBackground">Receives the background sprite.</param>
        /// <param name="buttonIcon">Receives the icon sprite, or null when no glyph texture was supplied.</param>
        /// <param name="buttonInteractable">Receives the interactable region.</param>
        /// <returns>Root entity of the new button, parented to the toolbar.</returns>
        EditorEntity CreateToolbarActionButton(
            RuntimeTexture iconTexture,
            Action<PointerInteraction> cursorHandler,
            out SpriteComponent buttonBackground,
            out SpriteComponent buttonIcon,
            out InteractableComponent buttonInteractable) {
            if (cursorHandler == null) {
                throw new ArgumentNullException(nameof(cursorHandler));
            }

            EditorEntity buttonRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            buttonBackground = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
            };
            buttonRoot.AddComponent(buttonBackground);

            if (iconTexture == null) {
                buttonIcon = null;
            } else {
                EditorEntity iconHost = new EditorEntity(OwnerCore, InteractionServices) {
                    LayerMask = LayerMask,
                    Position = new float3(0f, 0f, 0.1f)
                };
                buttonRoot.AddChild(iconHost);
                buttonIcon = new SpriteComponent {
                    Texture = iconTexture,
                    Color = new byte4(255, 255, 255, 224),
                    Size = new int2(ToolButtonIconSize, ToolButtonIconSize),
                };
                iconHost.AddComponent(buttonIcon);
            }

            buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            buttonInteractable.CursorEvent += (pos, delta, state) => cursorHandler(state);
            buttonRoot.AddComponent(buttonInteractable);
            return buttonRoot;
        }

        /// <summary>
        /// Creates the Play platform selector that sits between the Play and Stop buttons. It starts empty and inert
        /// until the session presents the playable platforms through <see cref="SetPlayPlatformOptions"/>.
        /// </summary>
        /// <param name="tabIndex">Keyboard traversal order of the selector inside the toolbar focus group.</param>
        void InitializePlayPlatformSelector(int tabIndex) {
            PlayPlatformComboHost = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(PlayPlatformComboHost);

            PlayPlatformComboBox = new ComboBoxComponent(
                new int2(PlayPlatformSelectorWidth, ToolButtonHeight),
                Font,
                Array.Empty<string>(),
                -1) {
                FocusGroup = ToolbarFocusGroup,
                TabIndex = tabIndex
            };
            PlayPlatformComboBox.SelectionChanged += HandlePlayPlatformComboSelectionChanged;
            PlayPlatformComboHost.AddComponent(PlayPlatformComboBox);
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(PlayPlatformComboBox);
            PlayPlatformComboHost.Enabled = false;
        }

        /// <summary>
        /// Measures the selector width that fits the longest display name and the arrow glyph in the toolbar font,
        /// never narrower than <see cref="PlayPlatformSelectorMinimumWidth"/>.
        /// </summary>
        /// <param name="displayNames">Display names the selector offers.</param>
        /// <returns>Selector width in pixels.</returns>
        int MeasurePlayPlatformSelectorWidth(IReadOnlyList<string> displayNames) {
            double longestNameWidth = 0.0;
            for (int index = 0; index < displayNames.Count; index++) {
                longestNameWidth = Math.Max(longestNameWidth, Font.MeasureTight(displayNames[index]).Width);
            }

            double arrowWidth = Font.MeasureTight(PlayPlatformSelectorArrowGlyph).Width;
            double width = PlayPlatformSelectorEdgePadding + longestNameWidth + PlayPlatformSelectorArrowGap + arrowWidth + PlayPlatformSelectorEdgePadding;
            return Math.Max(PlayPlatformSelectorMinimumWidth, (int)Math.Ceiling(width));
        }

        /// <summary>
        /// Refits the selector to its current items and relays the toolbar when the width changed.
        /// </summary>
        void UpdatePlayPlatformSelectorWidth() {
            int width = MeasurePlayPlatformSelectorWidth(PlayPlatformComboBox.Items);
            if (width == PlayPlatformSelectorWidth) {
                return;
            }

            PlayPlatformSelectorWidth = width;
            PlayPlatformComboBox.Size = new int2(width, ToolButtonHeight);
            LayoutToolbar();
        }

        /// <summary>
        /// Presents the platforms the Play platform selector offers and the one currently selected. Presenting a
        /// selection never raises <see cref="PlayPlatformSelected"/>; only a user pick does. The selector is sized to the
        /// longest name; with no platforms it is disabled, which hides it and removes it from keyboard traversal.
        /// </summary>
        /// <param name="platformIds">Ids of the offered platforms, in display order.</param>
        /// <param name="displayNames">Display names index-aligned with <paramref name="platformIds"/>.</param>
        /// <param name="selectedPlatformId">Id of the selected platform; empty when no platform is offered.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the lists differ in length or the selection is not offered.</exception>
        public void SetPlayPlatformOptions(IReadOnlyList<string> platformIds, IReadOnlyList<string> displayNames, string selectedPlatformId) {
            if (platformIds == null) {
                throw new ArgumentNullException(nameof(platformIds));
            } else if (displayNames == null) {
                throw new ArgumentNullException(nameof(displayNames));
            } else if (selectedPlatformId == null) {
                throw new ArgumentNullException(nameof(selectedPlatformId));
            } else if (platformIds.Count != displayNames.Count) {
                throw new ArgumentException("Every Play platform id needs exactly one display name.", nameof(displayNames));
            }

            int selectedIndex = -1;
            for (int index = 0; index < platformIds.Count; index++) {
                if (string.Equals(platformIds[index], selectedPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    selectedIndex = index;
                }
            }
            if (selectedIndex < 0 && (platformIds.Count > 0 || selectedPlatformId.Length > 0)) {
                throw new ArgumentException($"Selected Play platform '{selectedPlatformId}' is not among the offered platforms.", nameof(selectedPlatformId));
            }

            PresentedPlayPlatformIds.Clear();
            PresentedPlayPlatformIds.AddRange(platformIds);
            PresentedPlayPlatformId = selectedPlatformId;
            PlayPlatformComboBox.SetItems(displayNames, selectedIndex);
            PlayPlatformComboHost.Enabled = platformIds.Count > 0;
            UpdatePlayPlatformSelectorWidth();
        }

        /// <summary>
        /// Raises <see cref="PlayPlatformSelected"/> when the selector's item changes to a platform other than the one
        /// already presented, which filters out the change events caused by <see cref="SetPlayPlatformOptions"/>.
        /// </summary>
        /// <param name="selectedIndex">Index of the newly selected item.</param>
        /// <param name="selectedDisplayName">Display name of the newly selected item.</param>
        void HandlePlayPlatformComboSelectionChanged(int selectedIndex, string selectedDisplayName) {
            string platformId = PresentedPlayPlatformIds[selectedIndex];
            if (string.Equals(platformId, PresentedPlayPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            PresentedPlayPlatformId = platformId;
            PlayPlatformSelected?.Invoke(platformId);
        }

        /// <summary>
        /// Handles pointer interaction state updates for the Play button; a completed click requests Play.
        /// </summary>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandlePlayButtonCursor(PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    PlayButtonHoverState = true;
                    break;
                case PointerInteraction.Press:
                    PlayButtonPressedState = PresentedPlayAvailable;
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = PlayButtonPressedState && PlayButtonHoverState;
                    PlayButtonPressedState = false;
                    if (shouldActivate) {
                        ActivatePlayButton();
                    }
                    break;
                case PointerInteraction.Leave:
                    PlayButtonHoverState = false;
                    PlayButtonPressedState = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdatePlayButtonVisuals();
        }

        /// <summary>
        /// Handles pointer interaction state updates for the Stop button; a completed click requests Stop.
        /// </summary>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleStopButtonCursor(PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    StopButtonHoverState = true;
                    break;
                case PointerInteraction.Press:
                    StopButtonPressedState = PresentedPlayState != EditorPlaySessionState.Stopped;
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = StopButtonPressedState && StopButtonHoverState;
                    StopButtonPressedState = false;
                    if (shouldActivate) {
                        ActivateStopButton();
                    }
                    break;
                case PointerInteraction.Leave:
                    StopButtonHoverState = false;
                    StopButtonPressedState = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateStopButtonVisuals();
        }

        /// <summary>
        /// Raises <see cref="PlayRequested"/> when the selected Play platform supports Play; otherwise the click does nothing.
        /// Holding either Shift key during the activation requests a launch that waits for a debugger.
        /// </summary>
        void ActivatePlayButton() {
            if (!PresentedPlayAvailable) {
                return;
            }

            InputSystem input = RendererResources.Input;
            bool waitForDebugger = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            PlayRequested?.Invoke(waitForDebugger);
        }

        /// <summary>
        /// Raises <see cref="StopRequested"/> while a Play session is preparing or running; otherwise does nothing.
        /// </summary>
        void ActivateStopButton() {
            if (PresentedPlayState == EditorPlaySessionState.Stopped) {
                return;
            }

            StopRequested?.Invoke();
        }

        /// <summary>
        /// Applies the Play button colors: dimmed when unavailable, accented while a session is active, and the usual
        /// hover, focus and press feedback otherwise.
        /// </summary>
        void UpdatePlayButtonVisuals() {
            if (PlayButtonBackground == null) {
                return;
            }

            bool sessionActive = PresentedPlayState != EditorPlaySessionState.Stopped;
            bool highlighted = PlayButtonHoverState || PlayButtonKeyboardFocusState;
            if (!PresentedPlayAvailable) {
                PlayButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            } else if (PlayButtonPressedState) {
                PlayButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (sessionActive) {
                PlayButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            } else if (highlighted) {
                PlayButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                PlayButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            }

            if (PlayButtonIcon == null) {
                return;
            }

            if (!PresentedPlayAvailable) {
                PlayButtonIcon.Color = new byte4(255, 255, 255, 72);
            } else if (sessionActive || highlighted || PlayButtonPressedState) {
                PlayButtonIcon.Color = new byte4(255, 255, 255, 255);
            } else {
                PlayButtonIcon.Color = new byte4(255, 255, 255, 224);
            }
        }

        /// <summary>
        /// Applies the Stop button colors: highlighted while a session is preparing or running, dimmed when stopped.
        /// </summary>
        void UpdateStopButtonVisuals() {
            if (StopButtonBackground == null) {
                return;
            }

            bool sessionActive = PresentedPlayState != EditorPlaySessionState.Stopped;
            if (!sessionActive) {
                StopButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            } else if (StopButtonPressedState) {
                StopButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (StopButtonHoverState || StopButtonKeyboardFocusState) {
                StopButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                StopButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            }

            if (StopButtonIcon == null) {
                return;
            }

            if (sessionActive) {
                StopButtonIcon.Color = new byte4(255, 255, 255, 255);
            } else {
                StopButtonIcon.Color = new byte4(255, 255, 255, 72);
            }
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside one toolbar action button.
        /// </summary>
        /// <param name="buttonRoot">Root entity of the button.</param>
        /// <param name="buttonInteractable">Interactable region of the button.</param>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the button bounds.</returns>
        static bool ContainsToolbarActionButtonPoint(EditorEntity buttonRoot, InteractableComponent buttonInteractable, int2 point) {
            if (buttonRoot == null || buttonInteractable == null) {
                return false;
            }

            int left = (int)Math.Round(buttonRoot.Position.X);
            int top = (int)Math.Round(buttonRoot.Position.Y);
            return point.X >= left &&
                   point.X < left + buttonInteractable.Size.X &&
                   point.Y >= top &&
                   point.Y < top + buttonInteractable.Size.Y;
        }

        /// <summary>
        /// Lays out the Play button, the Play platform selector and the Stop button, in that order, to the left of the
        /// stats button and separated from it by a group gap.
        /// </summary>
        /// <param name="statsButtonX">Left edge of the stats button inside the toolbar.</param>
        /// <param name="buttonY">Top offset shared by every toolbar button.</param>
        void LayoutPlayButtons(float statsButtonX, float buttonY) {
            if (PlayButtonRoot == null || StopButtonRoot == null || PlayPlatformComboHost == null) {
                return;
            }

            float stopButtonX = Math.Max(ToolbarPadding, statsButtonX - SnapGroupSpacing - ToolButtonWidth);
            float selectorX = Math.Max(ToolbarPadding, stopButtonX - ToolbarButtonSpacing - PlayPlatformSelectorWidth);
            float playButtonX = Math.Max(ToolbarPadding, selectorX - ToolbarButtonSpacing - ToolButtonWidth);
            LayoutToolbarActionButton(PlayButtonRoot, PlayButtonBackground, PlayButtonIcon, PlayButtonInteractable, playButtonX, buttonY);
            PlayPlatformComboHost.Position = new float3(selectorX, buttonY, 0.1f);
            LayoutToolbarActionButton(StopButtonRoot, StopButtonBackground, StopButtonIcon, StopButtonInteractable, stopButtonX, buttonY);
        }

        /// <summary>
        /// Positions and sizes one toolbar action button and centers its glyph.
        /// </summary>
        /// <param name="buttonRoot">Root entity of the button.</param>
        /// <param name="buttonBackground">Background sprite of the button.</param>
        /// <param name="buttonIcon">Icon sprite of the button, or null when it has no glyph.</param>
        /// <param name="buttonInteractable">Interactable region of the button.</param>
        /// <param name="buttonX">Left edge inside the toolbar.</param>
        /// <param name="buttonY">Top edge inside the toolbar.</param>
        void LayoutToolbarActionButton(EditorEntity buttonRoot, SpriteComponent buttonBackground, SpriteComponent buttonIcon, InteractableComponent buttonInteractable, float buttonX, float buttonY) {
            buttonRoot.Position = new float3(buttonX, buttonY, 0.1f);
            buttonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
            buttonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
            if (buttonIcon != null) {
                LayoutToolButtonIcon(buttonIcon, ToolButtonWidth, ToolButtonHeight, ToolButtonIconSize, ToolButtonIconSize);
            }
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

            EditorEntity buttonRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = iconTexture,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(ToolButtonIconSize, ToolButtonIconSize),
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
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(buttonFocusTarget);

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
        /// Creates one snap-control label, editable value field, and up/down button pair.
        /// </summary>
        /// <param name="slotIndex">Snap slot index inside toolbar arrays.</param>
        /// <param name="snapSlot">Snap slot represented by the control group.</param>
        void CreateSnapControlGroup(int slotIndex, TransformGizmoSnapSlot snapSlot) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }

            EditorEntity labelRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(labelRoot);

            EditorEntity magnetIconRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            labelRoot.AddChild(magnetIconRoot);

            SpriteComponent magnetIcon = new SpriteComponent {
                Texture = ToolbarIcons.MagnetIcon,
                Color = new byte4(255, 255, 255, 255),
                Size = new int2(SnapLabelIconHeight, SnapLabelIconHeight),
            };
            magnetIconRoot.AddComponent(magnetIcon);

            EditorEntity modifierTextRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            labelRoot.AddChild(modifierTextRoot);

            TextComponent modifierText = new TextComponent {
                Font = SnapModifierFont,
                Text = GetSnapModifierLabel(snapSlot),
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
            };
            modifierTextRoot.AddComponent(modifierText);

            EditorEntity valueRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(valueRoot);

            TextBoxComponent valueTextBox = new TextBoxComponent(new int2(SnapValueWidth, ToolButtonHeight), Font);
            valueTextBox.UseFocusedBorderOnly = true;
            int capturedSlotIndex = slotIndex;
            valueTextBox.Submitted += textBox => HandleSnapValueSubmitted(capturedSlotIndex, textBox);
            valueRoot.AddComponent(valueTextBox);

            SnapSlots[slotIndex] = snapSlot;
            SnapLabelRoots[slotIndex] = labelRoot;
            SnapLabelMagnetIcons[slotIndex] = magnetIcon;
            SnapLabelModifierTexts[slotIndex] = modifierText;
            SnapValueRoots[slotIndex] = valueRoot;
            SnapValueTextBoxes[slotIndex] = valueTextBox;

            CreateSnapToolbarButton(slotIndex, true, ToolbarIcons.GetSnapButtonIcon(true));
            CreateSnapToolbarButton(slotIndex, false, ToolbarIcons.GetSnapButtonIcon(false));
        }

        /// <summary>
        /// Reassigns the current scaled toolbar fonts to the existing snap labels and editable value fields.
        /// </summary>
        void UpdateToolbarTextFonts() {
            for (int slotIndex = 0; slotIndex < SnapSlots.Length; slotIndex++) {
                TextComponent modifierText = SnapLabelModifierTexts[slotIndex];
                if (modifierText != null) {
                    modifierText.Font = SnapModifierFont;
                }

                TextBoxComponent valueTextBox = SnapValueTextBoxes[slotIndex];
                if (valueTextBox != null) {
                    valueTextBox.Font = Font;
                }
            }

            if (PlayPlatformComboBox != null) {
                PlayPlatformComboBox.Font = Font;
                UpdatePlayPlatformSelectorWidth();
            }
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

            EditorEntity buttonRoot = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(buttonRoot);

            SpriteComponent buttonBackground = new SpriteComponent {
                Texture = OwnerCore.RenderManager2D.PixelTexture,
                Color = ThemeManager.Colors.SurfaceInput,
            };
            buttonRoot.AddComponent(buttonBackground);

            EditorEntity iconHost = new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(iconHost);

            SpriteComponent buttonIcon = new SpriteComponent {
                Texture = iconTexture,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(SnapButtonIconWidth, SnapButtonIconHeight),
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
            EditorSessionInteractionServices.From(this).KeyboardFocus.RegisterTarget(buttonFocusTarget);

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
        /// Handles pointer interaction state updates for the viewport settings button.
        /// </summary>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleSettingsButtonCursor(PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    SettingsButtonHoverState = true;
                    break;
                case PointerInteraction.Press:
                    SettingsButtonPressedState = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldToggle = SettingsButtonPressedState && SettingsButtonHoverState;
                    SettingsButtonPressedState = false;
                    if (shouldToggle) {
                        ToggleSettingsOverlay();
                    }
                    break;
                case PointerInteraction.Leave:
                    SettingsButtonHoverState = false;
                    SettingsButtonPressedState = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateSettingsButtonVisuals();
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
        /// Applies visual state to the viewport settings button.
        /// </summary>
        void UpdateSettingsButtonVisuals() {
            if (SettingsButtonBackground == null || SettingsButtonIcon == null) {
                return;
            }

            if (SettingsButtonPressedState) {
                SettingsButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (IsSettingsOverlayOpen()) {
                SettingsButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            } else if (SettingsButtonKeyboardFocusState || SettingsButtonHoverState) {
                SettingsButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                SettingsButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            }

            if (IsSettingsOverlayOpen() || SettingsButtonHoverState || SettingsButtonPressedState || SettingsButtonKeyboardFocusState) {
                SettingsButtonIcon.Color = new byte4(255, 255, 255, 255);
            } else {
                SettingsButtonIcon.Color = new byte4(255, 255, 255, 224);
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
        /// Commits one typed snap value to the active tool mode and selected snap slot.
        /// </summary>
        /// <param name="slotIndex">Snap slot index represented by the submitting text box.</param>
        /// <param name="textBox">Text box that submitted the authored snap value.</param>
        void HandleSnapValueSubmitted(int slotIndex, TextBoxComponent textBox) {
            if (slotIndex < 0 || slotIndex >= SnapSlots.Length) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Snap slot index must be inside toolbar bounds.");
            }
            if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }
            if (!TryParseFiniteSnapValue(textBox.Text, out double value)) {
                UpdateSnapControlTexts();
                return;
            }

            EditorSessionInteractionServices.From(this).TransformSnap.SetSnapValue(ToolMode, SnapSlots[slotIndex], value);
            UpdateSnapControlTexts();
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
                EditorSessionInteractionServices.From(this).TransformSnap.IncreaseSnapValue(ToolMode, snapSlot);
            } else {
                EditorSessionInteractionServices.From(this).TransformSnap.DecreaseSnapValue(ToolMode, snapSlot);
            }

            UpdateSnapControlTexts();
        }

        /// <summary>
        /// Refreshes editable snap values to match the current tool mode configuration.
        /// </summary>
        void UpdateSnapControlTexts() {
            for (int slotIndex = 0; slotIndex < SnapSlots.Length; slotIndex++) {
                TextBoxComponent valueTextBox = SnapValueTextBoxes[slotIndex];
                if (valueTextBox == null) {
                    continue;
                }

                double snapValue = EditorSessionInteractionServices.From(this).TransformSnap.GetSnapValue(ToolMode, SnapSlots[slotIndex]);
                valueTextBox.Text = FormatSnapValue(snapValue);
            }

            LayoutToolbar();
        }

        /// <summary>
        /// Applies a tool mode selection for this viewport camera.
        /// </summary>
        /// <param name="toolMode">Tool mode to assign.</param>
        void SetToolMode(EditorViewportToolMode toolMode) {
            EditorSessionInteractionServices.From(this).ViewportTool.SetToolMode(Camera, toolMode);
            if (toolMode != EditorViewportToolMode.Translate) {
                EditorSessionInteractionServices.From(this).GizmoHover.ClearHoveredHandle(Camera);
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
            UpdateSettingsButtonVisuals();
            UpdateSnapButtonVisuals();
        }

        /// <summary>
        /// Tracks whether the viewport content target currently owns keyboard focus.
        /// </summary>
        /// <param name="isFocused">True when the content target is focused.</param>
        void HandleViewportContentFocusedChanged(bool isFocused) {
            IsViewportContentFocused = isFocused;
            ViewportContentFocusedChanged?.Invoke(this, isFocused);
        }

        /// <summary>
        /// Returns true when the viewport content target should react to one activation key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when the content target should activate for the key.</returns>
        bool CanActivateViewportContentKey(Keys key) {
            InputSystem inputManager = RendererResources.Input;
            if (inputManager != null && inputManager.GetMouseRightButtonState() == ButtonState.Pressed) {
                return false;
            }

            return key == Keys.W || key == Keys.R || key == Keys.S || key == Keys.F;
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
            } else if (key == Keys.F) {
                FocusSelectionRequested?.Invoke();
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
            int left = (int)Math.Round(ToolbarRoot.Position.X);
            int top = (int)Math.Round(ToolbarRoot.Position.Y);
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

            int left = (int)Math.Round(buttonRoot.Position.X);
            int top = (int)Math.Round(buttonRoot.Position.Y);
            int width = buttonInteractable.Size.X;
            int height = buttonInteractable.Size.Y;
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the viewport settings button.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the settings button bounds.</returns>
        bool ContainsSettingsButtonPoint(int2 point) {
            if (SettingsButtonRoot == null || SettingsButtonInteractable == null) {
                return false;
            }

            int left = (int)Math.Round(SettingsButtonRoot.Position.X);
            int top = (int)Math.Round(SettingsButtonRoot.Position.Y);
            int width = SettingsButtonInteractable.Size.X;
            int height = SettingsButtonInteractable.Size.Y;
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

            int left = (int)Math.Round(buttonRoot.Position.X);
            int top = (int)Math.Round(buttonRoot.Position.Y);
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
            float viewportTop = Position.Y + TitleBarHeightPixels + ToolbarHeight;
            Camera.Viewport = new float4(Position.X, viewportTop, viewportWidth, viewportHeight);
            LayoutToolbar();
            RefreshInputBlockers();
            if (NavigationCubeValue != null) {
                NavigationCubeValue.Resize(new int2((int)Math.Round(viewportWidth), (int)Math.Round(viewportHeight)), (float)UiMetrics.Scale);
            }
        }

        /// <summary>
        /// Lays out toolbar and tool-button positions based on current panel dimensions.
        /// </summary>
        void LayoutToolbar() {
            float toolbarX = 0f;
            float toolbarY = TitleBarHeightPixels;
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

            if (SettingsButtonRoot != null && SettingsButtonBackground != null && SettingsButtonIcon != null && SettingsButtonInteractable != null) {
                float settingsButtonX = Math.Max(
                    ToolbarPadding,
                    toolbarWidth - ToolbarPadding - ToolButtonWidth);
                SettingsButtonRoot.Position = new float3(settingsButtonX, buttonY, 0.1f);
                SettingsButtonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                SettingsButtonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                LayoutToolButtonIcon(SettingsButtonIcon, ToolButtonWidth, ToolButtonHeight, ToolButtonIconSize, ToolButtonIconSize);
                if (SettingsOverlayComponent != null) {
                    SettingsOverlayComponent.SetAnchorPosition(
                        ToolbarRoot.LocalPosition.X + settingsButtonX,
                        ToolbarRoot.LocalPosition.Y + buttonY + ToolButtonHeight,
                        ToolButtonWidth);
                }

                if (StatsButtonRoot != null && StatsButtonBackground != null && StatsButtonIcon != null && StatsButtonInteractable != null) {
                    float statsButtonX = Math.Max(
                        ToolbarPadding,
                        settingsButtonX - ToolbarButtonSpacing - ToolButtonWidth);
                    StatsButtonRoot.Position = new float3(statsButtonX, buttonY, 0.1f);
                    StatsButtonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                    StatsButtonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                    LayoutToolButtonIcon(StatsButtonIcon, ToolButtonWidth, ToolButtonHeight, ToolButtonIconSize, ToolButtonIconSize);
                    LayoutPlayButtons(statsButtonX, buttonY);
                }
            }

            if (StatsOverlayComponent != null) {
                StatsOverlayComponent.SetAnchorWidth(toolbarWidth);
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
            double currentX = ToolbarPadding + (ToolModes.Length * (ToolButtonWidth + ToolbarButtonSpacing));
            currentX -= ToolbarButtonSpacing;
            currentX += SnapGroupSpacing;

            for (int slotIndex = 0; slotIndex < SnapSlots.Length; slotIndex++) {
                EditorEntity labelRoot = SnapLabelRoots[slotIndex];
                SpriteComponent magnetIcon = SnapLabelMagnetIcons[slotIndex];
                TextComponent modifierText = SnapLabelModifierTexts[slotIndex];
                EditorEntity valueRoot = SnapValueRoots[slotIndex];
                TextBoxComponent valueTextBox = SnapValueTextBoxes[slotIndex];
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
                    valueTextBox == null ||
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
                valueTextBox.Size = new int2(SnapValueWidth, ToolButtonHeight);

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
        /// Parses one invariant-culture snap input and rejects non-finite values.
        /// </summary>
        /// <param name="text">Authored text to parse.</param>
        /// <param name="value">Parsed finite snap value when parsing succeeds.</param>
        /// <returns>True when the text represents one finite snap value.</returns>
        bool TryParseFiniteSnapValue(string text, out double value) {
            return double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value)
                && double.IsFinite(value);
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
        /// Toggles the viewport settings overlay shell state.
        /// </summary>
        void ToggleSettingsOverlay() {
            if (SettingsOverlayComponent == null) {
                return;
            }

            if (SettingsOverlayComponent.IsOpen) {
                SettingsOverlayComponent.Close(SettingsButtonFocusTarget);
            } else {
                SettingsOverlayComponent.Open();
            }
        }

        /// <summary>
        /// Returns true when the viewport settings overlay is currently open.
        /// </summary>
        /// <returns>True when the overlay is open.</returns>
        bool IsSettingsOverlayOpen() {
            return SettingsOverlayComponent != null && SettingsOverlayComponent.IsOpen;
        }

        /// <summary>
        /// Opens or closes the viewport settings overlay to match the requested state.
        /// </summary>
        /// <param name="isOpen">True to open the overlay; false to close it.</param>
        public void SetSettingsOverlayOpen(bool isOpen) {
            if (SettingsOverlayComponent == null) {
                return;
            }

            if (isOpen) {
                if (!SettingsOverlayComponent.IsOpen) {
                    SettingsOverlayComponent.Open();
                }

                return;
            }

            if (SettingsOverlayComponent.IsOpen) {
                SettingsOverlayComponent.Close(SettingsButtonFocusTarget);
            }
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

            int blockerX = (int)Math.Round(ToolbarRoot.Position.X);
            int blockerY = (int)Math.Round(ToolbarRoot.Position.Y);
            EditorSessionInteractionServices.From(this).InputCapture.SetBlocker(ToolbarInputBlockerOwner, new int2(blockerX, blockerY), blockerSize);
        }

        /// <summary>
        /// Refreshes settings-button visuals when the overlay open state changes.
        /// </summary>
        /// <param name="isOpen">True when the overlay became open; false when it closed.</param>
        void HandleSettingsOverlayOpenStateChanged(bool isOpen) {
            UpdateSettingsButtonVisuals();
        }
    }
}


