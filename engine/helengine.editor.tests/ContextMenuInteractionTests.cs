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
        readonly TestInputManager Input;
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
            Input = new TestInputManager();
            core.Initialize(null, new TestRenderManager2D(), Input);

            CreateUiCamera(320, 240, 0b0000000000000010);
        }

        /// <summary>
        /// Removes the temporary content root created for the test.
        /// </summary>
        public void Dispose() {
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
        /// Advances the input manager by one frame using the supplied mouse state.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the next frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Input.EarlyUpdate();
            Input.Update();
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
        /// Creates a font asset with the glyphs required by the test menu.
        /// </summary>
        /// <returns>Font asset used to lay out the test menu.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
