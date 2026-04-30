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
        public const int HeightPixels = 36;

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
        readonly FontAsset Font;
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
        /// Entity that hosts the File menu trigger button.
        /// </summary>
        readonly EditorEntity FileMenuButtonEntity;
        /// <summary>
        /// Width reserved for the File menu trigger button.
        /// </summary>
        readonly int FileMenuButtonWidth;
        /// <summary>
        /// Entity that hosts the Add menu trigger button.
        /// </summary>
        readonly EditorEntity AddMenuButtonEntity;
        /// <summary>
        /// Width reserved for the Add menu trigger button.
        /// </summary>
        readonly int AddMenuButtonWidth;
        /// <summary>
        /// Entity that hosts the Build menu trigger button.
        /// </summary>
        readonly EditorEntity BuildMenuButtonEntity;
        /// <summary>
        /// Width reserved for the Build menu trigger button.
        /// </summary>
        readonly int BuildMenuButtonWidth;
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
        /// Context menu shown when the Build button is activated.
        /// </summary>
        readonly ContextMenu BuildMenu;
        /// <summary>
        /// Items displayed by the Build context menu.
        /// </summary>
        readonly IReadOnlyList<ContextMenuItem> BuildMenuItems;
        /// <summary>
        /// Entity that hosts the minimize control.
        /// </summary>
        readonly EditorEntity MinimizeButtonEntity;
        /// <summary>
        /// Width reserved for the minimize control.
        /// </summary>
        readonly int MinimizeButtonWidth;
        /// <summary>
        /// Entity that hosts the maximize control.
        /// </summary>
        readonly EditorEntity MaximizeButtonEntity;
        /// <summary>
        /// Width reserved for the maximize control.
        /// </summary>
        readonly int MaximizeButtonWidth;
        /// <summary>
        /// Entity that hosts the close control.
        /// </summary>
        readonly EditorEntity CloseButtonEntity;
        /// <summary>
        /// Width reserved for the close control.
        /// </summary>
        readonly int CloseButtonWidth;
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
        public EditorTitleBar(FontAsset font, int windowWidth, int windowHeight, string titleText) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            TitleValue = titleText ?? string.Empty;
            BackgroundOrder = RenderOrder2D.PanelSurface;
            TextOrder = RenderOrder2D.PanelForeground;
            InputSurfaceOrder = RenderOrder2D.OverlayInput;
            HostSize = new int2(Math.Max(1, windowWidth), Math.Max(HeightPixels, windowHeight));

            RootEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = TitleBarLayerMask,
                Position = float3.Zero
            };

            Background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                Size = new int2(HostSize.X, HeightPixels),
                RenderOrder2D = BackgroundOrder
            };
            RootEntity.AddComponent(Background);

            HoverShieldEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = float3.Zero
            };
            RootEntity.AddChild(HoverShieldEntity);

            HoverShieldSurface = CreateInputSurface(new int2(HostSize.X, HeightPixels));
            HoverShieldEntity.AddComponent(HoverShieldSurface);

            HoverShieldInteractable = new InteractableComponent {
                Size = new int2(HostSize.X, HeightPixels)
            };
            HoverShieldEntity.AddComponent(HoverShieldInteractable);

            DragRegionEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = float3.Zero
            };
            RootEntity.AddChild(DragRegionEntity);

            DragRegion = new InteractableComponent {
                Size = new int2(0, HeightPixels)
            };
            DragRegion.CursorEvent += HandleTitleBarCursorEvent;
            DragRegionEntity.AddComponent(DragRegion);
            DragRegionInputSurface = CreateInputSurface(new int2(0, HeightPixels));
            DragRegionEntity.AddComponent(DragRegionInputSurface);

            FileMenuButtonEntity = CreateTitleBarButton("File", ToggleFileMenu, HandleFileMenuButtonHovered, true, false, out int fileMenuButtonWidth);
            FileMenuButtonWidth = fileMenuButtonWidth;
            AddMenuButtonEntity = CreateTitleBarButton("Add", ToggleAddMenu, HandleAddMenuButtonHovered, true, true, out int addMenuButtonWidth);
            AddMenuButtonWidth = addMenuButtonWidth;
            BuildMenuButtonEntity = CreateTitleBarButton("Build", ToggleBuildMenu, HandleBuildMenuButtonHovered, false, true, out int buildMenuButtonWidth);
            BuildMenuButtonWidth = buildMenuButtonWidth;

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

            byte menuBackgroundOrder = RenderOrder2D.OverlayBackground;
            byte menuTextOrder = RenderOrder2D.OverlayForeground;
            FileMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(FileMenu.Entity);
            FileMenuItems = BuildFileMenuItems();
            AddMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(AddMenu.Entity);
            AddMenuItems = BuildAddMenuItems();
            BuildMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
            RootEntity.AddChild(BuildMenu.Entity);
            BuildMenuItems = BuildBuildMenuItems();

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
        public int Height => HeightPixels;

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
        /// Raised when the user selects the Build Settings command.
        /// </summary>
        public event Action BuildSettingsRequested;
        /// <summary>
        /// Raised when the user selects the Build command.
        /// </summary>
        public event Action BuildRequested;

        /// <summary>
        /// Updates button placement, menu clamping, and title sizing to fit the provided host size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            int width = Math.Max(1, windowWidth);
            int height = Math.Max(HeightPixels, windowHeight);
            HostSize = new int2(width, height);

            Background.Size = new int2(width + 1, HeightPixels);
            HoverShieldSurface.Size = new int2(width, HeightPixels);
            HoverShieldInteractable.Size = new int2(width, HeightPixels);

            float fileButtonX = LeftIconSlotWidth;
            FileMenuButtonEntity.Position = new float3(fileButtonX, ButtonTop, 0f);
            float addButtonX = fileButtonX + FileMenuButtonWidth + ButtonSpacing;
            AddMenuButtonEntity.Position = new float3(addButtonX, ButtonTop, 0f);
            float buildButtonX = addButtonX + AddMenuButtonWidth + ButtonSpacing;
            BuildMenuButtonEntity.Position = new float3(buildButtonX, ButtonTop, 0f);

            int totalControlsWidth = MinimizeButtonWidth + MaximizeButtonWidth + CloseButtonWidth + (ButtonSpacing * 2);
            float titleX = buildButtonX + BuildMenuButtonWidth + ButtonSpacing + TitleSpacing;
            float controlStartX = Math.Max(0, width - totalControlsWidth);

            TitleEntity.Position = new float3(titleX, GetTitleVerticalOffset(), 0f);
            int titleWidth = Math.Max(1, (int)Math.Floor(controlStartX - titleX - TitleSpacing));
            TitleTextComponent.Size = new int2(titleWidth, TitleTextComponent.Size.Y);

            LayoutWindowControls(controlStartX);
            UpdateDragRegion(controlStartX);
            FileMenu.UpdateLayout(HostSize);
            AddMenu.UpdateLayout(HostSize);
            BuildMenu.UpdateLayout(HostSize);
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
                new ContextMenuItem("Save Map As...", RaiseSaveMapAsRequested)
            };
        }

        /// <summary>
        /// Creates the Add menu items shown beside the File button.
        /// </summary>
        /// <returns>Immutable collection of Add menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildAddMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Empty", RaiseAddEmptyRequested),
                new ContextMenuItem("Cube", RaiseAddCubeRequested),
                new ContextMenuItem("Plane", RaiseAddPlaneRequested)
            };
        }

        /// <summary>
        /// Creates the Build menu items shown beside the Add button.
        /// </summary>
        /// <returns>Immutable collection of Build menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildBuildMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("Build Platforms...", RaiseBuildSettingsRequested),
                new ContextMenuItem("Build...", RaiseBuildRequested)
            };
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

            ButtonComponent button = new ButtonComponent(label, new int2(width, ButtonHeight), Font, onClick, 0f);
            if (onHover != null) {
                button.Hovered += onHover;
            }
            button.SetRenderOrders(BackgroundOrder, TextOrder);
            button.UseHoverOnlyBackground();
            button.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            button.UseSquareCorners();
            buttonEntity.AddComponent(button);
            buttonEntity.AddComponent(CreateInputSurface(new int2(width, ButtonHeight)));
            AddTitleBarButtonVerticalBorders(buttonEntity, width, includeLeftBorder, includeRightBorder);
            RootEntity.AddChild(buttonEntity);
            return buttonEntity;
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
                AddTitleBarButtonVerticalBorderLine(buttonEntity, width - ButtonBorderWidth);
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
                Size = new int2(ButtonBorderWidth, ButtonHeight),
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
            float dragRegionX = LeftIconSlotWidth + FileMenuButtonWidth + ButtonSpacing + AddMenuButtonWidth + ButtonSpacing + BuildMenuButtonWidth + ButtonSpacing;
            int dragRegionWidth = Math.Max(0, (int)Math.Floor(controlStartX - dragRegionX - ButtonSpacing));

            DragRegionEntity.Position = new float3(dragRegionX, 0f, 0f);
            DragRegion.Size = new int2(dragRegionWidth, HeightPixels);
            DragRegionInputSurface.Size = new int2(dragRegionWidth, HeightPixels);
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
            BuildMenu.Hide();
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
                AddMenu.Hide();
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
        /// Switches to the File menu when hovering across an already active menu strip.
        /// </summary>
        void HandleFileMenuButtonHovered() {
            if (!AddMenu.IsVisible && !BuildMenu.IsVisible) {
                return;
            }

            ShowFileMenu();
        }

        /// <summary>
        /// Switches to the Add menu when hovering across an already active menu strip.
        /// </summary>
        void HandleAddMenuButtonHovered() {
            if (!FileMenu.IsVisible && !BuildMenu.IsVisible) {
                return;
            }

            ShowAddMenu();
        }

        /// <summary>
        /// Switches to the Build menu when hovering across an already active menu strip.
        /// </summary>
        void HandleBuildMenuButtonHovered() {
            if (!FileMenu.IsVisible && !AddMenu.IsVisible) {
                return;
            }

            ShowBuildMenu();
        }

        /// <summary>
        /// Shows the File menu and closes any other title-bar menu.
        /// </summary>
        void ShowFileMenu() {
            AddMenu.Hide();
            BuildMenu.Hide();
            FileMenu.Show(FileMenuItems, GetFileMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the Add menu and closes any other title-bar menu.
        /// </summary>
        void ShowAddMenu() {
            FileMenu.Hide();
            BuildMenu.Hide();
            AddMenu.Show(AddMenuItems, GetAddMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows the Build menu and closes any other title-bar menu.
        /// </summary>
        void ShowBuildMenu() {
            FileMenu.Hide();
            AddMenu.Hide();
            BuildMenu.Show(BuildMenuItems, GetBuildMenuPosition(), HostSize);
        }

        /// <summary>
        /// Computes the top-left position used to open the File menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetFileMenuPosition() {
            int x = (int)Math.Round(FileMenuButtonEntity.Position.X);
            return new int2(x, HeightPixels);
        }

        /// <summary>
        /// Computes the top-left position used to open the Add menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetAddMenuPosition() {
            int x = (int)Math.Round(AddMenuButtonEntity.Position.X);
            return new int2(x, HeightPixels);
        }

        /// <summary>
        /// Computes the top-left position used to open the Build menu.
        /// </summary>
        /// <returns>Menu position relative to the title bar root.</returns>
        int2 GetBuildMenuPosition() {
            int x = (int)Math.Round(BuildMenuButtonEntity.Position.X);
            return new int2(x, HeightPixels);
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

            if (FileMenu.IsVisible || AddMenu.IsVisible || BuildMenu.IsVisible) {
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
        /// Raises the Add Empty command event.
        /// </summary>
        void RaiseAddEmptyRequested() {
            if (AddEmptyRequested != null) {
                AddEmptyRequested();
            }
        }

        /// <summary>
        /// Raises the Add Cube command event.
        /// </summary>
        void RaiseAddCubeRequested() {
            if (AddCubeRequested != null) {
                AddCubeRequested();
            }
        }

        /// <summary>
        /// Raises the Add Plane command event.
        /// </summary>
        void RaiseAddPlaneRequested() {
            if (AddPlaneRequested != null) {
                AddPlaneRequested();
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
        /// Raises the Build command event.
        /// </summary>
        void RaiseBuildRequested() {
            HideMenus();
            if (BuildRequested != null) {
                BuildRequested();
            }
        }

        /// <summary>
        /// Computes a button width based on tight font metrics with added padding.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <returns>Calculated button width.</returns>
        int ComputeButtonWidth(string label) {
            FontTightMetrics tightMetrics = Font.MeasureTight(label);
            return Math.Max(40, (int)Math.Ceiling(tightMetrics.Width) + 16);
        }

        /// <summary>
        /// Computes the vertical offset needed to center the title label.
        /// </summary>
        /// <returns>Top offset for the title label.</returns>
        float GetTitleVerticalOffset() {
            float lineHeight = Math.Max(Font.LineHeight, 1f);
            return (HeightPixels - lineHeight) * 0.5f;
        }
    }
}
