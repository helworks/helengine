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
        /// Render order used for toolbar surfaces.
        /// </summary>
        readonly byte ToolbarSurfaceOrder;
        /// <summary>
        /// Render order used for toolbar button labels.
        /// </summary>
        readonly byte ToolbarTextOrder;
        /// <summary>
        /// Owner key used to register the toolbar input blocker.
        /// </summary>
        readonly object ToolbarInputBlockerOwner;
        /// <summary>
        /// Font used by toolbar button labels.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity hosting the toolbar visuals and interactables.
        /// </summary>
        readonly EditorEntity ToolbarRoot;
        /// <summary>
        /// Background sprite for the toolbar chrome.
        /// </summary>
        readonly SpriteComponent ToolbarBackground;
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
        /// Text labels for tool buttons, indexed to <see cref="ToolModes"/>.
        /// </summary>
        readonly TextComponent[] ToolButtonTexts;
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
        /// Initializes a new dockable viewport and binds it to the provided camera.
        /// </summary>
        /// <param name="camera">Camera rendering into the viewport.</param>
        /// <param name="font">Font used by the base dockable entity title bar.</param>
        public EditorViewport(CameraComponent camera, FontAsset font)
            : base(font) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            Title = "Viewport";
            SetContentBackgroundColor(new byte4(0, 0, 0, 0));

            ToolbarSurfaceOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            ToolbarTextOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);
            ToolbarInputBlockerOwner = new object();
            ToolModes = new[] {
                EditorViewportToolMode.Translate,
                EditorViewportToolMode.Rotate,
                EditorViewportToolMode.Scale
            };
            ToolButtonRoots = new EditorEntity[ToolModes.Length];
            ToolButtonBackgrounds = new SpriteComponent[ToolModes.Length];
            ToolButtonTexts = new TextComponent[ToolModes.Length];
            ToolButtonInteractables = new InteractableComponent[ToolModes.Length];
            ToolButtonHoverStates = new bool[ToolModes.Length];
            ToolButtonPressedStates = new bool[ToolModes.Length];

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
            AddComponent(new EditorViewportCameraAngleOverlayComponent(Camera, Font, ToolbarHeight));

            InitializeToolButtons();
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
            CreateToolButton(0, EditorViewportToolMode.Translate, "T");
            CreateToolButton(1, EditorViewportToolMode.Rotate, "R");
            CreateToolButton(2, EditorViewportToolMode.Scale, "S");
        }

        /// <summary>
        /// Creates one tool-button visual and interaction entry.
        /// </summary>
        /// <param name="buttonIndex">Button index into toolbar arrays.</param>
        /// <param name="toolMode">Tool mode represented by the button.</param>
        /// <param name="label">Text label shown on the button.</param>
        void CreateToolButton(int buttonIndex, EditorViewportToolMode toolMode, string label) {
            if (buttonIndex < 0 || buttonIndex >= ToolModes.Length) {
                throw new ArgumentOutOfRangeException(nameof(buttonIndex), "Tool button index must be inside toolbar bounds.");
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Tool button label must be provided.", nameof(label));
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

            EditorEntity labelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            buttonRoot.AddChild(labelHost);

            TextComponent buttonText = new TextComponent {
                Font = Font,
                Text = label,
                Color = ThemeManager.Colors.TextOnAccent,
                Size = new int2(1, 1),
                RenderOrder2D = ToolbarTextOrder
            };
            labelHost.AddComponent(buttonText);

            InteractableComponent buttonInteractable = new InteractableComponent {
                Size = new int2(ToolButtonWidth, ToolButtonHeight)
            };
            int capturedButtonIndex = buttonIndex;
            buttonInteractable.CursorEvent += (pos, delta, state) => HandleToolButtonCursor(capturedButtonIndex, state);
            buttonRoot.AddComponent(buttonInteractable);

            ToolModes[buttonIndex] = toolMode;
            ToolButtonRoots[buttonIndex] = buttonRoot;
            ToolButtonBackgrounds[buttonIndex] = buttonBackground;
            ToolButtonTexts[buttonIndex] = buttonText;
            ToolButtonInteractables[buttonIndex] = buttonInteractable;
            ToolButtonHoverStates[buttonIndex] = false;
            ToolButtonPressedStates[buttonIndex] = false;
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
        /// Applies visual state to all tool buttons based on active, hover, and pressed states.
        /// </summary>
        void UpdateToolButtonVisuals() {
            EditorViewportToolMode activeToolMode = ToolMode;
            for (int buttonIndex = 0; buttonIndex < ToolModes.Length; buttonIndex++) {
                SpriteComponent background = ToolButtonBackgrounds[buttonIndex];
                TextComponent text = ToolButtonTexts[buttonIndex];
                if (background == null || text == null) {
                    continue;
                }

                bool isActive = ToolModes[buttonIndex] == activeToolMode;
                bool isHovered = ToolButtonHoverStates[buttonIndex];
                bool isPressed = ToolButtonPressedStates[buttonIndex];
                if (isPressed) {
                    background.Color = ThemeManager.Colors.AccentTertiary;
                } else if (isActive) {
                    background.Color = ThemeManager.Colors.AccentPrimary;
                } else if (isHovered) {
                    background.Color = ThemeManager.Colors.AccentSecondary;
                } else {
                    background.Color = ThemeManager.Colors.SurfaceInput;
                }

                text.Color = isActive ? ThemeManager.Colors.TextOnAccent : ThemeManager.Colors.InputForegroundPrimary;
            }
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
            float buttonY = MathF.Round((ToolbarHeight - ToolButtonHeight) * 0.5f);
            for (int buttonIndex = 0; buttonIndex < ToolModes.Length; buttonIndex++) {
                EditorEntity buttonRoot = ToolButtonRoots[buttonIndex];
                SpriteComponent buttonBackground = ToolButtonBackgrounds[buttonIndex];
                TextComponent buttonText = ToolButtonTexts[buttonIndex];
                InteractableComponent buttonInteractable = ToolButtonInteractables[buttonIndex];
                if (buttonRoot == null || buttonBackground == null || buttonText == null || buttonInteractable == null) {
                    continue;
                }

                float buttonX = ToolbarPadding + buttonIndex * (ToolButtonWidth + ToolbarButtonSpacing);
                buttonRoot.Position = new float3(buttonX, buttonY, 0.1f);
                buttonBackground.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                buttonInteractable.Size = new int2(ToolButtonWidth, ToolButtonHeight);
                LayoutToolButtonLabel(buttonText, ToolButtonWidth, ToolButtonHeight);
            }
        }

        /// <summary>
        /// Centers a tool-button label using tight font metrics.
        /// </summary>
        /// <param name="buttonText">Text component to layout.</param>
        /// <param name="buttonWidth">Width of the button.</param>
        /// <param name="buttonHeight">Height of the button.</param>
        void LayoutToolButtonLabel(TextComponent buttonText, int buttonWidth, int buttonHeight) {
            if (buttonText == null) {
                throw new ArgumentNullException(nameof(buttonText));
            }

            FontTightMetrics metrics = Font.MeasureTight(buttonText.Text);
            float textX = MathF.Round((buttonWidth - metrics.Width) * 0.5f);
            float textY = GetTextTopOffset(buttonHeight, metrics);
            if (buttonText.Parent != null) {
                buttonText.Parent.Position = new float3(textX, textY, 0.1f);
            }

            buttonText.Size = new int2((int)Math.Ceiling(metrics.Width), (int)Math.Ceiling(metrics.Height));
        }

        /// <summary>
        /// Computes the top offset needed to vertically center text from tight metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container.</param>
        /// <param name="metrics">Tight text metrics.</param>
        /// <returns>Top offset that vertically centers the text.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return MathF.Round((containerHeight - metrics.Height) * 0.5f - metrics.MinTop);
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
