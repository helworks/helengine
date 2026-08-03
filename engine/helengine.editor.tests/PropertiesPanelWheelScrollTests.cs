using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the Properties panel's shared scroll controller actually advances from real mouse-wheel
    /// input once the panel is constructed and used the way EditorSession uses it, without any extra
    /// caller-side wiring.
    /// </summary>
    public class PropertiesPanelWheelScrollTests : IDisposable {
        readonly string TempRootPath;
        readonly TestInputBackend Input;

        public PropertiesPanelWheelScrollTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-properties-panel-wheel-scroll-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();
            EditorSceneMutationService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            Input = new TestInputBackend();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input, new PlatformInfo("test", "test-version"));
        }

        public void Dispose() {
            EditorInputCaptureService.Reset();
            EditorSceneMutationService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a freshly constructed properties panel scrolls from wheel input without requiring the
        /// caller to explicitly initialize its entity hierarchy.
        /// </summary>
        [Fact]
        public void PropertiesPanel_WhenWheelScrollsOverTallEntityContent_AdvancesScrollOffset() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(new HostFileSystemContentStreamSource(TempRootPath))) {
                Position = new float3(32f, 40f, 0f),
                Size = new int2(320, 120)
            };
            EditorEntity entity = new EditorEntity { Name = "Tall" };
            entity.AddComponent(new PropertiesPanelComponentShellTests.TallPropertyTestComponent());

            panel.ShowEntityProperties(entity);

            int mouseX = (int)panel.Position.X + 20;
            int mouseY = (int)panel.Position.Y + panel.TitleBarHeightPixels + 30;

            AdvanceInput(new MouseState(mouseX, mouseY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(mouseX, mouseY, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            ScrollComponent scrollComponent = (ScrollComponent)typeof(PropertiesPanel)
                .GetField("ContentScrollComponent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(panel);

            Assert.True(scrollComponent.MaximumScrollOffset > 0);
            Assert.True(scrollComponent.ScrollOffset > 0);
        }

        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }

        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['X'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['?'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
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
