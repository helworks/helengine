using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the editor script assembly host loads multiple generated module assemblies into one shared resolver view.
    /// </summary>
    public sealed class EditorGameScriptAssemblyHostTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary output root that receives copied test assemblies.
        /// </summary>
        readonly string OutputRootPath;

        /// <summary>
        /// Initializes isolated project and output roots for multi-module host tests.
        /// </summary>
        public EditorGameScriptAssemblyHostTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-script-assembly-host-tests", Guid.NewGuid().ToString("N"));
            OutputRootPath = Path.Combine(Path.GetTempPath(), "helengine-script-assembly-host-output", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRootPath);
            Directory.CreateDirectory(OutputRootPath);
        }

        /// <summary>
        /// Deletes temporary host test state after each run.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures reloading two module descriptors registers both module ids with the shared script type resolver.
        /// </summary>
        [Fact]
        public void Reload_WhenTwoModuleAssembliesExist_RegistersBothAssembliesByModuleId() {
            string gameplayAssemblyPath = WriteModuleAssembly("gameplay");
            string gameplayUiAssemblyPath = WriteModuleAssembly("gameplay.ui");
            string[] resolvedTypeNames = LoadAndResolveTypeNames(gameplayAssemblyPath, gameplayUiAssemblyPath);

            Assert.Equal(typeof(TestMenuDefinitionProvider).FullName, resolvedTypeNames[0]);
            Assert.Equal(typeof(TestMenuDefinitionProvider).FullName, resolvedTypeNames[1]);
        }

        /// <summary>
        /// Ensures transient script snapshots stay outside the project tree so editor startup does not require project write access.
        /// </summary>
        [Fact]
        public void Constructor_DoesNotCreateScriptSnapshotsUnderProjectUserSettings() {
            using EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);

            string projectSnapshotRootPath = Path.Combine(ProjectRootPath, "user_settings", "script_snapshots");

            Assert.False(Directory.Exists(projectSnapshotRootPath));
        }

        /// <summary>
        /// Ensures editor-only assemblies expose discovered editor commands while runtime assemblies do not.
        /// </summary>
        [Fact]
        public void Reload_WhenEditorAssemblyContainsEditorCommand_ExposesItThroughCatalog() {
            string gameplayAssemblyPath = WriteModuleAssembly("gameplay");
            string editorAssemblyPath = WriteModuleAssembly("menu.tools");

            using EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);
            host.Reload([
                CreateAssemblyDescriptor("gameplay", gameplayAssemblyPath, EditorCodeModuleKind.Runtime),
                CreateAssemblyDescriptor("menu.tools", editorAssemblyPath, EditorCodeModuleKind.Editor)
            ]);

            IReadOnlyList<EditorProjectCommandDescriptor> commands = host.GetAvailableEditorCommands();
            EditorProjectCommandDescriptor command = Assert.Single(
                commands,
                descriptor => string.Equals(descriptor.CommandId, "menu.regenerate-demo-disc-main-menu", StringComparison.Ordinal));
            Assert.Equal("menu.regenerate-demo-disc-main-menu", command.CommandId);
            Assert.Equal("Regenerate Demo Disc Main Menu", command.DisplayName);
            Assert.Equal("menu.tools", command.ModuleId);
        }

        /// <summary>
        /// Ensures editor-only assemblies expose discovered contributed menu items while runtime assemblies do not.
        /// </summary>
        [Fact]
        public void Reload_WhenEditorAssemblyContainsMenuProvider_ExposesContributedMenuItems() {
            string gameplayAssemblyPath = WriteModuleAssembly("gameplay");
            string editorAssemblyPath = WriteModuleAssembly("menu.tools");

            using EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);
            host.Reload([
                CreateAssemblyDescriptor("gameplay", gameplayAssemblyPath, EditorCodeModuleKind.Runtime),
                CreateAssemblyDescriptor("menu.tools", editorAssemblyPath, EditorCodeModuleKind.Editor)
            ]);

            EditorMenuItemDescriptor menuItem = Assert.Single(
                host.GetAvailableEditorMenuItems(),
                descriptor => string.Equals(descriptor.MenuItemId, "demo.regenerate-main-menu", StringComparison.Ordinal));
            Assert.Equal("demo", menuItem.TopLevelMenuId);
            Assert.Equal("Demo", menuItem.TopLevelMenuLabel);
            Assert.Equal("Regenerate Main Menu...", menuItem.MenuItemLabel);
            Assert.Equal("menu.regenerate-demo-disc-main-menu", menuItem.CommandId);
        }

        /// <summary>
        /// Copies the current test assembly into one generated-module output directory and returns the copied path.
        /// </summary>
        /// <param name="moduleId">Module id that owns the copied assembly.</param>
        /// <returns>Absolute path to the copied assembly.</returns>
        string WriteModuleAssembly(string moduleId) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }

            string moduleOutputDirectoryPath = Path.Combine(OutputRootPath, moduleId, "Debug", "net9.0");
            Directory.CreateDirectory(moduleOutputDirectoryPath);
            string assemblyPath = Path.Combine(moduleOutputDirectoryPath, moduleId + ".dll");
            File.Copy(typeof(TestMenuDefinitionProvider).Assembly.Location, assemblyPath, true);
            return assemblyPath;
        }

        /// <summary>
        /// Loads two generated module assemblies, resolves their provider types, and returns the resolved full names.
        /// </summary>
        /// <param name="gameplayAssemblyPath">Generated gameplay assembly path.</param>
        /// <param name="gameplayUiAssemblyPath">Generated gameplay UI assembly path.</param>
        /// <returns>Resolved provider type names ordered by gameplay module, then gameplay UI module.</returns>
        string[] LoadAndResolveTypeNames(string gameplayAssemblyPath, string gameplayUiAssemblyPath) {
            if (string.IsNullOrWhiteSpace(gameplayAssemblyPath)) {
                throw new ArgumentException("Gameplay assembly path must be provided.", nameof(gameplayAssemblyPath));
            }
            if (string.IsNullOrWhiteSpace(gameplayUiAssemblyPath)) {
                throw new ArgumentException("Gameplay UI assembly path must be provided.", nameof(gameplayUiAssemblyPath));
            }

            using EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);
            host.Reload([
                CreateAssemblyDescriptor("gameplay", gameplayAssemblyPath, EditorCodeModuleKind.Runtime),
                CreateAssemblyDescriptor("gameplay.ui", gameplayUiAssemblyPath, EditorCodeModuleKind.Runtime)
            ]);

            return [
                ResolveTypeFullName(host.ScriptTypeResolver, typeof(TestMenuDefinitionProvider).FullName + ", gameplay"),
                ResolveTypeFullName(host.ScriptTypeResolver, typeof(TestMenuDefinitionProvider).FullName + ", gameplay.ui")
            ];
        }

        /// <summary>
        /// Resolves one persisted script type name and returns its full name.
        /// </summary>
        /// <param name="scriptTypeResolver">Resolver backed by the currently loaded script assemblies.</param>
        /// <param name="assemblyQualifiedTypeName">Assembly-qualified type name to resolve.</param>
        /// <returns>Resolved type full name.</returns>
        string ResolveTypeFullName(IScriptTypeResolver scriptTypeResolver, string assemblyQualifiedTypeName) {
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }
            if (string.IsNullOrWhiteSpace(assemblyQualifiedTypeName)) {
                throw new ArgumentException("Assembly-qualified type name must be provided.", nameof(assemblyQualifiedTypeName));
            }

            Type type = scriptTypeResolver.Resolve(assemblyQualifiedTypeName);
            return type.FullName;
        }

        /// <summary>
        /// Builds one editor-owned assembly descriptor from the supplied module metadata.
        /// </summary>
        /// <param name="moduleId">Stable authored module identifier.</param>
        /// <param name="assemblyPath">Absolute path to the built module assembly.</param>
        /// <param name="moduleKind">Declares whether the module is runtime or editor-only.</param>
        /// <returns>Editor-owned descriptor used by the script assembly host.</returns>
        EditorScriptAssemblyDescriptor CreateAssemblyDescriptor(string moduleId, string assemblyPath, EditorCodeModuleKind moduleKind) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }
            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Assembly path must be provided.", nameof(assemblyPath));
            }

            string outputDirectoryPath = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
            return new EditorScriptAssemblyDescriptor(moduleId, outputDirectoryPath, assemblyPath, moduleKind);
        }
    }
}
