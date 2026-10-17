using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Runs the editor's real Play script build (a genuine <c>dotnet build</c>, not a fake compiler) on a tiny fixture
    /// project and checks the published runtime modules against the player's own module layout. This is the contract
    /// that broke live: the build published <c>bin/&lt;id&gt;/Debug/net9.0/&lt;id&gt;.dll</c> while the player and the
    /// Play fingerprint check looked for <c>bin/&lt;id&gt;/&lt;id&gt;.dll</c>.
    /// </summary>
    public sealed class PlayScriptBuildPlayerLayoutTests : IDisposable {
        /// <summary>Platform identifier the fixture is built for.</summary>
        const string PlatformId = "ps2";

        /// <summary>Runtime module at the codebase root that other modules depend on.</summary>
        const string CoreModuleId = "PlayFixtureCore";

        /// <summary>Runtime module nested under the codebase that depends on <see cref="CoreModuleId"/>.</summary>
        const string LevelsModuleId = "PlayFixtureLevels";

        /// <summary>Editor-only module that Play must not compile or expect.</summary>
        const string ToolsModuleId = "PlayFixtureTools";

        /// <summary>
        /// Environment variable MSBuild mixes into its node handshake; nodes started under a different value are never
        /// reused, so a unique value forces a build to start a fresh node.
        /// </summary>
        const string NodeHandshakeSaltVariableName = "MSBUILDNODEHANDSHAKESALT";

        /// <summary>
        /// Longest time the fresh-node build may take. A loaded machine builds the fixture in well under this, while a
        /// build stuck on the idle node's handles would wait for the node's 15-minute idle timeout.
        /// </summary>
        static readonly TimeSpan FreshNodeBuildBound = TimeSpan.FromMinutes(3);

        /// <summary>Scratch root holding the fixture project and its Play output, deleted on dispose.</summary>
        readonly string ScratchRootPath;

        /// <summary>Authored fixture project root.</summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Creates a fixture project shaped like a real game: a root runtime module, a nested runtime module that depends
        /// on it, and a nested editor module.
        /// </summary>
        public PlayScriptBuildPlayerLayoutTests() {
            ScratchRootPath = Path.Combine(ResolvePlayTestsRootPath(), Guid.NewGuid().ToString("N"));
            ProjectRootPath = Path.Combine(ScratchRootPath, "project");
            string codebasePath = Path.Combine(ProjectRootPath, "assets", "codebase");
            WriteModule(codebasePath, CoreModuleId, "runtime", "[]", "namespace PlayFixture { public class CoreProbe { } }");
            WriteModule(
                Path.Combine(codebasePath, "Levels"),
                LevelsModuleId,
                "runtime",
                "[\"" + CoreModuleId + "\"]",
                "namespace PlayFixture { public class LevelProbe { public CoreProbe Core; } }");
            WriteModule(
                Path.Combine(codebasePath, "Tools"),
                ToolsModuleId,
                "editor",
                "[\"" + CoreModuleId + "\"]",
                "namespace PlayFixture { public class ToolProbe { } }");
        }

        /// <summary>
        /// Deletes the fixture project and its build output.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ScratchRootPath)) {
                Directory.Delete(ScratchRootPath, true);
            }
        }

        /// <summary>
        /// A real Play script build publishes each runtime module's assembly and symbols exactly where the player loads
        /// them from, and a second build with unchanged inputs is skipped by the fingerprint instead of recompiling.
        /// </summary>
        [Fact]
        public void RealPlayScriptBuild_PublishesModulesWherePlayerLoads_AndSkipsUnchangedRebuild() {
            EditorPlayScriptBuildService compiler = new EditorPlayScriptBuildService(ProjectRootPath, "PlayFixture");
            EditorPlayScriptBuilder builder = new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => string.Empty);

            EditorBuildExecutionResult first = builder.Build(PlatformId, new EditorProcessCancellation(), out string modulesRootPath, out _);

            Assert.True(first.Succeeded, first.Message);
            Assert.Equal(compiler.ResolveRuntimeModulesRootPath(PlatformId), modulesRootPath);
            string coreAssemblyPath = Path.Combine(modulesRootPath, CoreModuleId, CoreModuleId + ".dll");
            string levelsAssemblyPath = Path.Combine(modulesRootPath, LevelsModuleId, LevelsModuleId + ".dll");
            Assert.Equal(new[] { coreAssemblyPath, levelsAssemblyPath }, PlayerScriptModuleLoader.ResolveModuleAssemblyPaths(modulesRootPath));
            Assert.True(File.Exists(Path.ChangeExtension(coreAssemblyPath, ".pdb")));
            Assert.True(File.Exists(Path.ChangeExtension(levelsAssemblyPath, ".pdb")));
            Assert.False(Directory.Exists(Path.Combine(modulesRootPath, ToolsModuleId)));
            DateTime firstCoreWriteTimeUtc = File.GetLastWriteTimeUtc(coreAssemblyPath);

            EditorBuildExecutionResult second = builder.Build(PlatformId, new EditorProcessCancellation(), out string secondModulesRootPath, out _);

            Assert.True(second.Succeeded, second.Message);
            Assert.Contains("script build skipped", second.Message);
            Assert.Equal(modulesRootPath, secondModulesRootPath);
            Assert.Equal(firstCoreWriteTimeUtc, File.GetLastWriteTimeUtc(coreAssemblyPath));
        }

        /// <summary>
        /// A real Play script build keeps its MSBuild intermediates in the project's Play output, so after one script
        /// edit only the edited module is recompiled: the untouched module's published assembly is left as it was, and
        /// the edited module still lands where the player loads it.
        /// </summary>
        [Fact]
        public void RealPlayScriptBuild_AfterOneScriptEdit_RecompilesOnlyTheEditedModule() {
            EditorPlayScriptBuildService compiler = new EditorPlayScriptBuildService(ProjectRootPath, "PlayFixture");
            EditorPlayScriptBuilder builder = new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => string.Empty);
            EditorBuildExecutionResult first = builder.Build(PlatformId, new EditorProcessCancellation(), out string modulesRootPath, out _);
            Assert.True(first.Succeeded, first.Message);
            string coreAssemblyPath = Path.Combine(modulesRootPath, CoreModuleId, CoreModuleId + ".dll");
            string levelsAssemblyPath = Path.Combine(modulesRootPath, LevelsModuleId, LevelsModuleId + ".dll");
            DateTime firstCoreWriteTimeUtc = File.GetLastWriteTimeUtc(coreAssemblyPath);
            byte[] firstLevelsBytes = File.ReadAllBytes(levelsAssemblyPath);

            File.WriteAllText(
                Path.Combine(ProjectRootPath, "assets", "codebase", "Levels", LevelsModuleId + ".cs"),
                "namespace PlayFixture { public class LevelProbe { public CoreProbe Core; public int Depth; } }");
            EditorBuildExecutionResult second = builder.Build(PlatformId, new EditorProcessCancellation(), out string secondModulesRootPath, out _);

            Assert.True(second.Succeeded, second.Message);
            Assert.DoesNotContain("script build skipped", second.Message);
            Assert.Equal(modulesRootPath, secondModulesRootPath);
            Assert.Equal(new[] { coreAssemblyPath, levelsAssemblyPath }, PlayerScriptModuleLoader.ResolveModuleAssemblyPaths(modulesRootPath));
            Assert.Equal(firstCoreWriteTimeUtc, File.GetLastWriteTimeUtc(coreAssemblyPath));
            Assert.NotEqual(firstLevelsBytes, File.ReadAllBytes(levelsAssemblyPath));
            Assert.True(File.Exists(Path.Combine(
                compiler.ResolvePlayOutputRootPath(PlatformId), "generated_code", "obj", CoreModuleId, "project.assets.json")));
        }

        /// <summary>
        /// A real incremental Play script build follows module set changes: a removed runtime module's folder disappears
        /// from the player's module root, and a newly added runtime module, whose project was never restored, still builds
        /// and lands where the player loads it.
        /// </summary>
        [Fact]
        public void RealPlayScriptBuild_WhenModulesAreAddedAndRemoved_PublishesExactlyTheCurrentModules() {
            EditorPlayScriptBuildService compiler = new EditorPlayScriptBuildService(ProjectRootPath, "PlayFixture");
            EditorPlayScriptBuilder builder = new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => string.Empty);
            EditorBuildExecutionResult first = builder.Build(PlatformId, new EditorProcessCancellation(), out string modulesRootPath, out _);
            Assert.True(first.Succeeded, first.Message);

            string codebasePath = Path.Combine(ProjectRootPath, "assets", "codebase");
            Directory.Delete(Path.Combine(codebasePath, "Levels"), true);
            const string ArenaModuleId = "PlayFixtureArena";
            WriteModule(
                Path.Combine(codebasePath, "Arena"),
                ArenaModuleId,
                "runtime",
                "[\"" + CoreModuleId + "\"]",
                "namespace PlayFixture { public class ArenaProbe { public CoreProbe Core; } }");
            EditorBuildExecutionResult second = builder.Build(PlatformId, new EditorProcessCancellation(), out string secondModulesRootPath, out _);

            Assert.True(second.Succeeded, second.Message);
            Assert.Equal(modulesRootPath, secondModulesRootPath);
            Assert.Equal(
                new[] {
                    Path.Combine(modulesRootPath, ArenaModuleId, ArenaModuleId + ".dll"),
                    Path.Combine(modulesRootPath, CoreModuleId, CoreModuleId + ".dll")
                },
                PlayerScriptModuleLoader.ResolveModuleAssemblyPaths(modulesRootPath));
            Assert.False(Directory.Exists(Path.Combine(modulesRootPath, LevelsModuleId)));
        }

        /// <summary>
        /// A real Play build that restores for changed project inputs and then fails to compile must not leave the
        /// previous restore stamp behind: the restore already rewrote the restore output for the new inputs, so reverting
        /// to the old inputs must restore again instead of skipping restore against the new inputs' output.
        /// </summary>
        [Fact]
        public void RealPlayScriptBuild_WhenRestoringBuildFailsToCompile_DropsThePreviousRestoreStamp() {
            EditorPlayScriptBuildService compiler = new EditorPlayScriptBuildService(ProjectRootPath, "PlayFixture");
            EditorPlayScriptBuilder builder = new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => string.Empty);
            EditorBuildExecutionResult first = builder.Build(PlatformId, new EditorProcessCancellation(), out _, out _);
            Assert.True(first.Succeeded, first.Message);
            string restoreStampFilePath = Path.Combine(
                compiler.ResolvePlayOutputRootPath(PlatformId),
                EditorDotNetScriptBuildTool.IncrementalRestoreStampFileName);
            Assert.True(File.Exists(restoreStampFilePath));

            // Dropping the Levels -> Core dependency changes the generated Levels project (a restore input), and the
            // edited source no longer compiles.
            string levelsFolderPath = Path.Combine(ProjectRootPath, "assets", "codebase", "Levels");
            WriteModule(levelsFolderPath, LevelsModuleId, "runtime", "[]", "namespace PlayFixture { public class LevelProbe { public CoreProbe Core; } }");
            EditorBuildExecutionResult failed = builder.Build(PlatformId, new EditorProcessCancellation(), out _, out _);

            Assert.False(failed.Succeeded);
            Assert.False(File.Exists(restoreStampFilePath));

            WriteModule(
                levelsFolderPath,
                LevelsModuleId,
                "runtime",
                "[\"" + CoreModuleId + "\"]",
                "namespace PlayFixture { public class LevelProbe { public CoreProbe Core; } }");
            EditorBuildExecutionResult reverted = builder.Build(PlatformId, new EditorProcessCancellation(), out _, out _);

            Assert.True(reverted.Succeeded, reverted.Message);
            Assert.True(File.Exists(restoreStampFilePath));
        }

        /// <summary>
        /// A real Play script build that has to start a fresh MSBuild worker node still returns once <c>dotnet</c> exits,
        /// with its compiler errors. The fresh node is kept warm for reuse and holds the build's standard output and error
        /// handles for its whole idle lifetime, so a build that waits for those streams to close instead of for the process
        /// hangs for as long as the node lives. A unique node handshake salt makes every idle node on the machine
        /// incompatible, which forces the fresh node; the bound fails the test long before the node would idle out.
        /// </summary>
        [Fact]
        public void RealPlayScriptBuild_WhenItStartsAFreshReusableNode_ReturnsWithTheCompilerErrors() {
            WriteModule(
                Path.Combine(ProjectRootPath, "assets", "codebase", "Levels"),
                LevelsModuleId,
                "runtime",
                "[\"" + CoreModuleId + "\"]",
                "namespace PlayFixture { public class LevelProbe { public MissingFreshNodeProbe Missing; } }");
            EditorPlayScriptBuildService compiler = new EditorPlayScriptBuildService(ProjectRootPath, "PlayFixture");
            EditorPlayScriptBuilder builder = new EditorPlayScriptBuilder(ProjectRootPath, compiler, platformId => string.Empty);
            string previousSalt = Environment.GetEnvironmentVariable(NodeHandshakeSaltVariableName);
            Environment.SetEnvironmentVariable(NodeHandshakeSaltVariableName, "helengine-play-tests-" + Guid.NewGuid().ToString("N"));
            try {
                Task<EditorBuildExecutionResult> build = Task.Run(() => builder.Build(PlatformId, new EditorProcessCancellation(), out _, out _));

                Assert.True(build.Wait(FreshNodeBuildBound), $"The Play script build did not return within {FreshNodeBuildBound}.");
                Assert.False(build.Result.Succeeded);
                Assert.Contains("MissingFreshNodeProbe", build.Result.Message);
            } finally {
                Environment.SetEnvironmentVariable(NodeHandshakeSaltVariableName, previousSalt);
            }
        }

        /// <summary>
        /// Resolves the user-designated temporary root for Play build fixtures independently of where the test
        /// binaries were built, so directing managed artifacts outside the checkout does not break these tests.
        /// </summary>
        /// <returns>Absolute scratch root for Play build fixtures.</returns>
        static string ResolvePlayTestsRootPath() {
            return @"C:\temp\helengine-windows\play-script-tests";
        }

        /// <summary>
        /// Writes one code module folder with its manifest and a single source file.
        /// </summary>
        /// <param name="moduleFolderPath">Absolute module folder path.</param>
        /// <param name="moduleId">Stable module identifier.</param>
        /// <param name="moduleKind">Manifest module kind: <c>runtime</c> or <c>editor</c>.</param>
        /// <param name="dependencyModuleIdsJson">JSON array of dependency module identifiers.</param>
        /// <param name="source">C# source written to the module's only script file.</param>
        static void WriteModule(string moduleFolderPath, string moduleId, string moduleKind, string dependencyModuleIdsJson, string source) {
            Directory.CreateDirectory(moduleFolderPath);
            File.WriteAllText(
                Path.Combine(moduleFolderPath, "code.module.json"),
                "{ \"moduleId\": \"" + moduleId + "\", \"dependencyModuleIds\": " + dependencyModuleIdsJson
                    + ", \"loadScopes\": [\"always-loaded\"], \"moduleKind\": \"" + moduleKind + "\" }");
            File.WriteAllText(Path.Combine(moduleFolderPath, moduleId + ".cs"), source);
        }
    }
}
