using System.Linq;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that editor context menus route pointer input to their visible rows.
    /// </summary>
    public class ContextMenuInteractionTests : IDisposable {
        /// <summary>
        /// Temporary content root used to initialize the test core.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Input manager used to simulate pointer interaction during the test.
        /// </summary>
        readonly TestInputBackend Input;
        /// <summary>
        /// Tracks how many times the current menu item action has been invoked.
        /// </summary>
        int ActivationCount;

        /// <summary>
        /// Initializes the core services required to test context-menu input routing.
        /// </summary>
        public ContextMenuInteractionTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-contextmenu-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            Input = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), Input);

            CreateUiCamera(320, 240, 0b0000000000000010);
        }

        /// <summary>
        /// Removes the temporary content root created for the test.
        /// </summary>
        public void Dispose() {
            ContextMenu.SubmenuIndicator = "v";
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures clicking a visible context-menu row invokes the assigned menu action.
        /// </summary>
        [Fact]
        public void ClickingContextMenuRow_InvokesMenuItemAction() {
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            menu.Show(
                new[] {
                    new ContextMenuItem("Open", HandleMenuItemActivated)
                },
                new int2(40, 24),
                new int2(320, 240));

            int2 rowPointer = new int2(
                menu.Position.X + 16,
                menu.Position.Y + ContextMenu.PaddingY + (ContextMenu.RowHeight / 2));

            AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, ActivationCount);
        }

        /// <summary>
        /// Ensures clicking a context-menu row still invokes the assigned action when the menu is parented under an offset entity and updated through the core loop.
        /// </summary>
        [Fact]
        public void ClickingParentedContextMenuRow_InvokesMenuItemAction() {
            EditorEntity host = new EditorEntity {
                InternalEntity = true,
                LayerMask = 0b0000000000000010,
                Position = new float3(32f, 40f, 0f)
            };

            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            host.AddChild(menu.Entity);
            menu.Show(
                new[] {
                    new ContextMenuItem("Open", HandleMenuItemActivated)
                },
                new int2(24, 31),
                new int2(320, 240));

            int2 rowPointer = new int2(
                (int)Math.Round(host.Position.X) + 24 + 16,
                (int)Math.Round(host.Position.Y) + 31 + ContextMenu.PaddingY + (ContextMenu.RowHeight / 2));

            AdvanceCoreInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, ActivationCount);
        }

        /// <summary>
        /// Ensures adjacent visible menu rows are laid out flush with no vertical gap between their strips.
        /// </summary>
        [Fact]
        public void Show_WhenMultipleRowsAreVisible_LaysOutMenuStripsWithoutGaps() {
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            menu.Show(
                new[] {
                    new ContextMenuItem("Open", HandleMenuItemActivated),
                    new ContextMenuItem("Open", HandleMenuItemActivated)
                },
                new int2(40, 24),
                new int2(320, 240));

            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(menu, "Rows");
            ContextMenuRow firstRow = rows[0];
            ContextMenuRow secondRow = rows[1];

            Assert.Equal(firstRow.Entity.Position.Y + ContextMenu.RowHeight, secondRow.Entity.Position.Y);
        }

        /// <summary>
        /// Ensures rows that open another menu automatically render the default submenu indicator on the right without mutating the source label.
        /// </summary>
        [Fact]
        public void Show_WhenItemOpensSubmenu_RendersDefaultIndicatorOnTheRight() {
            ContextMenuItem item = new ContextMenuItem("Light", HandleMenuItemActivated, HandleMenuItemActivated, false);
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            menu.Show(
                new[] {
                    item
                },
                new int2(40, 24),
                new int2(320, 240));

            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(menu, "Rows");
            ContextMenuRow row = rows[0];

            Assert.Equal("Light", row.Label.Text);
            Assert.Equal("v", row.Indicator.Text);
            Assert.True(row.IndicatorHost.Enabled);
            Assert.True(row.IndicatorHost.Position.X > row.LabelHost.Position.X);
            Assert.Equal("Light", item.Label);
        }

        /// <summary>
        /// Ensures the shared submenu indicator text can be customized globally for all rendered submenu rows.
        /// </summary>
        [Fact]
        public void Show_WhenGlobalSubmenuIndicatorChanges_UsesCustomizedRightIndicator() {
            ContextMenu.SubmenuIndicator = ">>";
            ContextMenuItem item = new ContextMenuItem("Light", HandleMenuItemActivated, HandleMenuItemActivated, false);
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            menu.Show(
                new[] {
                    item
                },
                new int2(40, 24),
                new int2(320, 240));

            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(menu, "Rows");

            Assert.Equal("Light", rows[0].Label.Text);
            Assert.Equal(">>", rows[0].Indicator.Text);
        }

        /// <summary>
        /// Ensures tall context menus clamp to the host viewport and expose their overflow through wheel scrolling.
        /// </summary>
        [Fact]
        public void Show_WhenMenuExceedsHostHeight_ClampsHeightAndScrollsVisibleRows() {
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            ContextMenuItem[] items = Enumerable.Range(0, 6)
                .Select(index => new ContextMenuItem("Open", HandleMenuItemActivated))
                .ToArray();
            int2 hostSize = new int2(320, (ContextMenu.PaddingY * 2) + (ContextMenu.RowHeight * 3));
            menu.Show(items, new int2(0, 0), hostSize);

            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(menu, "Rows");

            Assert.Equal(hostSize.Y, menu.Size.Y);
            Assert.Equal(3, rows.Count(row => row.Entity.Enabled));

            int2 rowPointer = new int2(
                menu.Position.X + 16,
                menu.Position.Y + ContextMenu.PaddingY + (ContextMenu.RowHeight / 2));

            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, GetPrivateField<ScrollComponent>(menu, "ScrollComponent").ScrollOffset);
        }

        /// <summary>
        /// Ensures the invisible blocker stays behind the visible row layer so the rows remain clickable.
        /// </summary>
        [Fact]
        public void Show_WhenMenuRowsAreVisible_PlacesTheBlockerBehindTheRows() {
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            menu.Show(
                new[] {
                    new ContextMenuItem("Open", HandleMenuItemActivated)
                },
                new int2(40, 24),
                new int2(320, 240));

            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(menu, "Rows");
            SpriteComponent blockerSurface = GetPrivateField<SpriteComponent>(menu, "BackgroundBlockerSurface");

            Assert.Single(rows, row => row.Entity.Enabled);
            Assert.True(blockerSurface.RenderOrder2D < rows[0].Background.RenderOrder2D);
        }

        /// <summary>
        /// Ensures one visible menu does not dismiss another visible menu that is receiving the click.
        /// </summary>
        [Fact]
        public void Update_WhenPointerClicksInsideAnotherVisibleMenu_KeepsTheParentMenuOpenAndAllowsActivation() {
            ContextMenu parentMenu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            ContextMenu childMenu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);

            parentMenu.Show(
                new[] {
                    new ContextMenuItem("Open", HandleMenuItemActivated)
                },
                new int2(40, 24),
                new int2(320, 240));

            childMenu.Show(
                new[] {
                    new ContextMenuItem("Child", HandleMenuItemActivated)
                },
                new int2(220, 24),
                new int2(320, 240));

            int2 childPointer = new int2(
                childMenu.Position.X + 16,
                childMenu.Position.Y + ContextMenu.PaddingY + (ContextMenu.RowHeight / 2));

            AdvanceCoreInput(new MouseState(childPointer.X, childPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceCoreInput(new MouseState(childPointer.X, childPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceCoreInput(new MouseState(childPointer.X, childPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.True(parentMenu.IsVisible);
            Assert.False(childMenu.IsVisible);
            Assert.Equal(1, ActivationCount);
        }

        /// <summary>
        /// Ensures relayout does not clear row interaction state while a visible menu is being hovered and clicked across consecutive frames.
        /// </summary>
        [Fact]
        public void UpdateLayout_WhenMenuRelayoutsBetweenHoverPressAndRelease_PreservesRowActivation() {
            ContextMenu menu = new ContextMenu(CreateFont(), 0b0000000000000010, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            int2 hostSize = new int2(320, 240);
            menu.Show(
                new[] {
                    new ContextMenuItem("Open", HandleMenuItemActivated)
                },
                new int2(40, 24),
                hostSize);

            int2 rowPointer = new int2(
                menu.Position.X + 16,
                menu.Position.Y + ContextMenu.PaddingY + (ContextMenu.RowHeight / 2));

            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            menu.UpdateLayout(hostSize);
            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            menu.UpdateLayout(hostSize);
            AdvanceCoreInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, ActivationCount);
        }

        /// <summary>
        /// Advances the input system by one frame using the supplied mouse state.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the next frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Input.EarlyUpdate();
            Input.Update();
        }

        /// <summary>
        /// Advances one full core frame using the supplied mouse state.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the next frame.</param>
        void AdvanceCoreInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }

        /// <summary>
        /// Creates the UI camera used to route context-menu hit testing.
        /// </summary>
        /// <param name="width">Viewport width.</param>
        /// <param name="height">Viewport height.</param>
        /// <param name="layerMask">Layer mask used by the camera.</param>
        void CreateUiCamera(int width, int height, ushort layerMask) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = layerMask,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }

        /// <summary>
        /// Records a context-menu activation for the current test.
        /// </summary>
        void HandleMenuItemActivated() {
            ActivationCount++;
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates a font asset with the glyphs required by the test menu.
        /// </summary>
        /// <returns>Font asset used to lay out the test menu.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['>'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
    }
}

