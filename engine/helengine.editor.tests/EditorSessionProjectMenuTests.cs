using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.components.ui;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session integration for project-authored contributed title-bar menus.
    /// </summary>
    public sealed class EditorSessionProjectMenuTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current editor-session project menu tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the temporary project tree required by the editor-session project menu tests.
        /// </summary>
        public EditorSessionProjectMenuTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-project-menu-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts"));
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { }");

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            TestEditorCommand.Reset();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures each successful script reload replaces the contributed project menu set instead of appending duplicate Demo entries.
        /// </summary>
        [Fact]
        public void HandleBuildScriptsRequested_WhenBuildSucceeds_ReplacesContributedMenusWithoutDuplicates() {
            TestEditorScriptAssemblyHost assemblyHost = CreateAssemblyHost();
            EditorGameScriptHotReloadService scriptHotReloadService = CreateScriptHotReloadService(assemblyHost);
            EditorSession session = CreateSession(scriptHotReloadService);
            EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");

            InvokePrivate(session, "HandleBuildScriptsRequested");
            int firstChildCount = titleBar.Entity.Children.Count;
            InvokePrivate(session, "HandleBuildScriptsRequested");

            Assert.Equal(2, assemblyHost.ReloadCount);
            Assert.Single(GetPrivateField<List<EditorTitleBarProjectMenuState>>(titleBar, "ProjectMenuStates"));
            Assert.Equal(firstChildCount, titleBar.Entity.Children.Count);
        }

        /// <summary>
        /// Ensures activating one contributed project menu item executes its mapped project-authored editor command.
        /// </summary>
        [Fact]
        public void HandleProjectMenuItemRequested_WhenMenuItemExists_ExecutesMappedEditorCommand() {
            TestEditorScriptAssemblyHost assemblyHost = CreateAssemblyHost();
            EditorGameScriptHotReloadService scriptHotReloadService = CreateScriptHotReloadService(assemblyHost);
            EditorSession session = CreateSession(scriptHotReloadService);

            InvokePrivate(session, "HandleProjectMenuItemRequested", "demo.regenerate-main-menu");

            Assert.Equal(1, TestEditorCommand.ExecuteCount);
        }

        /// <summary>
        /// Creates one minimally initialized editor session containing the collaborators required by the project-menu flow.
        /// </summary>
        /// <param name="scriptHotReloadService">Hot-reload service surfaced by the test session.</param>
        /// <returns>Editor session configured for project-menu tests.</returns>
        EditorSession CreateSession(EditorGameScriptHotReloadService scriptHotReloadService) {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SetPrivateField(session, "projectPath", TempProjectRootPath);
            SetPrivateField(session, "titleBar", new EditorTitleBar(CreateFont(), 1280, 720, "helengine"));
            SetPrivateField(session, "scriptHotReloadService", scriptHotReloadService);
            return session;
        }

        /// <summary>
        /// Creates one hot-reload service backed by fake build and assembly-host collaborators.
        /// </summary>
        /// <param name="assemblyHost">Fake assembly host surfaced through the hot-reload service.</param>
        /// <returns>Hot-reload service configured for project-menu tests.</returns>
        EditorGameScriptHotReloadService CreateScriptHotReloadService(TestEditorScriptAssemblyHost assemblyHost) {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestEditorIdeLauncher());
            return new EditorGameScriptHotReloadService(
                solutionService,
                new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok")),
                assemblyHost);
        }

        /// <summary>
        /// Creates one fake script assembly host with matching contributed menu and command catalogs.
        /// </summary>
        /// <returns>Fake script assembly host configured for project-menu tests.</returns>
        TestEditorScriptAssemblyHost CreateAssemblyHost() {
            return new TestEditorScriptAssemblyHost {
                AvailableEditorCommands = [
                    new EditorProjectCommandDescriptor(
                        "menu.regenerate-demo-disc-main-menu",
                        "Regenerate Demo Disc Main Menu",
                        typeof(TestEditorCommand),
                        "menu.tools")
                ],
                AvailableEditorMenuItems = [
                    new EditorMenuItemDescriptor(
                        "demo",
                        "Demo",
                        100,
                        "demo.regenerate-main-menu",
                        "Regenerate Main Menu...",
                        100,
                        "menu.regenerate-demo-disc-main-menu")
                ]
            };
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
        /// Assigns one non-public instance field for the supplied target object.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">New field value.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy title-bar layout for the session test harness.
        /// </summary>
        /// <returns>Font asset with glyph metrics used by the session test harness.</returns>
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
