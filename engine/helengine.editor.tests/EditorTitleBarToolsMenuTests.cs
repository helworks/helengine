using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor title bar exposes the Tools menu and environment registry command.
    /// </summary>
    public sealed class EditorTitleBarToolsMenuTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the title-bar tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the title bar.
        /// </summary>
        public EditorTitleBarToolsMenuTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-titlebar-tools-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the Tools menu contains the environment registry command and hides other menus when opened.
        /// </summary>
        [Fact]
        public void ToggleToolsMenu_ShowsEnvironmentsCommandAndHidesOtherMenus() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");
            InvokePrivate(titleBar, "ToggleToolsMenu");

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu toolsMenu = GetPrivateField<ContextMenu>(titleBar, "ToolsMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(toolsMenu, "ActiveItems");

            Assert.False(fileMenu.IsVisible);
            Assert.True(toolsMenu.IsVisible);
            Assert.Collection(activeItems, item => Assert.Equal("Environments...", item.Label));
        }

        /// <summary>
        /// Ensures activating Environments raises the public command event and closes the menu.
        /// </summary>
        [Fact]
        public void ToolsMenu_WhenEnvironmentsActivated_RaisesEnvironmentsRequested() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            bool raised = false;
            titleBar.EnvironmentsRequested += () => raised = true;

            InvokePrivate(titleBar, "ToggleToolsMenu");

            ContextMenu toolsMenu = GetPrivateField<ContextMenu>(titleBar, "ToolsMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(toolsMenu, "ActiveItems");
            activeItems[0].Action();

            Assert.True(raised);
            Assert.False(toolsMenu.IsVisible);
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
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Creates a small font asset that can satisfy title-bar layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
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
