using System;
using System.Collections.Generic;

namespace helengine.editor {
    /// <summary>
    /// Builds and manages the editor title bar UI while raising window control events for platform hosts.
    /// </summary>
    public class EditorTitleBar {
        /// <summary>
        /// Default title bar height in pixels.
        /// </summary>
        public const int HeightPixels = 36;

        const int TitleBarDoubleClickMs = 350;
        const int TitleBarDoubleClickDistance = 6;
        const ushort TitleBarLayerMask = 0b1000000000000000;

        FontAsset font;
        EditorEntity rootEntity;
        SpriteComponent background;
        InteractableComponent hitRegion;
        TextComponent titleTextComponent;
        List<(EditorEntity Entity, int Width)> menuButtons = new();
        List<(EditorEntity Entity, int Width)> windowControlButtons = new();

        long lastTitleBarClickTicks;
        int2 lastTitleBarClickPos;
        string title;

        /// <summary>
        /// Initializes the title bar UI with its menu buttons and window controls.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="windowWidth">Initial window width for layout.</param>
        /// <param name="titleText">Initial window title text.</param>
        public EditorTitleBar(FontAsset font, int windowWidth, string titleText) {
            this.font = font;
            title = titleText;

            rootEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(0, 0, 0)
            };

            background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                Size = new int2(windowWidth, HeightPixels),
                RenderOrder2D = 1
            };
            rootEntity.AddComponent(background);

            hitRegion = new InteractableComponent {
                Size = new int2(windowWidth, HeightPixels)
            };
            hitRegion.CursorEvent += HandleTitleBarCursorEvent;
            rootEntity.AddComponent(hitRegion);

            BuildMenuButtons();
            var titleEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(0, TitleVerticalOffset(), 0)
            };
            float lineHeight = MathF.Max(font.LineHeight, 1f);
            titleTextComponent = new TextComponent {
                Font = font,
                Text = titleText,
                Color = new byte4(255, 255, 255, 255),
                Size = new int2(300, (int)MathF.Ceiling(lineHeight)),
                RenderOrder2D = 3
            };
            titleEntity.AddComponent(titleTextComponent);
            rootEntity.AddChild(titleEntity);

            BuildWindowControls();

            UpdateLayout(windowWidth);
        }

        /// <summary>
        /// Gets the root entity representing the title bar.
        /// </summary>
        public EditorEntity Entity => rootEntity;

        /// <summary>
        /// Gets or sets the visible window title text.
        /// </summary>
        public string Title {
            get { return title; }
            set {
                title = value;
                titleTextComponent.Text = value;
            }
        }

        /// <summary>
        /// Gets the height of the title bar in pixels.
        /// </summary>
        public int Height => HeightPixels;

        /// <summary>
        /// Raised when the user initiates a window drag.
        /// </summary>
        public event Action? DragRequested;

        /// <summary>
        /// Raised when the user requests a maximize or restore via double-click or control.
        /// </summary>
        public event Action? ToggleMaximizeRequested;

        /// <summary>
        /// Raised when the user clicks the minimize control.
        /// </summary>
        public event Action? MinimizeRequested;

        /// <summary>
        /// Raised when the user clicks the close control.
        /// </summary>
        public event Action? CloseRequested;

        /// <summary>
        /// Updates button placement and background sizing to fit the provided window width.
        /// </summary>
        /// <param name="windowWidth">The current window width.</param>
        public void UpdateLayout(int windowWidth) {
            int fullWidth = windowWidth + 1;
            background.Size = new int2(fullWidth, HeightPixels);
            hitRegion.Size = new int2(fullWidth, HeightPixels);

            float x = 8f;
            for (int i = 0; i < menuButtons.Count; i++) {
                var entry = menuButtons[i];
                entry.Entity.Position = new float3(x, 6, 0);
                x += entry.Width + 6;
            }

            if (titleTextComponent.Parent != null) {
                titleTextComponent.Parent.Position = new float3(x + 10, TitleVerticalOffset(), 0);
            }

            int totalControlsWidth = 0;
            for (int i = 0; i < windowControlButtons.Count; i++) {
                totalControlsWidth += windowControlButtons[i].Width + 6;
            }

            float controlX = Math.Max(x + 20, windowWidth - totalControlsWidth - 8);
            for (int i = 0; i < windowControlButtons.Count; i++) {
                var entry = windowControlButtons[i];
                entry.Entity.Position = new float3(controlX, 6, 0);
                controlX += entry.Width + 6;
            }
        }

        /// <summary>
        /// Creates the menu buttons that sit on the left side of the title bar.
        /// </summary>
        void BuildMenuButtons() {
            menuButtons.Clear();
            float x = 8f;
            string[] labels = { "File", "Edit", "View", "Window", "Help" };
            for (int i = 0; i < labels.Length; i++) {
                string label = labels[i];
                int width = ComputeButtonWidth(label);
                var buttonEntity = new EditorEntity {
                    LayerMask = TitleBarLayerMask,
                    Position = new float3(x, 6, 0)
                };
                var button = new ButtonComponent(label, new int2(width, 24), font, null, 0f);
                buttonEntity.AddComponent(button);
                rootEntity.AddChild(buttonEntity);
                menuButtons.Add((buttonEntity, width));
                x += width + 6;
            }
        }

        /// <summary>
        /// Adds minimize, maximize, and close controls to the right side of the title bar.
        /// </summary>
        void BuildWindowControls() {
            windowControlButtons.Clear();
            int closeWidth = ComputeButtonWidth("X");
            int maxWidth = ComputeButtonWidth("Max");
            int minWidth = ComputeButtonWidth("-");

            AddWindowControl("-", minWidth, () => MinimizeRequested?.Invoke());
            AddWindowControl("Max", maxWidth, () => ToggleMaximizeRequested?.Invoke());
            AddWindowControl("X", closeWidth, () => CloseRequested?.Invoke());
        }

        /// <summary>
        /// Handles cursor interaction on the title bar surface to dispatch drag or toggle events.
        /// </summary>
        /// <param name="pos">Pointer position relative to the bar.</param>
        /// <param name="delta">Pointer delta relative to the last event.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleTitleBarCursorEvent(int2 pos, int2 delta, PointerInteraction state) {
            if (state != PointerInteraction.Press) {
                return;
            }

            long now = Environment.TickCount64;
            long elapsed = now - lastTitleBarClickTicks;
            bool isDoubleClick = elapsed <= TitleBarDoubleClickMs &&
                                 Math.Abs(pos.X - lastTitleBarClickPos.X) <= TitleBarDoubleClickDistance &&
                                 Math.Abs(pos.Y - lastTitleBarClickPos.Y) <= TitleBarDoubleClickDistance;

            lastTitleBarClickTicks = now;
            lastTitleBarClickPos = pos;

            if (isDoubleClick) {
                ToggleMaximizeRequested?.Invoke();
            } else {
                DragRequested?.Invoke();
            }
        }

        /// <summary>
        /// Adds a control button to the title bar and wires its click to the provided handler.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <param name="width">Button width.</param>
        /// <param name="onClick">Callback invoked when the button is clicked.</param>
        void AddWindowControl(string label, int width, Action onClick) {
            var buttonEntity = new EditorEntity {
                LayerMask = TitleBarLayerMask,
                Position = new float3(0, 6, 0)
            };
            var button = new ButtonComponent(label, new int2(width, 24), font, onClick, 0f);
            buttonEntity.AddComponent(button);
            rootEntity.AddChild(buttonEntity);
            windowControlButtons.Add((buttonEntity, width));
        }

        /// <summary>
        /// Computes a button width based on tight font metrics with added padding.
        /// </summary>
        /// <param name="label">Button label.</param>
        /// <returns>Calculated button width.</returns>
        int ComputeButtonWidth(string label) {
            var tight = font.MeasureTight(label);
            return Math.Max(40, (int)MathF.Ceiling(tight.Width) + 16);
        }

        float TitleVerticalOffset() {
            float lineHeight = MathF.Max(font.LineHeight, 1f);
            return (HeightPixels - lineHeight) / 2f;
        }
    }
}
