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
                new ScriptAssemblyDescriptor("gameplay", Path.GetDirectoryName(gameplayAssemblyPath), gameplayAssemblyPath),
                new ScriptAssemblyDescriptor("gameplay.ui", Path.GetDirectoryName(gameplayUiAssemblyPath), gameplayUiAssemblyPath)
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
    }
}
