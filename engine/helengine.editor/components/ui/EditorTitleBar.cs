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
        /// Padding applied at the left and right edges of the title bar.
        /// </summary>
        const int EdgePadding = 8;
        /// <summary>
        /// Top offset used for title bar buttons.
        /// </summary>
        const int ButtonTop = 6;
        /// <summary>
        /// Height used for title bar buttons.
        /// </summary>
        const int ButtonHeight = 24;
        /// <summary>
        /// Horizontal spacing between title bar controls.
        /// </summary>
        const int ButtonSpacing = 6;
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
        /// Entity that hosts the draggable title bar hit region.
        /// </summary>
        readonly EditorEntity DragRegionEntity;
        /// <summary>
        /// Interactable used for title bar drag and maximize gestures.
        /// </summary>
        readonly InteractableComponent DragRegion;
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
            BackgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            TextOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);
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

            FileMenuButtonEntity = CreateTitleBarButton("File", ToggleFileMenu, out int fileMenuButtonWidth);
            FileMenuButtonWidth = fileMenuButtonWidth;
            AddMenuButtonEntity = CreateTitleBarButton("Add", ToggleAddMenu, out int addMenuButtonWidth);
            AddMenuButtonWidth = addMenuButtonWidth;

            TitleEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(0f, GetTitleVerticalOffset(), 0f)
            };
            RootEntity.AddChild(TitleEntity);

            int titleHeight = (int)Math.Ceiling(Math.Max(Font.LineHeight, 1f));
            TitleTextComponent = new TextComponent {
                Font = Font,
                Text = TitleValue,
                Color = ThemeManager.Colors.TextPrimary,
                Size = new int2(Math.Max(1, HostSize.X), titleHeight),
                RenderOrder2D = TextOrder
            };
            TitleEntity.AddComponent(TitleTextComponent);

            FileMenu = new ContextMenu(Font, TitleBarLayerMask, BackgroundOrder, TextOrder);
            RootEntity.AddChild(FileMenu.Entity);
            FileMenuItems = BuildFileMenuItems();
            AddMenu = new ContextMenu(Font, TitleBarLayerMask, BackgroundOrder, TextOrder);
            RootEntity.AddChild(AddMenu.Entity);
            AddMenuItems = BuildAddMenuItems();

            MinimizeButtonEntity = CreateTitleBarButton("-", HandleMinimizeRequested, out int minimizeButtonWidth);
            MinimizeButtonWidth = minimizeButtonWidth;
            MaximizeButtonEntity = CreateTitleBarButton("Max", HandleToggleMaximizeRequested, out int maximizeButtonWidth);
            MaximizeButtonWidth = maximizeButtonWidth;
            CloseButtonEntity = CreateTitleBarButton("X", HandleCloseRequested, out int closeButtonWidth);
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
        /// Updates button placement, menu clamping, and title sizing to fit the provided host size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            int width = Math.Max(1, windowWidth);
            int height = Math.Max(HeightPixels, windowHeight);
            HostSize = new int2(width, height);

            Background.Size = new int2(width + 1, HeightPixels);

            float fileButtonX = EdgePadding;
            FileMenuButtonEntity.Position = new float3(fileButtonX, ButtonTop, 0f);
            float addButtonX = fileButtonX + FileMenuButtonWidth + ButtonSpacing;
            AddMenuButtonEntity.Position = new float3(addButtonX, ButtonTop, 0f);

            int totalControlsWidth = MinimizeButtonWidth + MaximizeButtonWidth + CloseButtonWidth + (ButtonSpacing * 2);
            float titleX = addButtonX + AddMenuButtonWidth + ButtonSpacing + TitleSpacing;
            float controlStartX = Math.Max(titleX + MinimumTitleWidth, width - totalControlsWidth - EdgePadding);

            TitleEntity.Position = new float3(titleX, GetTitleVerticalOffset(), 0f);
            int titleWidth = Math.Max(1, (int)Math.Floor(controlStartX - titleX - TitleSpacing));
            TitleTextComponent.Size = new int2(titleWidth, TitleTextComponent.Size.Y);

            LayoutWindowControls(controlStartX);
            UpdateDragRegion(controlStartX);
            FileMenu.UpdateLayout(HostSize);
            AddMenu.UpdateLayout(HostSize);
        }

        /// <summary>
        /// Creates the File menu items shown on the left side of the title bar.
        /// </summary>
        /// <returns>Immutable collection of File menu items.</returns>
        IReadOnlyList<ContextMenuItem> BuildFileMenuItems() {
            return new ContextMenuItem[] {
                new ContextMenuItem("New Map", RaiseNewMapRequested),
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
        /// Creates a title bar button with the standard title bar styling.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <param name="onClick">Callback invoked when the button is activated.</param>
        /// <param name="width">Computed button width.</param>
        /// <returns>Entity hosting the created button.</returns>
        EditorEntity CreateTitleBarButton(string label, Action onClick, out int width) {
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
            button.SetRenderOrders(BackgroundOrder, TextOrder);
            buttonEntity.AddComponent(button);
            RootEntity.AddChild(buttonEntity);
            return buttonEntity;
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
            float dragRegionX = EdgePadding + FileMenuButtonWidth + ButtonSpacing + AddMenuButtonWidth + ButtonSpacing;
            int dragRegionWidth = Math.Max(0, (int)Math.Floor(controlStartX - dragRegionX - ButtonSpacing));

            DragRegionEntity.Position = new float3(dragRegionX, 0f, 0f);
            DragRegion.Size = new int2(dragRegionWidth, HeightPixels);
        }

        /// <summary>
        /// Hides every title-bar context menu.
        /// </summary>
        void HideMenus() {
            FileMenu.Hide();
            AddMenu.Hide();
        }

        /// <summary>
        /// Shows or hides the File menu anchored beneath the File button.
        /// </summary>
        void ToggleFileMenu() {
            if (FileMenu.IsVisible) {
                FileMenu.Hide();
                return;
            }

            AddMenu.Hide();
            FileMenu.Show(FileMenuItems, GetFileMenuPosition(), HostSize);
        }

        /// <summary>
        /// Shows or hides the Add menu anchored beneath the Add button.
        /// </summary>
        void ToggleAddMenu() {
            if (AddMenu.IsVisible) {
                AddMenu.Hide();
                return;
            }

            FileMenu.Hide();
            AddMenu.Show(AddMenuItems, GetAddMenuPosition(), HostSize);
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
        /// Handles cursor interaction on the draggable title region.
        /// </summary>
        /// <param name="pos">Pointer position relative to the drag region.</param>
        /// <param name="delta">Pointer delta relative to the last event.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleTitleBarCursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state != PointerInteraction.Press) {
                return;
            }

            if (FileMenu.IsVisible || AddMenu.IsVisible) {
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
