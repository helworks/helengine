using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor session loads available project script libraries during startup.
    /// </summary>
    public sealed class EditorSessionProjectLibraryStartupTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current startup-bootstrap test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root containing a minimal user script library.
        /// </summary>
        public EditorSessionProjectLibraryStartupTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-project-library-startup-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts"));
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { }");
        }

        /// <summary>
        /// Deletes the temporary project root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures startup loads available project script libraries and applies contributed project menus immediately.
        /// </summary>
        [Fact]
        public void StartupProjectLibraryBootstrap_WhenLibrariesAreAvailable_LoadsAssembliesAndAppliesProjectMenus() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestEditorIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok"));
            TestEditorScriptAssemblyHost assemblyHost = new TestEditorScriptAssemblyHost {
                AvailableEditorMenuItems = [
                    new EditorMenuItemDescriptor(
                        "demo",
                        "Demo",
                        100,
                        "demo.generate-rendering-scenes",
                        "Generate Rendering Scenes",
                        200,
                        "menu.generate-rendering-scenes")
                ]
            };
            EditorGameScriptHotReloadService hotReloadService = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);
            IReadOnlyList<EditorMenuItemDescriptor> appliedMenus = Array.Empty<EditorMenuItemDescriptor>();
            MethodInfo bootstrapMethod = typeof(EditorSession).GetMethod("LoadProjectLibrariesOnStartup", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(bootstrapMethod);

            EditorBuildExecutionResult result = Assert.IsType<EditorBuildExecutionResult>(
                bootstrapMethod.Invoke(null, [hotReloadService, new Action<IReadOnlyList<EditorMenuItemDescriptor>>(menuItems => appliedMenus = menuItems)]));

            Assert.True(result.Succeeded);
            Assert.Equal(1, assemblyHost.ReloadCount);
            EditorMenuItemDescriptor menuItem = Assert.Single(appliedMenus);
            Assert.Equal("demo.generate-rendering-scenes", menuItem.MenuItemId);
        }
    }
}
