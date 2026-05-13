using System.Collections.Generic;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies render-order and contrast behavior for the main editor title bar.
    /// </summary>
    public class EditorTitleBarTests : IDisposable {
        /// <summary>
        /// Stores the theme that was active before the test modified it.
        /// </summary>
        readonly ThemeManager.ThemePalette OriginalTheme;

        /// <summary>
        /// Captures the original theme so it can be restored after each test.
        /// </summary>
        public EditorTitleBarTests() {
            OriginalTheme = ThemeManager.Current;
        }

        /// <summary>
        /// Restores shared theme state after each test.
        /// </summary>
        public void Dispose() {
            ThemeManager.SetTheme(OriginalTheme);
        }

        /// <summary>
        /// Ensures the default host title-bar height uses the reduced chrome target.
        /// </summary>
        [Fact]
        public void Constructor_UsesReducedDefaultTitleBarHeight() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            Assert.Equal(27, titleBar.Height);
        }

        /// <summary>
        /// Ensures the main title text uses a high-contrast color against the dark title-bar surface.
        /// </summary>
        [Fact]
        public void Constructor_UsesAccentQuaternaryForMainTitleText() {
            InitializeCore();
            ThemeManager.SetTheme(ThemeManager.CreateNeon90s());
            const string title = "Main Editor Title";

            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, title);

            TextComponent titleText = FindTextComponent(titleBar.Entity, title);

            Assert.Equal(ThemeManager.Colors.AccentQuaternary, titleText.Color);
        }

        /// <summary>
        /// Ensures the editor logo renders inside the reserved left title-bar slot when a texture is supplied.
        /// </summary>
        [Fact]
        public void Constructor_WithIconTexture_RendersEditorLogoInLeftSlot() {
            InitializeCore();
            RuntimeTexture iconTexture = new TestRuntimeTexture {
                Width = 128,
                Height = 128
            };

            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title", iconTexture);

            EditorEntity iconEntity = GetPrivateField<EditorEntity>(titleBar, "IconEntity");
            SpriteComponent iconSprite = GetPrivateField<SpriteComponent>(titleBar, "IconSprite");

            Assert.NotNull(iconEntity);
            Assert.Equal(4f, iconEntity.Position.X);
            Assert.Equal(4f, iconEntity.Position.Y);
            Assert.Same(iconTexture, iconSprite.Texture);
            Assert.Equal(new int2(19, 19), iconSprite.Size);
        }

        /// <summary>
        /// Ensures scaled metrics enlarge the title-bar header and icon placement consistently.
        /// </summary>
        [Fact]
        public void Constructor_WithScaledMetrics_UsesScaledTitleBarHeaderAndIconSize() {
            InitializeCore();
            RuntimeTexture iconTexture = new TestRuntimeTexture {
                Width = 128,
                Height = 128
            };
            EditorUiMetrics metrics = new EditorUiMetrics(1.5);

            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), metrics, 1280, 720, "Main Editor Title", iconTexture);

            EditorEntity iconEntity = GetPrivateField<EditorEntity>(titleBar, "IconEntity");
            SpriteComponent iconSprite = GetPrivateField<SpriteComponent>(titleBar, "IconSprite");

            Assert.Equal(41, titleBar.Height);
            Assert.Equal(6f, iconEntity.Position.X);
            Assert.Equal(6f, iconEntity.Position.Y);
            Assert.Equal(new int2(29, 29), iconSprite.Size);
        }

        /// <summary>
        /// Ensures the main File menu renders above docked panel content and labels.
        /// </summary>
        [Fact]
        public void FileMenu_UsesOverlayRenderOrdersAboveDockPanels() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");

            fileMenu.Show(
                new[] {
                    new ContextMenuItem("Main", HandleMenuItemActivated)
                },
                new int2(0, 0),
                new int2(1280, 720));

            RoundedRectComponent menuBackground = FindComponent<RoundedRectComponent>(fileMenu.Entity);
            TextComponent menuItemText = FindTextComponent(fileMenu.Entity, "Main");

            Assert.Equal(RenderOrder2D.OverlayBackground, menuBackground.RenderOrder2D);
            Assert.Equal(RenderOrder2D.OverlayForeground, menuItemText.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the Add menu uses the same overlay orders as the File menu and stays above docked panels.
        /// </summary>
        [Fact]
        public void AddMenu_UsesOverlayRenderOrdersAboveDockPanels() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");

            addMenu.Show(
                new[] {
                    new ContextMenuItem("Cube", HandleMenuItemActivated)
                },
                new int2(0, 0),
                new int2(1280, 720));

            RoundedRectComponent menuBackground = FindComponent<RoundedRectComponent>(addMenu.Entity);
            TextComponent menuItemText = FindTextComponent(addMenu.Entity, "Cube");

            Assert.Equal(RenderOrder2D.OverlayBackground, menuBackground.RenderOrder2D);
            Assert.Equal(RenderOrder2D.OverlayForeground, menuItemText.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the title bar creates one dedicated UI menu button beside the built-in menus.
        /// </summary>
        [Fact]
        public void Constructor_BuildsUiMenuButtonBesideBuildButton() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity uiButtonEntity = GetPrivateField<EditorEntity>(titleBar, "UiMenuButtonEntity");

            Assert.NotNull(uiButtonEntity);
        }

        /// <summary>
        /// Ensures test activation of a built-in UI menu action raises the matching routed action value.
        /// </summary>
        [Fact]
        public void ActivateUiMenuItemForTest_WhenSaveSlotThreeIsRequested_RaisesUiMenuAction() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
            EditorTitleBarUiMenuAction? action = null;
            titleBar.UiMenuActionRequested += value => action = value;

            titleBar.ActivateUiMenuItemForTest("save-slot-3");

            Assert.Equal(EditorTitleBarUiMenuAction.SaveSlot3, Assert.IsType<EditorTitleBarUiMenuAction>(action));
        }

        /// <summary>
        /// Ensures the UI Show submenu reflects the current registered panel labels.
        /// </summary>
        [Fact]
        public void ApplyUiShowMenuItems_WhenPanelTypesChange_RebuildsShowSubmenuItems() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            titleBar.ApplyUiShowMenuItems(["Viewport", "Preview", "Logger"]);
            titleBar.ShowUiShowMenuForTest();

            ContextMenu uiShowMenu = GetPrivateField<ContextMenu>(titleBar, "UiShowMenu");
            TextComponent loggerText = FindTextComponent(uiShowMenu.Entity, "Logger");

            Assert.NotNull(loggerText);
        }

        /// <summary>
        /// Ensures the left-side title-bar buttons start at the top edge and span the full title-bar height.
        /// </summary>
        [Fact]
        public void Layout_UsesFullHeightForLeftSideTitleBarButtons() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");

            AssertTitleBarButtonUsesFullHeight(fileButtonEntity);
            AssertTitleBarButtonUsesFullHeight(addButtonEntity);
        }

        /// <summary>
        /// Ensures the right-side window control buttons start at the top edge and span the full title-bar height.
        /// </summary>
        [Fact]
        public void Layout_UsesFullHeightForRightSideWindowControlButtons() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
            EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

            AssertTitleBarButtonUsesFullHeight(minimizeButtonEntity);
            AssertTitleBarButtonUsesFullHeight(maximizeButtonEntity);
            AssertTitleBarButtonUsesFullHeight(closeButtonEntity);
        }

        /// <summary>
        /// Ensures adjacent title-bar buttons touch horizontally without leaving gutters between them.
        /// </summary>
        [Fact]
        public void Layout_UsesNoHorizontalGapBetweenTitleBarButtons() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
            EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

            AssertAdjacentButtonsTouch(fileButtonEntity, addButtonEntity);
            AssertAdjacentButtonsTouch(minimizeButtonEntity, maximizeButtonEntity);
            AssertAdjacentButtonsTouch(maximizeButtonEntity, closeButtonEntity);
        }

        /// <summary>
        /// Ensures title-bar buttons render transparent at rest while preserving their hover background.
        /// </summary>
        [Fact]
        public void Layout_UsesHoverOnlyBackgroundForTitleBarButtons() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
            EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

            AssertTitleBarButtonUsesHoverOnlyBackground(fileButtonEntity);
            AssertTitleBarButtonUsesHoverOnlyBackground(addButtonEntity);
            AssertTitleBarButtonUsesHoverOnlyBackground(minimizeButtonEntity);
            AssertTitleBarButtonUsesHoverOnlyBackground(maximizeButtonEntity);
            AssertTitleBarButtonUsesHoverOnlyBackground(closeButtonEntity);
        }

        /// <summary>
        /// Ensures title-bar buttons use square corners so their full-height edges align with the title bar.
        /// </summary>
        [Fact]
        public void Layout_UsesSquareCornersForTitleBarButtons() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
            EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

            AssertTitleBarButtonUsesSquareCorners(fileButtonEntity);
            AssertTitleBarButtonUsesSquareCorners(addButtonEntity);
            AssertTitleBarButtonUsesSquareCorners(minimizeButtonEntity);
            AssertTitleBarButtonUsesSquareCorners(maximizeButtonEntity);
            AssertTitleBarButtonUsesSquareCorners(closeButtonEntity);
        }

        /// <summary>
        /// Ensures transparent title-bar buttons use a light label color against the title-bar surface.
        /// </summary>
        [Fact]
        public void Layout_UsesLightTextForTitleBarButtons() {
            InitializeCore();
            ThemeManager.SetTheme(ThemeManager.CreateNeon90s());
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
            EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

            AssertTitleBarButtonUsesLightText(fileButtonEntity, "File");
            AssertTitleBarButtonUsesLightText(addButtonEntity, "Add");
            AssertTitleBarButtonUsesLightText(minimizeButtonEntity, "-");
            AssertTitleBarButtonUsesLightText(maximizeButtonEntity, "Max");
            AssertTitleBarButtonUsesLightText(closeButtonEntity, "X");
        }

        /// <summary>
        /// Ensures title-bar buttons use one-pixel shared borders so adjacent edges do not render as two-pixel seams.
        /// </summary>
        [Fact]
        public void Layout_UsesSinglePixelSharedVerticalBordersForTitleBarButtons() {
            InitializeCore();
            ThemeManager.SetTheme(ThemeManager.CreateNeon90s());
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            EditorEntity buildButtonEntity = GetPrivateField<EditorEntity>(titleBar, "BuildMenuButtonEntity");
            EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
            EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

            RoundedRectComponent fileBackground = FindComponent<RoundedRectComponent>(fileButtonEntity);
            RoundedRectComponent addBackground = FindComponent<RoundedRectComponent>(addButtonEntity);
            RoundedRectComponent buildBackground = FindComponent<RoundedRectComponent>(buildButtonEntity);
            RoundedRectComponent minimizeBackground = FindComponent<RoundedRectComponent>(minimizeButtonEntity);
            RoundedRectComponent maximizeBackground = FindComponent<RoundedRectComponent>(maximizeButtonEntity);
            RoundedRectComponent closeBackground = FindComponent<RoundedRectComponent>(closeButtonEntity);

            AssertBorderPositions(fileButtonEntity, fileButtonEntity.Position.X);
            AssertBorderPositions(addButtonEntity, addButtonEntity.Position.X, addButtonEntity.Position.X + addBackground.Size.X - 1f);
            AssertBorderPositions(buildButtonEntity, buildButtonEntity.Position.X + buildBackground.Size.X - 1f);
            AssertBorderPositions(minimizeButtonEntity, minimizeButtonEntity.Position.X);
            AssertBorderPositions(maximizeButtonEntity, maximizeButtonEntity.Position.X);
            AssertBorderPositions(closeButtonEntity, closeButtonEntity.Position.X);

            Assert.DoesNotContain(fileButtonEntity.Position.X + fileBackground.Size.X - 1f, GetButtonBorderAbsoluteXPositions(fileButtonEntity));
            Assert.DoesNotContain(buildButtonEntity.Position.X, GetButtonBorderAbsoluteXPositions(buildButtonEntity));
            Assert.DoesNotContain(minimizeButtonEntity.Position.X + minimizeBackground.Size.X - 1f, GetButtonBorderAbsoluteXPositions(minimizeButtonEntity));
            Assert.DoesNotContain(maximizeButtonEntity.Position.X + maximizeBackground.Size.X - 1f, GetButtonBorderAbsoluteXPositions(maximizeButtonEntity));
            Assert.DoesNotContain(closeButtonEntity.Position.X + closeBackground.Size.X - 1f, GetButtonBorderAbsoluteXPositions(closeButtonEntity));
        }

        /// <summary>
        /// Ensures the close button reaches the right edge of the host window.
        /// </summary>
        [Fact]
        public void Layout_AlignsCloseButtonToRightWindowWall() {
            InitializeCore();
            const int windowWidth = 1280;
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), windowWidth, 720, "Main Editor Title");

            EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");
            RoundedRectComponent closeBackground = FindComponent<RoundedRectComponent>(closeButtonEntity);

            Assert.Equal(windowWidth, closeButtonEntity.Position.X + closeBackground.Size.X);
        }

        /// <summary>
        /// Ensures the File button leaves room for the editor icon at the left edge of the title bar.
        /// </summary>
        [Fact]
        public void Layout_ReservesLeftIconSlotBeforeFileButton() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

            EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");

            Assert.Equal(EditorTitleBar.HeightPixels, fileButtonEntity.Position.X);
        }

        /// <summary>
        /// Initializes a core instance with the minimum services required by title-bar UI controls.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy title-bar text layout in tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Handles a test context-menu activation without side effects.
        /// </summary>
        void HandleMenuItemActivated() {
        }

        /// <summary>
        /// Verifies that a title-bar button is flush with the title-bar bounds and uses the full available height.
        /// </summary>
        /// <param name="buttonEntity">Title-bar button entity to inspect.</param>
        void AssertTitleBarButtonUsesFullHeight(EditorEntity buttonEntity) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }

            RoundedRectComponent background = FindComponent<RoundedRectComponent>(buttonEntity);
            InteractableComponent interactable = FindComponent<InteractableComponent>(buttonEntity);

            Assert.Equal(0f, buttonEntity.Position.Y);
            Assert.Equal(EditorTitleBar.HeightPixels, background.Size.Y);
            Assert.Equal(EditorTitleBar.HeightPixels, interactable.Size.Y);
        }

        /// <summary>
        /// Verifies that two buttons are laid out edge-to-edge.
        /// </summary>
        /// <param name="leftButtonEntity">Button expected to appear on the left.</param>
        /// <param name="rightButtonEntity">Button expected to appear immediately to the right.</param>
        void AssertAdjacentButtonsTouch(EditorEntity leftButtonEntity, EditorEntity rightButtonEntity) {
            if (leftButtonEntity == null) {
                throw new ArgumentNullException(nameof(leftButtonEntity));
            }
            if (rightButtonEntity == null) {
                throw new ArgumentNullException(nameof(rightButtonEntity));
            }

            RoundedRectComponent leftBackground = FindComponent<RoundedRectComponent>(leftButtonEntity);
            float expectedRightButtonX = leftButtonEntity.Position.X + leftBackground.Size.X;

            Assert.Equal(expectedRightButtonX, rightButtonEntity.Position.X);
        }

        /// <summary>
        /// Verifies that a title-bar button only paints its background while hovered.
        /// </summary>
        /// <param name="buttonEntity">Title-bar button entity to inspect.</param>
        void AssertTitleBarButtonUsesHoverOnlyBackground(EditorEntity buttonEntity) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }

            RoundedRectComponent background = FindComponent<RoundedRectComponent>(buttonEntity);
            InteractableComponent interactable = FindComponent<InteractableComponent>(buttonEntity);
            byte4 transparent = new byte4(255, 255, 255, 0);

            Assert.Equal(transparent, background.FillColor);

            interactable.OnCursor(new int2(1, 1), new int2(0, 0), PointerInteraction.Hover);

            Assert.Equal(ThemeManager.Colors.AccentPrimary, background.FillColor);

            interactable.OnCursor(new int2(1, 1), new int2(0, 0), PointerInteraction.Leave);

            Assert.Equal(transparent, background.FillColor);
        }

        /// <summary>
        /// Verifies that a title-bar button background has no corner radius.
        /// </summary>
        /// <param name="buttonEntity">Title-bar button entity to inspect.</param>
        void AssertTitleBarButtonUsesSquareCorners(EditorEntity buttonEntity) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }

            RoundedRectComponent background = FindComponent<RoundedRectComponent>(buttonEntity);

            Assert.Equal(0f, background.Radius);
        }

        /// <summary>
        /// Verifies that a title-bar button label uses the title-bar foreground color.
        /// </summary>
        /// <param name="buttonEntity">Title-bar button entity to inspect.</param>
        /// <param name="label">Expected button label text.</param>
        void AssertTitleBarButtonUsesLightText(EditorEntity buttonEntity, string label) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Button label must be provided.", nameof(label));
            }

            TextComponent textComponent = FindTextComponent(buttonEntity, label);

            Assert.Equal(ThemeManager.Colors.AccentQuaternary, textComponent.Color);
        }

        /// <summary>
        /// Verifies that a title-bar button contains border sprites at the expected absolute x coordinates.
        /// </summary>
        /// <param name="buttonEntity">Title-bar button entity to inspect.</param>
        /// <param name="expectedPositions">Expected absolute x positions for the button borders.</param>
        void AssertBorderPositions(EditorEntity buttonEntity, params float[] expectedPositions) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }

            List<float> actualPositions = GetButtonBorderAbsoluteXPositions(buttonEntity);

            Assert.Equal(expectedPositions.Length, actualPositions.Count);
            for (int positionIndex = 0; positionIndex < expectedPositions.Length; positionIndex++) {
                Assert.Contains(expectedPositions[positionIndex], actualPositions);
            }
        }

        /// <summary>
        /// Gets the absolute x positions of one-pixel vertical border sprites owned by a title-bar button.
        /// </summary>
        /// <param name="buttonEntity">Title-bar button entity to inspect.</param>
        /// <returns>Absolute x positions of visible vertical border sprites.</returns>
        List<float> GetButtonBorderAbsoluteXPositions(EditorEntity buttonEntity) {
            if (buttonEntity == null) {
                throw new ArgumentNullException(nameof(buttonEntity));
            }

            List<float> borderPositions = new List<float>();
            if (buttonEntity.Children == null) {
                return borderPositions;
            }

            for (int childIndex = 0; childIndex < buttonEntity.Children.Count; childIndex++) {
                Entity currentEntity = buttonEntity.Children[childIndex];
                if (currentEntity.Components != null) {
                    for (int componentIndex = 0; componentIndex < currentEntity.Components.Count; componentIndex++) {
                        if (currentEntity.Components[componentIndex] is SpriteComponent spriteComponent &&
                            spriteComponent.Size.X == 1 &&
                            spriteComponent.Size.Y == EditorTitleBar.HeightPixels &&
                            spriteComponent.Color.Equals(ThemeManager.Colors.AccentQuaternary)) {
                            borderPositions.Add(currentEntity.Position.X);
                        }
                    }
                }
            }

            return borderPositions;
        }

        /// <summary>
        /// Finds a text component in an entity hierarchy by exact displayed text.
        /// </summary>
        /// <param name="entity">Root entity to inspect.</param>
        /// <param name="text">Exact text to locate.</param>
        /// <returns>Matching text component.</returns>
        TextComponent FindTextComponent(Entity entity, string text) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (string.IsNullOrWhiteSpace(text)) {
                throw new ArgumentException("Text to locate must be provided.", nameof(text));
            }

            List<Entity> pendingEntities = new List<Entity> {
                entity
            };

            for (int entityIndex = 0; entityIndex < pendingEntities.Count; entityIndex++) {
                Entity currentEntity = pendingEntities[entityIndex];
                if (currentEntity.Components != null) {
                    for (int componentIndex = 0; componentIndex < currentEntity.Components.Count; componentIndex++) {
                        if (currentEntity.Components[componentIndex] is TextComponent textComponent &&
                            textComponent.Text == text) {
                            return textComponent;
                        }
                    }
                }

                if (currentEntity.Children != null) {
                    for (int childIndex = 0; childIndex < currentEntity.Children.Count; childIndex++) {
                        pendingEntities.Add(currentEntity.Children[childIndex]);
                    }
                }
            }

            throw new InvalidOperationException("Expected to find the requested text component in the title-bar hierarchy.");
        }

        /// <summary>
        /// Finds the first component of the requested type in an entity hierarchy.
        /// </summary>
        /// <typeparam name="T">Component type to locate.</typeparam>
        /// <param name="entity">Root entity to inspect.</param>
        /// <returns>Matching component instance.</returns>
        T FindComponent<T>(Entity entity) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<Entity> pendingEntities = new List<Entity> {
                entity
            };

            for (int entityIndex = 0; entityIndex < pendingEntities.Count; entityIndex++) {
                Entity currentEntity = pendingEntities[entityIndex];
                if (currentEntity.Components != null) {
                    for (int componentIndex = 0; componentIndex < currentEntity.Components.Count; componentIndex++) {
                        if (currentEntity.Components[componentIndex] is T component) {
                            return component;
                        }
                    }
                }

                if (currentEntity.Children != null) {
                    for (int childIndex = 0; childIndex < currentEntity.Children.Count; childIndex++) {
                        pendingEntities.Add(currentEntity.Children[childIndex]);
                    }
                }
            }

            throw new InvalidOperationException("Expected to find the requested component type in the entity hierarchy.");
        }

        /// <summary>
        /// Reads a private reference-type field from an object using reflection.
        /// </summary>
        /// <typeparam name="T">Field type.</typeparam>
        /// <param name="instance">Object containing the private field.</param>
        /// <param name="fieldName">Exact field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object instance, string fieldName) where T : class {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            FieldInfo fieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null) {
                throw new InvalidOperationException("Expected the requested private field to exist.");
            }

            object value = fieldInfo.GetValue(instance);
            if (value is T typedValue) {
                return typedValue;
            }

            throw new InvalidOperationException("Expected the requested private field to match the requested type.");
        }
    }
}
