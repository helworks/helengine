using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies script build staleness is decided per module, so editor-only engine changes leave runtime modules alone.
    /// </summary>
    public sealed class EditorScriptBuildModuleFingerprintTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Creates one project with a runtime module and an editor module that depends on it.
        /// </summary>
        public EditorScriptBuildModuleFingerprintTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-module-fingerprint-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "codebase", "gameplay"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "codebase", "menu.tools"));
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "codebase", "gameplay", "Player.cs"), "public sealed class Player { }");
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "codebase", "gameplay", "code.module.json"), """
{
  "moduleId": "gameplay",
  "dependencyModuleIds": [],
  "loadScopes": [ "always-loaded" ]
}
""");
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "codebase", "menu.tools", "RegenerateCommand.cs"), "public sealed class RegenerateCommand { }");
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "codebase", "menu.tools", "code.module.json"), """
{
  "moduleId": "menu.tools",
  "dependencyModuleIds": [ "gameplay" ],
  "loadScopes": [ "always-loaded" ],
  "moduleKind": "editor"
}
""");
        }

        /// <summary>
        /// Deletes temporary test state.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a runtime module never carries the editor assemblies in its build inputs, so rebuilding the
        /// editor cannot mark gameplay code stale, while an editor module does carry them.
        /// </summary>
        [Fact]
        public void DescribeModuleBuildInputs_SeparatesEditorAssembliesFromRuntimeModules() {
            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            service.GenerateSolutionFiles();

            IReadOnlyList<KeyValuePair<string, EditorScriptBuildInputs>> moduleInputs = service.DescribeModuleBuildInputs();

            EditorScriptBuildInputs gameplay = Single(moduleInputs, "gameplay");
            EditorScriptBuildInputs menuTools = Single(moduleInputs, "menu.tools");

            Assert.DoesNotContain(gameplay.ReferencedAssemblyPaths, path => HasFileName(path, "helengine.editor.dll"));
            Assert.DoesNotContain(gameplay.ReferencedAssemblyPaths, path => HasFileName(path, "AssimpNetter.dll"));
            Assert.Contains(gameplay.ReferencedAssemblyPaths, path => HasFileName(path, "helengine.core.dll"));

            Assert.Contains(menuTools.ReferencedAssemblyPaths, path => HasFileName(path, "helengine.editor.dll"));
            Assert.Contains(menuTools.ReferencedAssemblyPaths, path => HasFileName(path, "AssimpNetter.dll"));
        }

        /// <summary>
        /// Ensures each module only watches its own sources, so editing one module's code cannot mark another stale.
        /// </summary>
        [Fact]
        public void ComputeAll_WhenOneModuleSourceChanges_OnlyThatModuleFingerprintChanges() {
            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            service.GenerateSolutionFiles();

            Dictionary<string, string> before = EditorScriptBuildFingerprint.ComputeAll(service.DescribeModuleBuildInputs());

            string editedPath = Path.Combine(TempProjectRootPath, "assets", "codebase", "menu.tools", "RegenerateCommand.cs");
            File.WriteAllText(editedPath, "public sealed class RegenerateCommand { public int Added; }");
            File.SetLastWriteTimeUtc(editedPath, DateTime.UtcNow.AddSeconds(5));

            Dictionary<string, string> after = EditorScriptBuildFingerprint.ComputeAll(service.DescribeModuleBuildInputs());

            Assert.Equal(before["gameplay"], after["gameplay"]);
            Assert.NotEqual(before["menu.tools"], after["menu.tools"]);
            Assert.False(EditorScriptBuildFingerprint.AllModulesMatch(before, after));
        }

        /// <summary>
        /// Ensures the per-module store round-trips and that added, removed, or changed modules all read as stale.
        /// </summary>
        [Fact]
        public void StoredModules_RoundTripAndDetectEveryKindOfDifference() {
            string filePath = Path.Combine(TempProjectRootPath, "cache", "script-build.fingerprint");
            Dictionary<string, string> current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["gameplay"] = "aaa",
                ["menu.tools"] = "bbb"
            };

            Assert.False(EditorScriptBuildFingerprint.AllModulesMatch(EditorScriptBuildFingerprint.ReadStoredModules(filePath), current));

            EditorScriptBuildFingerprint.WriteStoredModules(filePath, current);
            Dictionary<string, string> stored = EditorScriptBuildFingerprint.ReadStoredModules(filePath);

            Assert.True(EditorScriptBuildFingerprint.AllModulesMatch(stored, current));
            Assert.False(EditorScriptBuildFingerprint.AllModulesMatch(stored, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["gameplay"] = "aaa",
                ["menu.tools"] = "changed"
            }));
            Assert.False(EditorScriptBuildFingerprint.AllModulesMatch(stored, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["gameplay"] = "aaa"
            }));
            Assert.False(EditorScriptBuildFingerprint.AllModulesMatch(stored, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["gameplay"] = "aaa",
                ["menu.tools"] = "bbb",
                ["added"] = "ccc"
            }));
        }

        /// <summary>
        /// Ensures a fingerprint file left by an older single-value format is treated as absent rather than trusted.
        /// </summary>
        [Fact]
        public void ReadStoredModules_WhenFileUsesTheOlderSingleValueFormat_ReadsAsEmpty() {
            string filePath = Path.Combine(TempProjectRootPath, "cache", "script-build.fingerprint");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, new string('a', 64) + Environment.NewLine);

            Assert.Empty(EditorScriptBuildFingerprint.ReadStoredModules(filePath));
        }

        static bool HasFileName(string path, string fileName) {
            return string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
        }

        static EditorScriptBuildInputs Single(IReadOnlyList<KeyValuePair<string, EditorScriptBuildInputs>> moduleInputs, string moduleId) {
            foreach (KeyValuePair<string, EditorScriptBuildInputs> entry in moduleInputs) {
                if (string.Equals(entry.Key, moduleId, StringComparison.OrdinalIgnoreCase)) {
                    return entry.Value;
                }
            }

            throw new InvalidOperationException($"No module inputs for '{moduleId}'.");
        }

        /// <summary>
        /// Minimal IDE launcher used to satisfy the solution service constructor.
        /// </summary>
        sealed class TestIdeLauncher : IEditorIdeLauncher {
            public void OpenSolution(string solutionPath) {
            }
        }
    }
}
