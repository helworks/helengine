using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui {
    /// <summary>
    /// Verifies the title bar can render and replace project-authored contributed menus.
    /// </summary>
    public sealed class EditorTitleBarProjectMenuTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the title-bar tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the contributed title-bar menu tests.
        /// </summary>
        public EditorTitleBarProjectMenuTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-titlebar-project-menu-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary title-bar test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the first contributed menu button is laid out immediately to the right of Build.
        /// </summary>
        [Fact]
        public void ApplyProjectMenus_WhenDemoMenuIsProvided_PlacesDemoButtonImmediatelyToTheRightOfBuild() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "helengine");

            titleBar.ApplyProjectMenus(CreateDemoMenuItems());

            EditorEntity buildButtonEntity = GetPrivateField<EditorEntity>(titleBar, "BuildMenuButtonEntity");
            int buildButtonWidth = GetPrivateField<int>(titleBar, "BuildMenuButtonWidth");
            EditorTitleBarProjectMenuState projectMenuState = Assert.Single(GetProjectMenuStates(titleBar));

            Assert.Equal(buildButtonEntity.Position.X + buildButtonWidth, projectMenuState.ButtonEntity.Position.X);
        }

        /// <summary>
        /// Ensures activating the contributed demo menu item raises the mapped project menu item identifier.
        /// </summary>
        [Fact]
        public void ApplyProjectMenus_WhenDemoMenuIsProvided_RaisesActivationForItsMenuItem() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "helengine");
            string activatedMenuItemId = string.Empty;
            titleBar.ProjectMenuItemRequested += menuItemId => activatedMenuItemId = menuItemId;

            titleBar.ApplyProjectMenus(CreateDemoMenuItems());
            titleBar.ActivateProjectMenuItemForTest("demo.regenerate-main-menu");

            Assert.Equal("demo.regenerate-main-menu", activatedMenuItemId);
        }

        /// <summary>
        /// Ensures applying the same contributed menu set twice replaces prior UI state instead of appending duplicates.
        /// </summary>
        [Fact]
        public void ApplyProjectMenus_WhenAppliedTwice_ReplacesPriorStateWithoutDuplicatingTopLevelMenus() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "helengine");

            titleBar.ApplyProjectMenus(CreateDemoMenuItems());
            int firstChildCount = titleBar.Entity.Children.Count;
            titleBar.ApplyProjectMenus(CreateDemoMenuItems());

            Assert.Single(GetProjectMenuStates(titleBar));
            Assert.Equal(firstChildCount, titleBar.Entity.Children.Count);
        }

        /// <summary>
        /// Returns the deterministic contributed demo menu descriptors used by the title-bar tests.
        /// </summary>
        /// <returns>One contributed top-level demo menu item.</returns>
        IReadOnlyList<EditorMenuItemDescriptor> CreateDemoMenuItems() {
            return [
                new EditorMenuItemDescriptor(
                    "demo",
                    "Demo",
                    100,
                    "demo.regenerate-main-menu",
                    "Regenerate Main Menu...",
                    100,
                    "menu.regenerate-demo-disc-main-menu")
            ];
        }

        /// <summary>
        /// Returns the current contributed project menu states rendered by the title bar.
        /// </summary>
        /// <param name="titleBar">Title bar under test.</param>
        /// <returns>Contributed project menu states rendered by the title bar.</returns>
        IReadOnlyList<EditorTitleBarProjectMenuState> GetProjectMenuStates(EditorTitleBar titleBar) {
            return GetPrivateField<List<EditorTitleBarProjectMenuState>>(titleBar, "ProjectMenuStates");
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
        /// Creates a small font asset that can satisfy title-bar layout for the contributed menu tests.
        /// </summary>
        /// <returns>Font asset with glyph metrics used by the contributed menu tests.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['B'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
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
