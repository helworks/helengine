using System;
using System.Collections.Generic;

namespace helengine.editor {
    /// <summary>
    /// Builds and manages the editor title bar UI while raising window and file-menu events for platform hosts.
    /// </summary>
    public class EditorTitleBar {
        /// <summary>
        /// Default title bar height in pixels.
        /// </summary>
        public const int HeightPixels = 27;

        /// <summary>
        /// Padding applied around the rendered editor icon.
        /// </summary>
        public const int IconPaddingPixels = 4;

        /// <summary>
        /// Size used for the rendered editor icon inside the left slot.
        /// </summary>
        public const int IconSizePixels = HeightPixels - (IconPaddingPixels * 2);

        /// <summary>
        /// Width reserved at the left edge for the editor icon.
        /// </summary>
        const int LeftIconSlotWidth = HeightPixels;
        /// <summary>
        /// Top offset used for title bar buttons.
        /// </summary>
        const int ButtonTop = 0;
        /// <summary>
        /// Height used for title bar buttons.
        /// </summary>
        const int ButtonHeight = HeightPixels;
        /// <summary>
        /// Horizontal spacing between title bar controls.
        /// </summary>
        const int ButtonSpacing = 0;
        /// <summary>
        /// Width of the vertical separator drawn on title bar buttons.
        /// </summary>
        const int ButtonBorderWidth = 1;
        /// <summary>
        /// Extra spacing applied between the File button and the title text.
        /// </summary>
        const int TitleSpacing = 10;
        /// <summary>
        /// Index of the Light entry inside the Add menu.
        /// </summary>
        const int AddMenuLightItemIndex = 4;
        /// <summary>
        /// Index of the Show entry inside the UI menu.
        /// </summary>
        const int UiMenuShowItemIndex = 0;
        /// <summary>
        /// Index of the Save entry inside the UI menu.
        /// </summary>
        const int UiMenuSaveItemIndex = 1;
        /// <summary>
        /// Index of the Load entry inside the UI menu.
        /// </summary>
        const int UiMenuLoadItemIndex = 2;
        /// <summary>
        /// Minimum width reserved for the title label.
        /// </summary>
        const int MinimumTitleWidth = 40;
        /// <summary>
        /// Maximum time between clicks to treat them as a title-bar double-click.
        /// </summary>
        const int TitleBarDoubleClickMs = 350;
        /// <summary>
        /// Maximum pointer movement between clicks to treat them as a title-bar double-click.
        /// </summary>
        const int TitleBarDoubleClickDistance = 6;
        /// <summary>
        /// Dedicated layer mask used by the title bar UI.
        /// </summary>
        const ushort TitleBarLayerMask = 0b1000000000000000;

        /// <summary>
        /// Font used to render the title bar labels.
        /// </summary>
        FontAsset Font;
        /// <summary>
        /// Shared scaled metrics used to size the title bar chrome.
        /// </summary>
        EditorUiMetrics Metrics;
        /// <summary>
        /// Root entity that owns all title bar visuals.
        /// </summary>
        readonly EditorEntity RootEntity;
        /// <summary>
        /// Background sprite that spans the title bar.
        /// </summary>
        readonly SpriteComponent Background;
        /// <summary>
        /// Entity that blocks hover and clicks from leaking through uncovered title-bar gaps.
        /// </summary>
        readonly EditorEntity HoverShieldEntity;
        /// <summary>
        /// Transparent surface used to put the hover shield at the top input layer.
        /// </summary>
        readonly SpriteComponent HoverShieldSurface;
        /// <summary>
        /// Interactable used to absorb pointer events over uncovered title-bar regions.
        /// </summary>
        readonly InteractableComponent HoverShieldInteractable;
        /// <summary>
        /// Entity that hosts the draggable title bar hit region.
        /// </summary>
        readonly EditorEntity DragRegionEntity;
        /// <summary>
        /// Interactable used for title bar drag and maximize gestures.
        /// </summary>
        readonly InteractableComponent DragRegion;
        /// <summary>
        /// Transparent surface that keeps the drag region above the hover shield in hit testing.
        /// </summary>
        readonly SpriteComponent DragRegionInputSurface;
        /// <summary>
        /// Entity that hosts the window title text.
        /// </summary>
        readonly EditorEntity TitleEntity;
        /// <summary>
        /// Text component that renders the current window title.
        /// </summary>
        readonly TextComponent TitleTextComponent;
        /// <summary>
        /// Entity that hosts the optional editor logo shown in the left title-bar slot.
        /// </summary>
        readonly EditorEntity IconEntity;
        /// <summary>
        /// Sprite component used to render the editor logo in the title bar.
        /// </summary>
        readonly SpriteComponent IconSprite;
        /// <summary>
        /// Entity that hosts the File menu trigger button.
        /// </summary>
        readonly EditorEntity FileMenuButtonEntity;
        /// <summary>
        /// Width reserved for the File menu trigger button.
        /// </summary>
        int FileMenuButtonWidth;
        /// <summary>
        /// Entity that hosts the Add menu trigger button.
        /// </summary>
        readonly EditorEntity AddMenuButtonEntity;
        /// <summary>
        /// Width reserved for the Add menu trigger button.
        /// </summary>
        int AddMenuButtonWidth;
        /// <summary>
        /// Entity that hosts the Build menu trigger button.
        /// </summary>
        readonly EditorEntity BuildMenuButtonEntity;
        /// <summary>
        /// Width reserved for the Build menu trigger button.
        /// </summary>
        int BuildMenuButtonWidth;
        /// <summary>
        /// Entity that hosts the UI menu trigger button.
        /// </summary>
        readonly EditorEntity UiMenuButtonEntity;
        /// <summary>
        /// Width reserved for the UI menu trigger button.
        /// </summary>
        int UiMenuButtonWidth;
        /// <summary>
        /// Context menu shown when the File button is activated.
        /// </summary>
        readonly ContextMenu FileMenu;
        /// <summary>
        /// Items displayed by the File context menu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> FileMenuItems;
        /// <summary>
        /// Context menu shown when the Add button is activated.
        /// </summary>
        readonly ContextMenu AddMenu;
        /// <summary>
        /// Items displayed by the Add context menu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> AddMenuItems;
        /// <summary>
        /// Context menu shown when the Light submenu is opened from the Add menu.
        /// </summary>
        readonly ContextMenu LightMenu;
        /// <summary>
        /// Items displayed by the Light submenu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> LightMenuItems;
        /// <summary>
        /// Context menu shown when the Build button is activated.
        /// </summary>
        readonly ContextMenu BuildMenu;
        /// <summary>
        /// Items displayed by the Build context menu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> BuildMenuItems;
        /// <summary>
        /// Context menu shown when the UI button is activated.
        /// </summary>
        readonly ContextMenu UiMenu;
        /// <summary>
        /// Items displayed by the UI context menu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> UiMenuItems;
        /// <summary>
        /// Context menu shown when the UI Show submenu is opened.
        /// </summary>
        readonly ContextMenu UiShowMenu;
        /// <summary>
        /// Items displayed by the UI Show submenu.
        /// </summary>
        IReadOnlyList<ContextMenuItem> UiShowMenuItems;
        /// <summary>
        /// Context menu shown when the UI Save submenu is opened.
        /// </summary>
        readonly ContextMenu UiSaveMenu;
        /// <summary>
        /// Items displayed by the UI Save submenu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> UiSaveMenuItems;
        /// <summary>
        /// Context menu shown when the UI Load submenu is opened.
        /// </summary>
        readonly ContextMenu UiLoadMenu;
        /// <summary>
        /// Items displayed by the UI Load submenu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> UiLoadMenuItems;
        /// <summary>
        /// Maps UI Show submenu labels to built-in menu actions.
        /// </summary>
        readonly Dictionary<string, EditorTitleBarUiMenuAction> UiShowMenuActionsByLabel;
        /// <summary>
        /// Contributed top-level project menus currently rendered in the title bar.
        /// </summary>
        readonly List<EditorTitleBarProjectMenuState> ProjectMenuStates;
        /// <summary>
        /// Current contributed project menu descriptors keyed by stable menu item identifier.
        /// </summary>
        readonly Dictionary<string, EditorMenuItemDescriptor> ProjectMenuItemsById;
        /// <summary>
        /// Current contributed project menu descriptors applied to the title bar.
        /// </summary>
        IReadOnlyList<EditorMenuItemDescriptor> ProjectMenuItems;
        /// <summary>
        /// Entity that hosts the minimize control.
        /// </summary>
        readonly EditorEntity MinimizeButtonEntity;
        /// <summary>
        /// Width reserved for the minimize control.
        /// </summary>
        int MinimizeButtonWidth;
        /// <summary>
        /// Entity that hosts the maximize control.
        /// </summary>
        readonly EditorEntity MaximizeButtonEntity;
        /// <summary>
        /// Width reserved for the maximize control.
        /// </summary>
        int MaximizeButtonWidth;
        /// <summary>
        /// Entity that hosts the close control.
        /// </summary>
        readonly EditorEntity CloseButtonEntity;
        /// <summary>
        /// Width reserved for the close control.
        /// </summary>
        int CloseButtonWidth;
        /// <summary>
        /// Render order used for the title bar background.
        /// </summary>
        readonly byte BackgroundOrder;
        /// <summary>
        /// Render order used for title bar foreground text and buttons.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Render order used for invisible input surfaces that must stay above the rest of the UI.
        /// </summary>
        readonly byte InputSurfaceOrder;

        /// <summary>
        /// Cached host size used to clamp the File menu inside the editor window.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// Tick count recorded for the most recent drag-region press.
        /// </summary>
        long LastTitleBarClickTicks;
        /// <summary>
        /// Pointer position recorded for the most recent drag-region press.
        /// </summary>
        int2 LastTitleBarClickPos;
        /// <summary>
        /// Current window title text.
        /// </summary>
        string TitleValue;

        /// <summary>
        /// Initializes the title bar UI with its File menu and window controls.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="windowWidth">Initial host window width.</param>
        /// <param name="windowHeight">Initial host window height.</param>
        /// <param name="titleText">Initial window title text.</param>
        /// <param name="iconTexture">Optional editor logo texture rendered in the left title-bar slot.</param>
        public EditorTitleBar(FontAsset font, int windowWidth, int windowHeight, string titleText, RuntimeTexture iconTexture = null)
            : this(font, EditorUiMetrics.Default, windowWidth, windowHeight, titleText, iconTexture) {
        }

        /// <summary>
        /// Initializes the title bar UI with its File menu and window controls using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the title bar chrome.</param>
        /// <param name="windowWidth">Initial host window width.</param>
        /// <param name="windowHeight">Initial host window height.</param>
        /// <param name="titleText">Initial window title text.</param>
        /// <param name="iconTexture">Optional editor logo texture rendered in the left title-bar slot.</param>
        public EditorTitleBar(FontAsset font, EditorUiMetrics metrics, int windowWidth, int windowHeight, string titleText, RuntimeTexture iconTexture = null) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Font = font;
            Metrics = metrics;
            TitleValue = titleText ?? string.Empty;
            IconEntity = null;
            IconSprite = null;
            BackgroundOrder = RenderOrder2D.PanelSurface;
            TextOrder = RenderOrder2D.PanelForeground;
            InputSurfaceOrder = RenderOrder2D.OverlayInput;
            HostSize = new int2(Math.Max(1, windowWidth), Math.Max(Height, windowHeight));

            RootEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = TitleBarLayerMask,
                Position = float3.Zero
            };

            Background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                Size = new int2(HostSize.X, Height),
                RenderOrder2D = BackgroundOrder
            };
            RootEntity.AddComponent(Background);

            HoverShieldEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = float3.Zero
            };
            RootEntity.AddChild(HoverShieldEntity);

            HoverShieldSurface = CreateInputSurface(new int2(HostSize.X, Height));
            HoverShieldEntity.AddComponent(HoverShieldSurface);

            HoverShieldInteractable = new InteractableComponent {
                Size = new int2(HostSize.X, Height)
            };
            HoverShieldEntity.AddComponent(HoverShieldInteractable);

            DragRegionEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = float3.Zero
            };
            RootEntity.AddChild(DragRegionEntity);

            DragRegion = new InteractableComponent {
                Size = new int2(0, Height)
            };
            DragRegion.CursorEvent += HandleTitleBarCursorEvent;
            DragRegionEntity.AddComponent(DragRegion);
            DragRegionInputSurface = CreateInputSurface(new int2(0, Height));
            DragRegionEntity.AddComponent(DragRegionInputSurface);

            FileMenuButtonEntity = CreateTitleBarButton("File", ToggleFileMenu, HandleFileMenuButtonHovered, true, false, out int fileMenuButtonWidth);
            FileMenuButtonWidth = fileMenuButtonWidth;
            AddMenuButtonEntity = CreateTitleBarButton("Add", ToggleAddMenu, HandleAddMenuButtonHovered, true, true, out int addMenuButtonWidth);
            AddMenuButtonWidth = addMenuButtonWidth;
            BuildMenuButtonEntity = CreateTitleBarButton("Build", ToggleBuildMenu, HandleBuildMenuButtonHovered, false, true, out int buildMenuButtonWidth);
            BuildMenuButtonWidth = buildMenuButtonWidth;
            UiMenuButtonEntity = CreateTitleBarButton("UI", ToggleUiMenu, HandleUiMenuButtonHovered, false, true, out int uiMenuButtonWidth);
            UiMenuButtonWidth = uiMenuButtonWidth;

            TitleEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(0f, GetTitleVerticalOffset(), 0f)
            };
            RootEntity.AddChild(TitleEntity);

            int titleHeight = (int)Math.Ceiling(Math.Max(Font.LineHeight, 1f));
            TitleTextComponent = new TextComponent {
                Font = Font,
                Text = TitleValue,
                Color = ThemeManager.Colors.AccentQuaternary,
                Size = new int2(Math.Max(1, HostSize.X), titleHeight),
                RenderOrder2D = TextOrder
            };
            TitleEntity.AddComponent(TitleTextComponent);

            if (iconTexture != null) {
                IconEntity = new EditorEntity {
                    LayerMask = TitleBarLayerMask,
                    Position = new float3(Metrics.HostTitleBarIconPadding, Metrics.HostTitleBarIconPadding, 0f)
                };
                RootEntity.AddChild(IconEntity);

                IconSprite = new SpriteComponent {
                    Texture = iconTexture,
                    Size = new int2(Metrics.HostTitleBarIconSize, Metrics.HostTitleBarIconSize),
                    RenderOrder2D = TextOrder
                };
                IconEntity.AddComponent(IconSprite);
            }

            byte menuBackgroundOrder = RenderOrder2D.OverlayBackground;
            byte menuTextOrder = RenderOrder2D.OverlayForeground;
            FileMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(FileMenu.Entity);
            FileMenuItems = BuildFileMenuItems();
            AddMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(AddMenu.Entity);
            AddMenuItems = BuildAddMenuItems();
            LightMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(LightMenu.Entity);
            LightMenuItems = BuildLightMenuItems();
            BuildMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(BuildMenu.Entity);
            BuildMenuItems = BuildBuildMenuItems();
            UiMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(UiMenu.Entity);
            UiMenuItems = BuildUiMenuItems();
            UiShowMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(UiShowMenu.Entity);
            UiShowMenuActionsByLabel = BuildUiShowMenuActionsByLabel();
            UiShowMenuItems = Array.Empty<ContextMenuItem>();
            ApplyUiShowMenuItems(new List<string>(UiShowMenuActionsByLabel.Keys));
            UiSaveMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(UiSaveMenu.Entity);
            UiSaveMenuItems = BuildUiSaveMenuItems();
            UiLoadMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(UiLoadMenu.Entity);
            UiLoadMenuItems = BuildUiLoadMenuItems();
            ProjectMenuStates = [];
            ProjectMenuItemsById = new Dictionary<string, EditorMenuItemDescriptor>(StringComparer.OrdinalIgnoreCase);
            ProjectMenuItems = Array.Empty<EditorMenuItemDescriptor>();

            MinimizeButtonEntity = CreateTitleBarButton("-", HandleMinimizeRequested, null, true, false, out int minimizeButtonWidth);
            MinimizeButtonWidth = minimizeButtonWidth;
            MaximizeButtonEntity = CreateTitleBarButton("Max", HandleToggleMaximizeRequested, null, true, false, out int maximizeButtonWidth);
            MaximizeButtonWidth = maximizeButtonWidth;
            CloseButtonEntity = CreateTitleBarButton("X", HandleCloseRequested, null, true, false, out int closeButtonWidth);
            CloseButtonWidth = closeButtonWidth;

            UpdateLayout(HostSize.X, HostSize.Y);
        }

        /// <summary>
        /// Gets the root entity representing the title bar.
        /// </summary>
        public EditorEntity Entity => RootEntity;

        /// <summary>
        /// Gets or sets the visible window title text.
        /// </summary>
        public string Title {
            get { return TitleValue; }
            set {
                TitleValue = value ?? string.Empty;
                TitleTextComponent.Text = TitleValue;
            }
        }

        /// <summary>
        /// Gets the height of the title bar in pixels.
        /// </summary>
        public int Height => Metrics.HostTitleBarHeight;

        /// <summary>
        /// Reapplies the title-bar font and metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated font used to render title-bar labels.</param>
        /// <param name="metrics">Updated scaled editor UI metrics used to size the title-bar chrome.</param>
        public void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Font = font;
            Metrics = metrics;
            TitleTextComponent.Font = font;

            FileMenuButtonWidth = ComputeButtonWidth("File");
            AddMenuButtonWidth = ComputeButtonWidth("Add");
            BuildMenuButtonWidth = ComputeButtonWidth("Build");
            UiMenuButtonWidth = ComputeButtonWidth("UI");
            MinimizeButtonWidth = ComputeButtonWidth("-");
            MaximizeButtonWidth = ComputeButtonWidth("Max");
            CloseButtonWidth = ComputeButtonWidth("X");

            UpdateTitleBarButtonChrome(FileMenuButtonEntity, FileMenuButtonWidth, true, false, font);
            UpdateTitleBarButtonChrome(AddMenuButtonEntity, AddMenuButtonWidth, true, true, font);
            UpdateTitleBarButtonChrome(BuildMenuButtonEntity, BuildMenuButtonWidth, false, true, font);
            UpdateTitleBarButtonChrome(UiMenuButtonEntity, UiMenuButtonWidth, false, true, font);
            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                EditorTitleBarProjectMenuState projectMenuState = ProjectMenuStates[index];
                projectMenuState.ButtonWidth = ComputeButtonWidth(projectMenuState.TopLevelMenuLabel);
                UpdateTitleBarButtonChrome(projectMenuState.ButtonEntity, projectMenuState.ButtonWidth, false, true, font);
            }
            UpdateTitleBarButtonChrome(MinimizeButtonEntity, MinimizeButtonWidth, true, false, font);
            UpdateTitleBarButtonChrome(MaximizeButtonEntity, MaximizeButtonWidth, true, false, font);
            UpdateTitleBarButtonChrome(CloseButtonEntity, CloseButtonWidth, true, false, font);

            if (IconEntity != null) {
                IconEntity.Position = new float3(Metrics.HostTitleBarIconPadding, Metrics.HostTitleBarIconPadding, 0f);
            }
            if (IconSprite != null) {
                IconSprite.Size = new int2(Metrics.HostTitleBarIconSize, Metrics.HostTitleBarIconSize);
            }

            UpdateLayout(HostSize.X, HostSize.Y);
        }

        /// <summary>
        /// Raised when the user initiates a window drag from the title region.
        /// </summary>
        public event Action DragRequested;

        /// <summary>
        /// Raised when the user requests a maximize or restore action.
        /// </summary>
        public event Action ToggleMaximizeRequested;

        /// <summary>
        /// Raised when the user clicks the minimize control.
        /// </summary>
        public event Action MinimizeRequested;

        /// <summary>
        /// Raised when the user clicks the close control.
        /// </summary>
        public event Action CloseRequested;

        /// <summary>
        /// Raised when the user selects the New Map file-menu command.
        /// </summary>
        public event Action NewMapRequested;

        /// <summary>
        /// Raised when the user selects the Open Map file-menu command.
        /// </summary>
        public event Action OpenMapRequested;

        /// <summary>
        /// Raised when the user selects the Save Map file-menu command.
        /// </summary>
        public event Action SaveMapRequested;

        /// <summary>
        /// Raised when the user selects the Save Map As file-menu command.
        /// </summary>
        public event Action SaveMapAsRequested;
        /// <summary>
        /// Raised when the user selects the Preferences file-menu command.
        /// </summary>
        public event Action PreferencesRequested;
        /// <summary>
        /// Raised when the user selects the Scene Settings file-menu command.
        /// </summary>
        public event Action SceneSettingsRequested;
        /// <summary>
        /// Raised when the user selects the Add Empty command.
        /// </summary>
        public event Action AddEmptyRequested;
        /// <summary>
        /// Raised when the user selects the Add Cube command.
        /// </summary>
        public event Action AddCubeRequested;
        /// <summary>
        /// Raised when the user selects the Add Plane command.
        /// </summary>
        public event Action AddPlaneRequested;
        /// <summary>
        /// Raised when the user selects the Add Camera command.
        /// </summary>
        public event Action AddCameraRequested;
        /// <summary>
        /// Raised when the user selects the Add Spot Light command.
        /// </summary>
        public event Action AddSpotLightRequested;
        /// <summary>
        /// Raised when the user selects the Add Point Light command.
        /// </summary>
        public event Action AddPointLightRequested;
        /// <summary>
        /// Raised when the user selects the Add Directional Light command.
        /// </summary>
        public event Action AddDirectionalLightRequested;
        /// <summary>
        /// Raised when the user selects the Build Settings command.
        /// </summary>
        public event Action BuildSettingsRequested;
        /// <summary>
        /// Raised when the user selects the Platforms command.
        /// </summary>
        public event Action PlatformsRequested;
        /// <summary>
        /// Raised when the user selects the Build command.
        /// </summary>
        public event Action BuildRequested;
        /// <summary>
        /// Raised when the user selects the Profiles command.
        /// </summary>
        public event Action ProfilesRequested;
        /// <summary>
        /// Raised when the user selects the Build Scripts command.
        /// </summary>
        public event Action BuildScriptsRequested;
        /// <summary>
        /// Raised when the user selects the Open in IDE command.
        /// </summary>
        public event Action OpenInIDERequested;
        /// <summary>
        /// Raised when one contributed project-authored menu item is activated.
        /// </summary>
        public event Action<string> ProjectMenuItemRequested;
        /// <summary>
        /// Raised when one built-in UI menu action is activated.
        /// </summary>
        public event Action<EditorTitleBarUiMenuAction> UiMenuActionRequested;

        /// <summary>
        /// Updates button placement, menu clamping, and title sizing to fit the provided host size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            int width = Math.Max(1, windowWidth);
            int height = Math.Max(Height, windowHeight);
            HostSize = new int2(width, height);

            Background.Size = new int2(width + 1, Height);
            HoverShieldSurface.Size = new int2(width, Height);
            HoverShieldInteractable.Size = new int2(width, Height);

            float fileButtonX = GetLeftIconSlotWidth();
            FileMenuButtonEntity.Position = new float3(fileButtonX, ButtonTop, 0f);
            float addButtonX = fileButtonX + FileMenuButtonWidth + ButtonSpacing;
            AddMenuButtonEntity.Position = new float3(addButtonX, ButtonTop, 0f);
            float buildButtonX = addButtonX + AddMenuButtonWidth + ButtonSpacing;
            BuildMenuButtonEntity.Position = new float3(buildButtonX, ButtonTop, 0f);
            float uiButtonX = buildButtonX + BuildMenuButtonWidth + ButtonSpacing;
            UiMenuButtonEntity.Position = new float3(uiButtonX, ButtonTop, 0f);
            float lastMenuButtonRightEdge = uiButtonX + UiMenuButtonWidth;
            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                EditorTitleBarProjectMenuState projectMenuState = ProjectMenuStates[index];
                float projectButtonX = lastMenuButtonRightEdge + ButtonSpacing;
                projectMenuState.ButtonEntity.Position = new float3(projectButtonX, ButtonTop, 0f);
                lastMenuButtonRightEdge = projectButtonX + projectMenuState.ButtonWidth;
            }

            int totalControlsWidth = MinimizeButtonWidth + MaximizeButtonWidth + CloseButtonWidth + (ButtonSpacing * 2);
            float titleX = lastMenuButtonRightEdge + ButtonSpacing + GetTitleSpacing();
            float controlStartX = Math.Max(0, width - totalControlsWidth);

            TitleEntity.Position = new float3(titleX, GetTitleVerticalOffset(), 0f);
            int titleWidth = Math.Max(GetMinimumTitleWidth(), (int)Math.Floor(controlStartX - titleX - GetTitleSpacing()));
            TitleTextComponent.Size = new int2(titleWidth, TitleTextComponent.Size.Y);

            LayoutWindowControls(controlStartX);
            UpdateDragRegion(controlStartX);
            FileMenu.UpdateLayout(HostSize);
            AddMenu.UpdateLayout(HostSize);
            LightMenu.UpdateLayout(HostSize);
            BuildMenu.UpdateLayout(HostSize);
            UiMenu.UpdateLayout(HostSize);
            UiShowMenu.UpdateLayout(HostSize);
            UiSaveMenu.UpdateLayout(HostSize);
            UiLoadMenu.UpdateLayout(HostSize);
            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                ProjectMenuStates[index].Menu.UpdateLayout(HostSize);
            }
        }

        /// <summary>
        /// Applies one fresh contributed project-menu set, replacing any prior project-authored menu state.
        /// </summary>
        /// <param name="menuItems">Project-authored menu descriptors that should be rendered in the title bar.</param>
        public void ApplyProjectMenus(IReadOnlyList<EditorMenuItemDescriptor> menuItems) {
            ClearProjectMenus();
            ProjectMenuItems = menuItems ?? Array.Empty<EditorMenuItemDescriptor>();
            RebuildProjectMenus();
            UpdateLayout(HostSize.X, HostSize.Y);
        }

        /// <summary>
        /// Activates one contributed project menu item for test coverage without simulating pointer input.
        /// </summary>
        /// <param name="menuItemId">Stable contributed menu item identifier to activate.</param>
        internal void ActivateProjectMenuItemForTest(string menuItemId) {
            RaiseProjectMenuItemRequested(menuItemId);
        }

        /// <summary>
        /// Activates one built-in UI menu item for test coverage without simulating pointer input.
        /// </summary>
        /// <param name="menuItemId">Stable built-in UI menu item identifier to activate.</param>
        internal void ActivateUiMenuItemForTest(string menuItemId) {
            RaiseUiMenuActionRequested(ResolveUiMenuAction(menuItemId));
        }

        /// <summary>
        /// Applies the set of panel labels that should appear under the UI Show submenu.
        /// </summary>
        /// <param name="panelLabels">Panel labels that should be offered for opening.</param>
        public void ApplyUiShowMenuItems(IReadOnlyList<string> panelLabels) {
            if (panelLabels == null) {
                throw new ArgumentNullException(nameof(panelLabels));
            }

            List<ContextMenuItem> menuItems = new List<ContextMenuItem>(panelLabels.Count);
            for (int index = 0; index < panelLabels.Count; index++) {
                string panelLabel = panelLabels[index];
                if (string.IsNullOrWhiteSpace(panelLabel)) {
                    throw new InvalidOperationException("UI Show panel labels must not be blank.");
                }
                if (!UiShowMenuActionsByLabel.TryGetValue(panelLabel, out EditorTitleBarUiMenuAction action)) {
                    throw new InvalidOperationException($"UI Show panel '{panelLabel}' is not registered.");
                }

                menuItems.Add(new ContextMenuItem(panelLabel, CreateUiMenuActionHandler(action)));
            }

            UiShowMenuItems = menuItems;
        }

        /// <summary>
        /// Shows the UI Show submenu for test coverage without simulating pointer input.
        /// </summary>
        internal void ShowUiShowMenuForTest() {
            UiShowMenu.Show(UiShowMenuItems, GetUiShowMenuPosition(), HostSize);
        }

        /// <summary>
        /// Creates the File menu items shown on the left side of the title bar.
        /// </summary>
        /// <returns>Immutable collection of File menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildFileMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("New Map", RaiseNewMapRequested),
                new ContextMenuItem("Open Map...", RaiseOpenMapRequested),
                new ContextMenuItem("Save Map", RaiseSaveMapRequested),
                new ContextMenuItem("Save Map As...", RaiseSaveMapAsRequested),
                new ContextMenuItem("Scene Settings...", RaiseSceneSettingsRequested),
                new ContextMenuItem("Preferences...", RaisePreferencesRequested)
            };
        }

        /// <summary>
        /// Creates the Add menu items shown beside the File button.
        /// </summary>
        /// <returns>Immutable collection of Add menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildAddMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Empty", RaiseAddEmptyRequested, HideLightMenu, true),
                new ContextMenuItem("Cube", RaiseAddCubeRequested, HideLightMenu, true),
                new ContextMenuItem("Plane", RaiseAddPlaneRequested, HideLightMenu, true),
                new ContextMenuItem("Camera", RaiseAddCameraRequested, HideLightMenu, true),
                new ContextMenuItem("Light", ShowLightMenu, ShowLightMenu, false)
            };
        }

        /// <summary>
        /// Creates the Light submenu items shown beneath the Add menu.
        /// </summary>
        /// <returns>Immutable collection of Light submenu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildLightMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Spot Light", RaiseAddSpotLightRequested),
                new ContextMenuItem("Point Light", RaiseAddPointLightRequested),
                new ContextMenuItem("Directional Light", RaiseAddDirectionalLightRequested)
            };
        }

        /// <summary>
        /// Creates the Build menu items shown beside the Add button.
        /// </summary>
        /// <returns>Immutable collection of Build menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildBuildMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Platforms...", RaisePlatformsRequested),
                new ContextMenuItem("Profiles...", RaiseProfilesRequested),
                new ContextMenuItem("Build...", RaiseBuildRequested),
                new ContextMenuItem("Build Scripts...", RaiseBuildScriptsRequested),
                new ContextMenuItem("Open in IDE...", RaiseOpenInIDERequested)
            };
        }

        /// <summary>
        /// Creates the UI menu items shown beside the Build button.
        /// </summary>
        /// <returns>Immutable collection of UI menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildUiMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Show", ShowUiShowMenu, ShowUiShowMenu, false),
                new ContextMenuItem("Save", ShowUiSaveMenu, ShowUiSaveMenu, false),
                new ContextMenuItem("Load", ShowUiLoadMenu, ShowUiLoadMenu, false)
            };
        }

        /// <summary>
        /// Creates the built-in UI Show action map keyed by panel label.
        /// </summary>
        /// <returns>Lookup of panel labels to UI menu actions.</returns>
        Dictionary<string, EditorTitleBarUiMenuAction> BuildUiShowMenuActionsByLabel() {
            return new Dictionary<string, EditorTitleBarUiMenuAction>(StringComparer.OrdinalIgnoreCase) {
                ["Viewport"] = EditorTitleBarUiMenuAction.ShowViewport,
                ["Scene Hierarchy"] = EditorTitleBarUiMenuAction.ShowSceneHierarchy,
                ["Asset Browser"] = EditorTitleBarUiMenuAction.ShowAssetBrowser,
                ["Properties"] = EditorTitleBarUiMenuAction.ShowProperties,
                ["Logger"] = EditorTitleBarUiMenuAction.ShowLogger,
                ["Preview"] = EditorTitleBarUiMenuAction.ShowPreview
            };
        }

        /// <summary>
        /// Creates the UI Save submenu items.
        /// </summary>
        /// <returns>Immutable collection of save-slot menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildUiSaveMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Slot 1", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.SaveSlot1)),
                new ContextMenuItem("Slot 2", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.SaveSlot2)),
                new ContextMenuItem("Slot 3", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.SaveSlot3)),
                new ContextMenuItem("Slot 4", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.SaveSlot4)),
                new ContextMenuItem("Slot 5", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.SaveSlot5))
            };
        }

        /// <summary>
        /// Creates the UI Load submenu items.
        /// </summary>
        /// <returns>Immutable collection of load-slot menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildUiLoadMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Slot 1", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.LoadSlot1)),
                new ContextMenuItem("Slot 2", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.LoadSlot2)),
                new ContextMenuItem("Slot 3", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.LoadSlot3)),
                new ContextMenuItem("Slot 4", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.LoadSlot4)),
                new ContextMenuItem("Slot 5", CreateUiMenuActionHandler(EditorTitleBarUiMenuAction.LoadSlot5))
            };
        }

        /// <summary>
        /// Rebuilds the currently applied contributed project menus from the current descriptor list.
        /// </summary>
        void RebuildProjectMenus() {
            if (ProjectMenuItems.Count < 1) {
                return;
            }

            byte menuBackgroundOrder = RenderOrder2D.OverlayBackground;
            byte menuTextOrder = RenderOrder2D.OverlayForeground;
            Dictionary<string, List<EditorMenuItemDescriptor>> itemsByTopLevelMenuId = new Dictionary<string, List<EditorMenuItemDescriptor>>(StringComparer.OrdinalIgnoreCase);
            List<string> topLevelMenuIds = new List<string>();
            for (int index = 0; index < ProjectMenuItems.Count; index++) {
                EditorMenuItemDescriptor menuItem = ProjectMenuItems[index] ?? throw new InvalidOperationException("Project menu descriptors must not contain null entries.");
                ProjectMenuItemsById.Add(menuItem.MenuItemId, menuItem);

                if (!itemsByTopLevelMenuId.TryGetValue(menuItem.TopLevelMenuId, out List<EditorMenuItemDescriptor> topLevelItems)) {
                    topLevelItems = new List<EditorMenuItemDescriptor>();
                    itemsByTopLevelMenuId.Add(menuItem.TopLevelMenuId, topLevelItems);
                    topLevelMenuIds.Add(menuItem.TopLevelMenuId);
                }

                topLevelItems.Add(menuItem);
            }

            for (int index = 0; index < topLevelMenuIds.Count; index++) {
                string topLevelMenuId = topLevelMenuIds[index];
                List<EditorMenuItemDescriptor> topLevelItems = itemsByTopLevelMenuId[topLevelMenuId];
                EditorMenuItemDescriptor firstItem = topLevelItems[0];
                EditorEntity buttonEntity = CreateTitleBarButton(
                    firstItem.TopLevelMenuLabel,
                    CreateProjectMenuToggleAction(firstItem.TopLevelMenuId),
                    CreateProjectMenuHoverAction(firstItem.TopLevelMenuId),
                    false,
                    true,
                    out int buttonWidth);
                ContextMenu menu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
                RootEntity.AddChild(menu.Entity);
                IReadOnlyList<ContextMenuItem> menuItems = BuildProjectMenuItems(topLevelItems);
                ProjectMenuStates.Add(new EditorTitleBarProjectMenuState(
                    firstItem.TopLevelMenuId,
                    firstItem.TopLevelMenuLabel,
                    firstItem.TopLevelMenuOrder,
                    buttonEntity,
                    buttonWidth,
                    menu,
                    menuItems));
            }
        }

        /// <summary>
        /// Builds one context-menu item list for the supplied contributed top-level menu descriptors.
        /// </summary>
        /// <param name="items">Contributed menu descriptors that belong to one top-level menu.</param>
        /// <returns>Immutable contributed context-menu item list.</returns>
        IReadOnlyList<ContextMenuItem> BuildProjectMenuItems(IReadOnlyList<EditorMenuItemDescriptor> items) {
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }

            ContextMenuItem[] menuItems = new ContextMenuItem[items.Count];
            for (int index = 0; index < items.Count; index++) {
                EditorMenuItemDescriptor item = items[index] ?? throw new InvalidOperationException("Project menu descriptors must not contain null entries.");
                menuItems[index] = new ContextMenuItem(item.MenuItemLabel, CreateProjectMenuItemAction(item.MenuItemId));
            }

            return menuItems;
        }

        /// <summary>
        /// Clears every contributed project menu from the title bar before one fresh menu set is applied.
        /// </summary>
        void ClearProjectMenus() {
            HideProjectMenus();

            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                EditorTitleBarProjectMenuState projectMenuState = ProjectMenuStates[index];
                RootEntity.RemoveChild(projectMenuState.Menu.Entity);
                RootEntity.RemoveChild(projectMenuState.ButtonEntity);
            }

            ProjectMenuStates.Clear();
            ProjectMenuItemsById.Clear();
        }

        /// <summary>
        /// Creates one reusable click handler for the supplied contributed top-level menu identifier.
        /// </summary>
        /// <param name="topLevelMenuId">Stable contributed top-level menu identifier.</param>
        /// <returns>Reusable click handler that toggles the matching contributed menu.</returns>
        Action CreateProjectMenuToggleAction(string topLevelMenuId) {
            if (string.IsNullOrWhiteSpace(topLevelMenuId)) {
                throw new ArgumentException("Top-level menu id must be provided.", nameof(topLevelMenuId));
            }

            return () => ToggleProjectMenu(topLevelMenuId);
        }

        /// <summary>
        /// Creates one reusable hover handler for the supplied contributed top-level menu identifier.
        /// </summary>
        /// <param name="topLevelMenuId">Stable contributed top-level menu identifier.</param>
        /// <returns>Reusable hover handler that switches to the matching contributed menu.</returns>
        Action CreateProjectMenuHoverAction(string topLevelMenuId) {
            if (string.IsNullOrWhiteSpace(topLevelMenuId)) {
                throw new ArgumentException("Top-level menu id must be provided.", nameof(topLevelMenuId));
            }

            return () => HandleProjectMenuButtonHovered(topLevelMenuId);
        }

        /// <summary>
        /// Creates one reusable click handler for the supplied contributed menu item identifier.
        /// </summary>
        /// <param name="menuItemId">Stable contributed menu item identifier.</param>
        /// <returns>Reusable click handler that raises the contributed menu activation event.</returns>
        Action CreateProjectMenuItemAction(string menuItemId) {
            if (string.IsNullOrWhiteSpace(menuItemId)) {
                throw new ArgumentException("Menu item id must be provided.", nameof(menuItemId));
            }

            return () => RaiseProjectMenuItemRequested(menuItemId);
        }

        /// <summary>
        /// Creates a title bar button with the standard title bar styling.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <param name="onClick">Callback invoked when the button is activated.</param>
        /// <param name="onHover">Optional callback invoked when the button is hovered.</param>
        /// <param name="includeLeftBorder">True when the button should draw its own left separator.</param>
        /// <param name="includeRightBorder">True when the button should draw its own right separator.</param>
        /// <param name="width">Computed button width.</param>
        /// <returns>Entity hosting the created button.</returns>
        EditorEntity CreateTitleBarButton(string label, Action onClick, Action onHover, bool includeLeftBorder, bool includeRightBorder, out int width) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Button label must be provided.", nameof(label));
            }
            if (onClick == null) {
                throw new ArgumentNullException(nameof(onClick));
            }

            width = ComputeButtonWidth(label);
            EditorEntity buttonEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(0f, ButtonTop, 0f)
            };

            ButtonComponent button = new ButtonComponent(label, new int2(width, GetButtonHeight()), Font, onClick, 0f);
            if (onHover != null) {
                button.Hovered += onHover;
            }
            button.SetRenderOrders(BackgroundOrder, TextOrder);
            button.UseHoverOnlyBackground();
            button.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            button.UseSquareCorners();
            buttonEntity.AddComponent(button);
            buttonEntity.AddComponent(CreateInputSurface(new int2(width, GetButtonHeight())));
            AddTitleBarButtonVerticalBorders(buttonEntity, width, includeLeftBorder, includeRightBorder);
            RootEntity.AddChild(buttonEntity);
            return buttonEntity;
        }

        /// <summary>
        /// Reapplies the size, font, and separator geometry for one existing title-bar button.
        /// </summary>
        /// <param name="buttonEntity">Existing title-bar button entity.</param>
        /// <param name="width">Updated button width in pixels.</param>
        /// <param name="includeLeftBorder">True when the left separator should remain visible.</param>
        /// <param name="includeRightBorder">True when the right separator should remain visible.</param>
        /// <param name="font">Updated font used by the button label.</param>
        void UpdateTitleBarButtonChrome(EditorEntity buttonEntity, int width, bool includeLeftBorder, bool includeRightBorder, FontAsset font) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            ButtonComponent button = GetComponent<ButtonComponent>(buttonEntity);
            button.Font = font;
            button.SetSize(new int2(width, GetButtonHeight()));

            SpriteComponent inputSurface = FindInputSurface(buttonEntity);
            if (inputSurface != null) {
                inputSurface.Size = new int2(width, GetButtonHeight());
            }

            UpdateTitleBarButtonVerticalBorders(buttonEntity, width, includeLeftBorder, includeRightBorder);
        }

        /// <summary>
        /// Updates existing vertical separators for one title-bar button.
        /// </summary>
        /// <param name="buttonEntity">Button entity that owns the separators.</param>
        /// <param name="width">Current button width in pixels.</param>
        /// <param name="includeLeftBorder">True when the left separator should remain visible.</param>
        /// <param name="includeRightBorder">True when the right separator should remain visible.</param>
        void UpdateTitleBarButtonVerticalBorders(EditorEntity buttonEntity, int width, bool includeLeftBorder, bool includeRightBorder) {
            if (buttonEntity.Children == null) {
                return;
            }

            List<Entity> borderEntities = FindBorderEntities(buttonEntity);

            int borderIndex = 0;
            if (includeLeftBorder && borderIndex < borderEntities.Count) {
                UpdateTitleBarButtonVerticalBorderLine(borderEntities[borderIndex], 0f);
                borderIndex++;
            }

            if (includeRightBorder && borderIndex < borderEntities.Count) {
                UpdateTitleBarButtonVerticalBorderLine(borderEntities[borderIndex], width - GetButtonBorderWidth());
            }
        }

        /// <summary>
        /// Updates one existing vertical separator inside a title-bar button.
        /// </summary>
        /// <param name="borderEntity">Separator entity to reposition and resize.</param>
        /// <param name="x">Local x offset for the separator.</param>
        void UpdateTitleBarButtonVerticalBorderLine(Entity borderEntity, float x) {
            if (borderEntity == null) {
                throw new ArgumentNullException(nameof(borderEntity));
            }

            borderEntity.Position = new float3(x, 0f, 0f);
            SpriteComponent border = GetComponent<SpriteComponent>(borderEntity);
            border.Size = new int2(GetButtonBorderWidth(), GetButtonHeight());
        }

        /// <summary>
        /// Finds the transparent input surface component attached to one title-bar button entity.
        /// </summary>
        /// <param name="buttonEntity">Button entity to inspect.</param>
        /// <returns>Transparent input surface component when present; otherwise null.</returns>
        SpriteComponent FindInputSurface(EditorEntity buttonEntity) {
            if (buttonEntity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < buttonEntity.Components.Count; componentIndex++) {
                if (buttonEntity.Components[componentIndex] is SpriteComponent spriteComponent &&
                    spriteComponent.RenderOrder2D == InputSurfaceOrder &&
                    spriteComponent.Color.W == 0) {
                    return spriteComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the child entities that render title-bar separator lines.
        /// </summary>
        /// <param name="buttonEntity">Button entity to inspect.</param>
        /// <returns>Ordered list of separator child entities.</returns>
        List<Entity> FindBorderEntities(EditorEntity buttonEntity) {
            List<Entity> borderEntities = new List<Entity>(2);
            if (buttonEntity == null || buttonEntity.Children == null) {
                return borderEntities;
            }

            for (int childIndex = 0; childIndex < buttonEntity.Children.Count; childIndex++) {
                Entity childEntity = buttonEntity.Children[childIndex];
                if (TryGetComponent<SpriteComponent>(childEntity, out _)) {
                    borderEntities.Add(childEntity);
                }
            }

            return borderEntities;
        }

        /// <summary>
        /// Gets the first component of the requested type from one entity.
        /// </summary>
        /// <typeparam name="T">Component type to locate.</typeparam>
        /// <param name="entity">Entity that owns the component.</param>
        /// <returns>Component of the requested type.</returns>
        T GetComponent<T>(Entity entity) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                throw new InvalidOperationException("Expected the entity to contain components.");
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is T component) {
                    return component;
                }
            }

            throw new InvalidOperationException("Expected the entity to contain the requested component type.");
        }

        /// <summary>
        /// Attempts to locate the first component of the requested type on one entity.
        /// </summary>
        /// <typeparam name="T">Component type to locate.</typeparam>
        /// <param name="entity">Entity that owns the component.</param>
        /// <param name="component">Resolved component when present.</param>
        /// <returns>True when the requested component type was found; otherwise false.</returns>
        bool TryGetComponent<T>(Entity entity, out T component) where T : Component {
            component = null;
            if (entity == null || entity.Components == null) {
                return false;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is T typedComponent) {
                    component = typedComponent;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds one-pixel vertical separators to a title-bar button.
        /// </summary>
        /// <param name="buttonEntity">Button entity that owns the separators.</param>
        /// <param name="width">Current button width in pixels.</param>
        /// <param name="includeLeftBorder">True when the left separator should be drawn.</param>
        /// <param name="includeRightBorder">True when the right separator should be drawn.</param>
        void AddTitleBarButtonVerticalBorders(EditorEntity buttonEntity, int width, bool includeLeftBorder, bool includeRightBorder) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }

            if (includeLeftBorder) {
                AddTitleBarButtonVerticalBorderLine(buttonEntity, 0f);
            }

            if (includeRightBorder) {
                AddTitleBarButtonVerticalBorderLine(buttonEntity, width - GetButtonBorderWidth());
            }
        }

        /// <summary>
        /// Adds a one-pixel vertical separator at the requested x offset inside a title-bar button.
        /// </summary>
        /// <param name="buttonEntity">Button entity that owns the separator.</param>
        /// <param name="x">Local x offset for the separator.</param>
        void AddTitleBarButtonVerticalBorderLine(EditorEntity buttonEntity, float x) {
            EditorEntity borderEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(x, 0f, 0f)
            };

            SpriteComponent border = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentQuaternary,
                Size = new int2(GetButtonBorderWidth(), GetButtonHeight()),
                RenderOrder2D = TextOrder
            };
            borderEntity.AddComponent(border);
            buttonEntity.AddChild(borderEntity);
        }

        /// <summary>
        /// Lays out the right-aligned window control buttons.
        /// </summary>
        /// <param name="startX">Starting x coordinate for the first control.</param>
        void LayoutWindowControls(float startX) {
            float controlX = startX;

            MinimizeButtonEntity.Position = new float3(controlX, ButtonTop, 0f);
            controlX += MinimizeButtonWidth + ButtonSpacing;

            MaximizeButtonEntity.Position = new float3(controlX, ButtonTop, 0f);
            controlX += MaximizeButtonWidth + ButtonSpacing;

            CloseButtonEntity.Position = new float3(controlX, ButtonTop, 0f);
        }

        /// <summary>
        /// Updates the drag-region bounds so the File button and window controls remain independently clickable.
        /// </summary>
        /// <param name="controlStartX">Left edge of the window control cluster.</param>
        void UpdateDragRegion(float controlStartX) {
            float dragRegionX = GetMenuStripRightEdge() + ButtonSpacing;
            int dragRegionWidth = Math.Max(0, (int)Math.Floor(controlStartX - dragRegionX - ButtonSpacing));

            DragRegionEntity.Position = new float3(dragRegionX, 0f, 0f);
            DragRegion.Size = new int2(dragRegionWidth, Height);
            DragRegionInputSurface.Size = new int2(dragRegionWidth, Height);
        }

        /// <summary>
        /// Creates a transparent sprite that participates in draw ordering so input can respect title-bar occlusion.
        /// </summary>
        /// <param name="size">Surface size in pixels.</param>
        /// <returns>Transparent sprite component registered at the dedicated input-surface order.</returns>
        SpriteComponent CreateInputSurface(int2 size) {
            return new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(255, 255, 255, 0),
                Size = size,
                RenderOrder2D = InputSurfaceOrder
            };
        }

        /// <summary>
        /// Hides every title-bar context menu.
        /// </summary>
        void HideMenus() {
            FileMenu.Hide();
            AddMenu.Hide();
            LightMenu.Hide();
            BuildMenu.Hide();
            UiMenu.Hide();
            UiShowMenu.Hide();
            UiSaveMenu.Hide();
            UiLoadMenu.Hide();
            HideProjectMenus();
        }

        /// <summary>
        /// Shows or hides the File menu anchored beneath the File button.
        /// </summary>
        void ToggleFileMenu() {
            if (FileMenu.IsVisible) {
                FileMenu.Hide();
                return;
            }

            ShowFileMenu();
        }

        /// <summary>
        /// Shows or hides the Add menu anchored beneath the Add button.
        /// </summary>
        void ToggleAddMenu() {
            if (AddMenu.IsVisible) {
                HideMenus();
                return;
            }

            ShowAddMenu();
        }

        /// <summary>
        /// Shows or hides the Build menu anchored beneath the Build button.
        /// </summary>
        void ToggleBuildMenu() {
            if (BuildMenu.IsVisible) {
                BuildMenu.Hide();
                return;
            }

            ShowBuildMenu();
        }

        /// <summary>
        /// Shows or hides the UI menu anchored beneath the UI button.
        /// </summary>
        void ToggleUiMenu() {
            if (UiMenu.IsVisible) {
                UiMenu.Hide();
                UiShowMenu.Hide();
                UiSaveMenu.Hide();
                UiLoadMenu.Hide();
                return;
            }

            ShowUiMenu();
        }

        /// <summary>
        /// Switches to the File menu when hovering across an already active menu strip.
        /// </summary>
        void HandleFileMenuButtonHovered() {
            if (!IsAnyOtherTopLevelMenuVisible("file")) {
                return;
            }

            ShowFileMenu();
        }

        /// <summary>
        /// Switches to the Add menu when hovering across an already active menu strip.
        /// </summary>
        void HandleAddMenuButtonHovered() {
            if (!IsAnyOtherTopLevelMenuVisible("add")) {
                return;
            }

            ShowAddMenu();
        }

        /// <summary>
        /// Switches to the Build menu when hovering across an already active menu strip.
        /// </summary>
        void HandleBuildMenuButtonHovered() {
            if (!IsAnyOtherTopLevelMenuVisible("build")) {
                return;
            }

            ShowBuildMenu();
        }

        /// <summary>
        /// Switches to the UI menu when hovering across an already active menu strip.
        /// </summary>
        void HandleUiMenuButtonHovered() {
            if (!IsAnyOtherTopLevelMenuVisible("ui")) {
                return;
            }

            ShowUiMenu();
        }

        /// <summary>
        /// Switches to one contributed menu when hovering across an already active menu strip.
        /// </summary>
        /// <param name="topLevelMenuId">Stable contributed top-level menu identifier.</param>
        void HandleProjectMenuButtonHovered(string topLevelMenuId) {
            if (!IsAnyOtherTopLevelMenuVisible(topLevelMenuId)) {
                return;
            }

            ShowProjectMenu(topLevelMenuId);
        }

        /// <summary>
        /// Shows the File menu and closes any other title-bar menu.
        /// </summary>
        void ShowFileMenu() {
            HideMenus();
            FileMenu.Show(FileMenuItems, GetFileMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the Add menu and closes any other title-bar menu.
        /// </summary>
        void ShowAddMenu() {
            HideMenus();
            AddMenu.Show(AddMenuItems, GetAddMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the Build menu and closes any other title-bar menu.
        /// </summary>
        void ShowBuildMenu() {
            HideMenus();
            BuildMenu.Show(BuildMenuItems, GetBuildMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the UI menu and closes any other title-bar menu.
        /// </summary>
        void ShowUiMenu() {
            HideMenus();
            UiMenu.Show(UiMenuItems, GetUiMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows or hides one contributed menu anchored beneath its top-level button.
        /// </summary>
        /// <param name="topLevelMenuId">Stable contributed top-level menu identifier.</param>
        void ToggleProjectMenu(string topLevelMenuId) {
            EditorTitleBarProjectMenuState projectMenuState = GetProjectMenuState(topLevelMenuId);
            if (projectMenuState.Menu.IsVisible) {
                projectMenuState.Menu.Hide();
                return;
            }

            ShowProjectMenu(topLevelMenuId);
        }

        /// <summary>
        /// Shows one contributed top-level project menu and closes every other title-bar menu.
        /// </summary>
        /// <param name="topLevelMenuId">Stable contributed top-level menu identifier.</param>
        void ShowProjectMenu(string topLevelMenuId) {
            EditorTitleBarProjectMenuState projectMenuState = GetProjectMenuState(topLevelMenuId);
            HideMenus();
            projectMenuState.Menu.Show(projectMenuState.MenuItems, GetProjectMenuPosition(projectMenuState), HostSize);
        }

        /// <summary>
        /// Shows the Light submenu anchored beside the Light entry inside the Add menu.
        /// </summary>
        void ShowLightMenu() {
            if (!AddMenu.IsVisible) {
                return;
            }

            FileMenu.Hide();
            BuildMenu.Hide();
            UiMenu.Hide();
            UiShowMenu.Hide();
            UiSaveMenu.Hide();
            UiLoadMenu.Hide();
            LightMenu.Show(LightMenuItems, GetLightMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the UI Show submenu anchored beside the Show entry inside the UI menu.
        /// </summary>
        void ShowUiShowMenu() {
            if (!UiMenu.IsVisible) {
                return;
            }

            FileMenu.Hide();
            AddMenu.Hide();
            LightMenu.Hide();
            BuildMenu.Hide();
            UiSaveMenu.Hide();
            UiLoadMenu.Hide();
            UiShowMenu.Show(UiShowMenuItems, GetUiShowMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the UI Save submenu anchored beside the Save entry inside the UI menu.
        /// </summary>
        void ShowUiSaveMenu() {
            if (!UiMenu.IsVisible) {
                return;
            }

            FileMenu.Hide();
            AddMenu.Hide();
            LightMenu.Hide();
            BuildMenu.Hide();
            UiShowMenu.Hide();
            UiLoadMenu.Hide();
            UiSaveMenu.Show(UiSaveMenuItems, GetUiSaveMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the UI Load submenu anchored beside the Load entry inside the UI menu.
        /// </summary>
        void ShowUiLoadMenu() {
            if (!UiMenu.IsVisible) {
                return;
            }

            FileMenu.Hide();
            AddMenu.Hide();
            LightMenu.Hide();
            BuildMenu.Hide();
            UiShowMenu.Hide();
            UiSaveMenu.Hide();
            UiLoadMenu.Show(UiLoadMenuItems, GetUiLoadMenuPosition(), HostSize);
        }

        /// <summary>
        /// Hides the Light submenu without affecting the top-level Add menu.
        /// </summary>
        void HideLightMenu() {
            LightMenu.Hide();
        }

        /// <summary>
        /// Computes the top-left position used to open the File menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetFileMenuPosition() {
            int x = (int)Math.Round(FileMenuButtonEntity.Position.X);
            return new int2(x, Height);
        }

        /// <summary>
        /// Computes the top-left position used to open the Add menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetAddMenuPosition() {
            int x = (int)Math.Round(AddMenuButtonEntity.Position.X);
            return new int2(x, Height);
        }

        /// <summary>
        /// Computes the top-left position used to open the Light submenu from the Add menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetLightMenuPosition() {
            int x = AddMenu.Position.X + AddMenu.Size.X;
            int y = AddMenu.Position.Y + ContextMenu.PaddingY + (AddMenuLightItemIndex * (ContextMenu.RowHeight + ContextMenu.RowSpacing));
            return new int2(x, y);
        }

        /// <summary>
        /// Computes the top-left position used to open the Build menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetBuildMenuPosition() {
            int x = (int)Math.Round(BuildMenuButtonEntity.Position.X);
            return new int2(x, Height);
        }

        /// <summary>
        /// Computes the top-left position used to open the UI menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetUiMenuPosition() {
            int x = (int)Math.Round(UiMenuButtonEntity.Position.X);
            return new int2(x, Height);
        }

        /// <summary>
        /// Computes the top-left position used to open the UI Show submenu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetUiShowMenuPosition() {
            int x = UiMenu.Position.X + UiMenu.Size.X;
            int y = UiMenu.Position.Y + ContextMenu.PaddingY + (UiMenuShowItemIndex * (ContextMenu.RowHeight + ContextMenu.RowSpacing));
            return new int2(x, y);
        }

        /// <summary>
        /// Computes the top-left position used to open the UI Save submenu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetUiSaveMenuPosition() {
            int x = UiMenu.Position.X + UiMenu.Size.X;
            int y = UiMenu.Position.Y + ContextMenu.PaddingY + (UiMenuSaveItemIndex * (ContextMenu.RowHeight + ContextMenu.RowSpacing));
            return new int2(x, y);
        }

        /// <summary>
        /// Computes the top-left position used to open the UI Load submenu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetUiLoadMenuPosition() {
            int x = UiMenu.Position.X + UiMenu.Size.X;
            int y = UiMenu.Position.Y + ContextMenu.PaddingY + (UiMenuLoadItemIndex * (ContextMenu.RowHeight + ContextMenu.RowSpacing));
            return new int2(x, y);
        }

        /// <summary>
        /// Computes the top-left position used to open one contributed top-level menu.
        /// </summary>
        /// <param name="projectMenuState">Contributed menu whose context menu should be positioned.</param>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetProjectMenuPosition(EditorTitleBarProjectMenuState projectMenuState) {
            if (projectMenuState == null) {
                throw new ArgumentNullException(nameof(projectMenuState));
            }

            int x = (int)Math.Round(projectMenuState.ButtonEntity.Position.X);
            return new int2(x, Height);
        }

        /// <summary>
        /// Handles cursor interaction on the draggable title region.
        /// </summary>
        /// <param name="pos">Pointer position relative to the drag region.</param>
        /// <param name="delta">Pointer delta relative to the last event.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleTitleBarCursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state != PointerInteraction.Press) {
                return;
            }

            if (AreAnyMenusVisible()) {
                HideMenus();
                return;
            }

            long now = Environment.TickCount64;
            long elapsed = now - LastTitleBarClickTicks;
            bool isDoubleClick = elapsed <= TitleBarDoubleClickMs &&
                                 Math.Abs(pos.X - LastTitleBarClickPos.X) <= TitleBarDoubleClickDistance &&
                                 Math.Abs(pos.Y - LastTitleBarClickPos.Y) <= TitleBarDoubleClickDistance;

            LastTitleBarClickTicks = now;
            LastTitleBarClickPos = pos;

            if (isDoubleClick) {
                RaiseToggleMaximizeRequested();
                return;
            }

            RaiseDragRequested();
        }

        /// <summary>
        /// Raises the drag-request event when a host is listening.
        /// </summary>
        void RaiseDragRequested() {
            if (DragRequested != null) {
                DragRequested();
            }
        }

        /// <summary>
        /// Raises the maximize-toggle event after hiding the File menu.
        /// </summary>
        void HandleToggleMaximizeRequested() {
            HideMenus();
            RaiseToggleMaximizeRequested();
        }

        /// <summary>
        /// Raises the maximize-toggle event when a host is listening.
        /// </summary>
        void RaiseToggleMaximizeRequested() {
            if (ToggleMaximizeRequested != null) {
                ToggleMaximizeRequested();
            }
        }

        /// <summary>
        /// Raises the minimize event after hiding the File menu.
        /// </summary>
        void HandleMinimizeRequested() {
            HideMenus();
            if (MinimizeRequested != null) {
                MinimizeRequested();
            }
        }

        /// <summary>
        /// Raises the close event after hiding the File menu.
        /// </summary>
        void HandleCloseRequested() {
            HideMenus();
            if (CloseRequested != null) {
                CloseRequested();
            }
        }

        /// <summary>
        /// Raises the New Map command event.
        /// </summary>
        void RaiseNewMapRequested() {
            if (NewMapRequested != null) {
                NewMapRequested();
            }
        }

        /// <summary>
        /// Raises the Open Map command event.
        /// </summary>
        void RaiseOpenMapRequested() {
            if (OpenMapRequested != null) {
                OpenMapRequested();
            }
        }

        /// <summary>
        /// Raises the Save Map command event.
        /// </summary>
        void RaiseSaveMapRequested() {
            if (SaveMapRequested != null) {
                SaveMapRequested();
            }
        }

        /// <summary>
        /// Raises the Save Map As command event.
        /// </summary>
        void RaiseSaveMapAsRequested() {
            if (SaveMapAsRequested != null) {
                SaveMapAsRequested();
            }
        }

        /// <summary>
        /// Raises the Preferences command event.
        /// </summary>
        void RaisePreferencesRequested() {
            HideMenus();
            if (PreferencesRequested != null) {
                PreferencesRequested();
            }
        }

        /// <summary>
        /// Raises the Scene Settings command event.
        /// </summary>
        void RaiseSceneSettingsRequested() {
            HideMenus();
            if (SceneSettingsRequested != null) {
                SceneSettingsRequested();
            }
        }

        /// <summary>
        /// Raises the Add Empty command event.
        /// </summary>
        void RaiseAddEmptyRequested() {
            HideMenus();
            if (AddEmptyRequested != null) {
                AddEmptyRequested();
            }
        }

        /// <summary>
        /// Raises the Add Cube command event.
        /// </summary>
        void RaiseAddCubeRequested() {
            HideMenus();
            if (AddCubeRequested != null) {
                AddCubeRequested();
            }
        }

        /// <summary>
        /// Raises the Add Plane command event.
        /// </summary>
        void RaiseAddPlaneRequested() {
            HideMenus();
            if (AddPlaneRequested != null) {
                AddPlaneRequested();
            }
        }

        /// <summary>
        /// Raises the Add Camera command event.
        /// </summary>
        void RaiseAddCameraRequested() {
            HideMenus();
            if (AddCameraRequested != null) {
                AddCameraRequested();
            }
        }

        /// <summary>
        /// Raises the Add Spot Light command event after closing title-bar menus.
        /// </summary>
        void RaiseAddSpotLightRequested() {
            HideMenus();
            if (AddSpotLightRequested != null) {
                AddSpotLightRequested();
            }
        }

        /// <summary>
        /// Raises the Add Point Light command event after closing title-bar menus.
        /// </summary>
        void RaiseAddPointLightRequested() {
            HideMenus();
            if (AddPointLightRequested != null) {
                AddPointLightRequested();
            }
        }

        /// <summary>
        /// Raises the Add Directional Light command event after closing title-bar menus.
        /// </summary>
        void RaiseAddDirectionalLightRequested() {
            HideMenus();
            if (AddDirectionalLightRequested != null) {
                AddDirectionalLightRequested();
            }
        }

        /// <summary>
        /// Raises the Build Settings command event.
        /// </summary>
        void RaiseBuildSettingsRequested() {
            HideMenus();
            if (BuildSettingsRequested != null) {
                BuildSettingsRequested();
            }
        }

        /// <summary>
        /// Raises the Platforms command event while also forwarding the legacy Build Settings event during migration.
        /// </summary>
        void RaisePlatformsRequested() {
            HideMenus();
            if (PlatformsRequested != null) {
                PlatformsRequested();
            }
            if (BuildSettingsRequested != null) {
                BuildSettingsRequested();
            }
        }

        /// <summary>
        /// Raises the Build command event.
        /// </summary>
        void RaiseBuildRequested() {
            HideMenus();
            if (BuildRequested != null) {
                BuildRequested();
            }
        }

        /// <summary>
        /// Raises the Profiles command event.
        /// </summary>
        void RaiseProfilesRequested() {
            HideMenus();
            if (ProfilesRequested != null) {
                ProfilesRequested();
            }
        }

        /// <summary>
        /// Raises the Build Scripts command event.
        /// </summary>
        void RaiseBuildScriptsRequested() {
            HideMenus();
            if (BuildScriptsRequested != null) {
                BuildScriptsRequested();
            }
        }

        /// <summary>
        /// Raises the Open in IDE command event.
        /// </summary>
        void RaiseOpenInIDERequested() {
            HideMenus();
            if (OpenInIDERequested != null) {
                OpenInIDERequested();
            }
        }

        /// <summary>
        /// Raises one contributed project-menu activation event.
        /// </summary>
        /// <param name="menuItemId">Stable contributed menu item identifier.</param>
        void RaiseProjectMenuItemRequested(string menuItemId) {
            if (string.IsNullOrWhiteSpace(menuItemId)) {
                throw new ArgumentException("Project menu item id must be provided.", nameof(menuItemId));
            }
            if (!ProjectMenuItemsById.ContainsKey(menuItemId)) {
                throw new InvalidOperationException($"Project menu item '{menuItemId}' is not available.");
            }

            HideMenus();
            if (ProjectMenuItemRequested != null) {
                ProjectMenuItemRequested(menuItemId);
            }
        }

        /// <summary>
        /// Raises one built-in UI menu action after closing open menus.
        /// </summary>
        /// <param name="action">UI menu action requested by the user.</param>
        void RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction action) {
            HideMenus();
            if (UiMenuActionRequested != null) {
                UiMenuActionRequested(action);
            }
        }

        /// <summary>
        /// Hides every contributed top-level project menu currently rendered by the title bar.
        /// </summary>
        void HideProjectMenus() {
            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                ProjectMenuStates[index].Menu.Hide();
            }
        }

        /// <summary>
        /// Returns true when any top-level title-bar menu other than the supplied identifier is visible.
        /// </summary>
        /// <param name="excludedTopLevelMenuId">Stable top-level menu identifier that should be ignored.</param>
        /// <returns>True when another top-level menu is visible; otherwise false.</returns>
        bool IsAnyOtherTopLevelMenuVisible(string excludedTopLevelMenuId) {
            if (!string.Equals(excludedTopLevelMenuId, "file", StringComparison.OrdinalIgnoreCase) && FileMenu.IsVisible) {
                return true;
            }
            if (!string.Equals(excludedTopLevelMenuId, "add", StringComparison.OrdinalIgnoreCase) && AddMenu.IsVisible) {
                return true;
            }
            if (!string.Equals(excludedTopLevelMenuId, "build", StringComparison.OrdinalIgnoreCase) && BuildMenu.IsVisible) {
                return true;
            }
            if (!string.Equals(excludedTopLevelMenuId, "ui", StringComparison.OrdinalIgnoreCase) && UiMenu.IsVisible) {
                return true;
            }

            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                EditorTitleBarProjectMenuState projectMenuState = ProjectMenuStates[index];
                if (!string.Equals(projectMenuState.TopLevelMenuId, excludedTopLevelMenuId, StringComparison.OrdinalIgnoreCase)
                    && projectMenuState.Menu.IsVisible) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves one contributed top-level project menu state by its stable identifier.
        /// </summary>
        /// <param name="topLevelMenuId">Stable contributed top-level menu identifier.</param>
        /// <returns>Resolved contributed top-level project menu state.</returns>
        EditorTitleBarProjectMenuState GetProjectMenuState(string topLevelMenuId) {
            if (string.IsNullOrWhiteSpace(topLevelMenuId)) {
                throw new ArgumentException("Top-level menu id must be provided.", nameof(topLevelMenuId));
            }

            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                EditorTitleBarProjectMenuState projectMenuState = ProjectMenuStates[index];
                if (string.Equals(projectMenuState.TopLevelMenuId, topLevelMenuId, StringComparison.OrdinalIgnoreCase)) {
                    return projectMenuState;
                }
            }

            throw new InvalidOperationException($"Project menu '{topLevelMenuId}' is not available.");
        }

        /// <summary>
        /// Computes the current right edge of the top-level menu strip before the title region begins.
        /// </summary>
        /// <returns>Right edge of the rightmost top-level menu button in pixels.</returns>
        float GetMenuStripRightEdge() {
            if (ProjectMenuStates.Count > 0) {
                EditorTitleBarProjectMenuState lastProjectMenuState = ProjectMenuStates[ProjectMenuStates.Count - 1];
                return lastProjectMenuState.ButtonEntity.Position.X + lastProjectMenuState.ButtonWidth;
            }

            return UiMenuButtonEntity.Position.X + UiMenuButtonWidth;
        }

        /// <summary>
        /// Returns true when any title-bar menu or submenu is currently visible.
        /// </summary>
        /// <returns>True when at least one title-bar menu is visible.</returns>
        bool AreAnyMenusVisible() {
            if (FileMenu.IsVisible || AddMenu.IsVisible || LightMenu.IsVisible || BuildMenu.IsVisible || UiMenu.IsVisible || UiShowMenu.IsVisible || UiSaveMenu.IsVisible || UiLoadMenu.IsVisible) {
                return true;
            }

            for (int index = 0; index < ProjectMenuStates.Count; index++) {
                if (ProjectMenuStates[index].Menu.IsVisible) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates one no-argument action that raises the supplied UI menu action.
        /// </summary>
        /// <param name="action">Built-in UI menu action to raise.</param>
        /// <returns>Action delegate that raises the supplied UI menu action.</returns>
        Action CreateUiMenuActionHandler(EditorTitleBarUiMenuAction action) {
            if (action == EditorTitleBarUiMenuAction.ShowViewport) {
                return RaiseShowViewportRequested;
            }
            if (action == EditorTitleBarUiMenuAction.ShowSceneHierarchy) {
                return RaiseShowSceneHierarchyRequested;
            }
            if (action == EditorTitleBarUiMenuAction.ShowAssetBrowser) {
                return RaiseShowAssetBrowserRequested;
            }
            if (action == EditorTitleBarUiMenuAction.ShowProperties) {
                return RaiseShowPropertiesRequested;
            }
            if (action == EditorTitleBarUiMenuAction.ShowLogger) {
                return RaiseShowLoggerRequested;
            }
            if (action == EditorTitleBarUiMenuAction.ShowPreview) {
                return RaiseShowPreviewRequested;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot1) {
                return RaiseSaveSlot1Requested;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot2) {
                return RaiseSaveSlot2Requested;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot3) {
                return RaiseSaveSlot3Requested;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot4) {
                return RaiseSaveSlot4Requested;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot5) {
                return RaiseSaveSlot5Requested;
            }
            if (action == EditorTitleBarUiMenuAction.LoadSlot1) {
                return RaiseLoadSlot1Requested;
            }
            if (action == EditorTitleBarUiMenuAction.LoadSlot2) {
                return RaiseLoadSlot2Requested;
            }
            if (action == EditorTitleBarUiMenuAction.LoadSlot3) {
                return RaiseLoadSlot3Requested;
            }
            if (action == EditorTitleBarUiMenuAction.LoadSlot4) {
                return RaiseLoadSlot4Requested;
            }

            return RaiseLoadSlot5Requested;
        }

        /// <summary>
        /// Resolves one built-in UI menu action from its stable identifier.
        /// </summary>
        /// <param name="menuItemId">Stable UI menu item identifier.</param>
        /// <returns>Resolved built-in UI menu action.</returns>
        EditorTitleBarUiMenuAction ResolveUiMenuAction(string menuItemId) {
            if (string.IsNullOrWhiteSpace(menuItemId)) {
                throw new ArgumentException("UI menu item id must be provided.", nameof(menuItemId));
            }

            if (string.Equals(menuItemId, "show-viewport", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.ShowViewport;
            }
            if (string.Equals(menuItemId, "show-scene-hierarchy", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.ShowSceneHierarchy;
            }
            if (string.Equals(menuItemId, "show-asset-browser", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.ShowAssetBrowser;
            }
            if (string.Equals(menuItemId, "show-properties", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.ShowProperties;
            }
            if (string.Equals(menuItemId, "show-logger", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.ShowLogger;
            }
            if (string.Equals(menuItemId, "show-preview", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.ShowPreview;
            }
            if (string.Equals(menuItemId, "save-slot-1", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.SaveSlot1;
            }
            if (string.Equals(menuItemId, "save-slot-2", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.SaveSlot2;
            }
            if (string.Equals(menuItemId, "save-slot-3", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.SaveSlot3;
            }
            if (string.Equals(menuItemId, "save-slot-4", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.SaveSlot4;
            }
            if (string.Equals(menuItemId, "save-slot-5", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.SaveSlot5;
            }
            if (string.Equals(menuItemId, "load-slot-1", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.LoadSlot1;
            }
            if (string.Equals(menuItemId, "load-slot-2", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.LoadSlot2;
            }
            if (string.Equals(menuItemId, "load-slot-3", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.LoadSlot3;
            }
            if (string.Equals(menuItemId, "load-slot-4", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.LoadSlot4;
            }
            if (string.Equals(menuItemId, "load-slot-5", StringComparison.OrdinalIgnoreCase)) {
                return EditorTitleBarUiMenuAction.LoadSlot5;
            }

            throw new InvalidOperationException($"UI menu item '{menuItemId}' is not available.");
        }

        /// <summary>
        /// Raises the UI action used to open one new viewport panel instance.
        /// </summary>
        void RaiseShowViewportRequested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.ShowViewport);
        }

        /// <summary>
        /// Raises the UI action used to open one new scene hierarchy panel instance.
        /// </summary>
        void RaiseShowSceneHierarchyRequested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.ShowSceneHierarchy);
        }

        /// <summary>
        /// Raises the UI action used to open one new asset browser panel instance.
        /// </summary>
        void RaiseShowAssetBrowserRequested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.ShowAssetBrowser);
        }

        /// <summary>
        /// Raises the UI action used to open one new properties panel instance.
        /// </summary>
        void RaiseShowPropertiesRequested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.ShowProperties);
        }

        /// <summary>
        /// Raises the UI action used to open one new logger panel instance.
        /// </summary>
        void RaiseShowLoggerRequested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.ShowLogger);
        }

        /// <summary>
        /// Raises the UI action used to open one new preview panel instance.
        /// </summary>
        void RaiseShowPreviewRequested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.ShowPreview);
        }

        /// <summary>
        /// Raises the UI action used to save the current workspace to slot 1.
        /// </summary>
        void RaiseSaveSlot1Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.SaveSlot1);
        }

        /// <summary>
        /// Raises the UI action used to save the current workspace to slot 2.
        /// </summary>
        void RaiseSaveSlot2Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.SaveSlot2);
        }

        /// <summary>
        /// Raises the UI action used to save the current workspace to slot 3.
        /// </summary>
        void RaiseSaveSlot3Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.SaveSlot3);
        }

        /// <summary>
        /// Raises the UI action used to save the current workspace to slot 4.
        /// </summary>
        void RaiseSaveSlot4Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.SaveSlot4);
        }

        /// <summary>
        /// Raises the UI action used to save the current workspace to slot 5.
        /// </summary>
        void RaiseSaveSlot5Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.SaveSlot5);
        }

        /// <summary>
        /// Raises the UI action used to load the workspace from slot 1.
        /// </summary>
        void RaiseLoadSlot1Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.LoadSlot1);
        }

        /// <summary>
        /// Raises the UI action used to load the workspace from slot 2.
        /// </summary>
        void RaiseLoadSlot2Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.LoadSlot2);
        }

        /// <summary>
        /// Raises the UI action used to load the workspace from slot 3.
        /// </summary>
        void RaiseLoadSlot3Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.LoadSlot3);
        }

        /// <summary>
        /// Raises the UI action used to load the workspace from slot 4.
        /// </summary>
        void RaiseLoadSlot4Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.LoadSlot4);
        }

        /// <summary>
        /// Raises the UI action used to load the workspace from slot 5.
        /// </summary>
        void RaiseLoadSlot5Requested() {
            RaiseUiMenuActionRequested(EditorTitleBarUiMenuAction.LoadSlot5);
        }

        /// <summary>
        /// Computes a button width based on tight font metrics with added padding.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <returns>Calculated button width.</returns>
        int ComputeButtonWidth(string label) {
            FontTightMetrics tightMetrics = Font.MeasureTight(label);
            return Math.Max(GetMinimumTitleWidth(), (int)Math.Ceiling(tightMetrics.Width) + Metrics.ScalePixels(16));
        }

        /// <summary>
        /// Computes the vertical offset needed to center the title label.
        /// </summary>
        /// <returns>Top offset for the title label.</returns>
        float GetTitleVerticalOffset() {
            float lineHeight = Math.Max(Font.LineHeight, 1f);
            return (Height - lineHeight) * 0.5f;
        }

        /// <summary>
        /// Gets the scaled width reserved for the title-bar icon slot.
        /// </summary>
        /// <returns>Scaled width reserved for the title-bar icon slot.</returns>
        int GetLeftIconSlotWidth() {
            return Height;
        }

        /// <summary>
        /// Gets the scaled height used for title-bar buttons.
        /// </summary>
        /// <returns>Scaled title-bar button height in pixels.</returns>
        int GetButtonHeight() {
            return Height;
        }

        /// <summary>
        /// Gets the scaled width of vertical separators drawn on title-bar buttons.
        /// </summary>
        /// <returns>Scaled separator width in pixels.</returns>
        int GetButtonBorderWidth() {
            return Metrics.ScalePixels(ButtonBorderWidth);
        }

        /// <summary>
        /// Gets the scaled spacing inserted between the menu strip and the title text.
        /// </summary>
        /// <returns>Scaled title spacing in pixels.</returns>
        int GetTitleSpacing() {
            return Metrics.ScalePixels(TitleSpacing);
        }

        /// <summary>
        /// Gets the scaled minimum width reserved for the title label.
        /// </summary>
        /// <returns>Scaled minimum title width in pixels.</returns>
        int GetMinimumTitleWidth() {
            return Metrics.ScalePixels(MinimumTitleWidth);
        }
    }
}
