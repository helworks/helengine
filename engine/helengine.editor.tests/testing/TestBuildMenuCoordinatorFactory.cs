using helengine.platforms;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Builds fully wired <see cref="EditorBuildMenuCoordinator"/> instances for editor-session tests that
    /// only exercise the platform-menu rules. The coordinator also owns build-executor creation and therefore
    /// requires the project identity, importer and shader collaborators a real build would consume; this
    /// factory supplies test-scoped equivalents so each test file does not have to assemble them by hand.
    /// </summary>
    internal static class TestBuildMenuCoordinatorFactory {
        /// <summary>
        /// Creates one build-menu coordinator bound to the supplied temporary project root.
        /// </summary>
        /// <param name="platformProviderResolver">Resolver that discovers the platforms installed for the test engine version.</param>
        /// <param name="requiredEngineVersion">Exact engine version the test project declares.</param>
        /// <param name="projectRootPath">Absolute temporary project root used by the test.</param>
        /// <returns>Build-menu coordinator usable by platform-menu and build-dialog tests.</returns>
        public static EditorBuildMenuCoordinator Create(
            AvailablePlatformProviderResolver platformProviderResolver,
            string requiredEngineVersion,
            string projectRootPath) {
            if (platformProviderResolver == null) {
                throw new ArgumentNullException(nameof(platformProviderResolver));
            }
            if (string.IsNullOrWhiteSpace(requiredEngineVersion)) {
                throw new ArgumentException("Required engine version must be provided.", nameof(requiredEngineVersion));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            EditorGameSolutionService solutionService = new EditorGameSolutionService(projectRootPath, "TestProject", new TestEditorIdeLauncher());
            EditorGameScriptHotReloadService scriptHotReloadService = new EditorGameScriptHotReloadService(
                solutionService,
                new TestScriptBuildTool(EditorBuildExecutionResult.Success("Test build tool succeeded.")),
                new TestEditorScriptAssemblyHost());

            return new EditorBuildMenuCoordinator(
                platformProviderResolver,
                requiredEngineVersion,
                projectRootPath,
                "TestProject",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                PackagedFontAssetFactory.Create(),
                scriptHotReloadService,
                TestGeneratedAssetGraph.CreateShaderLibrary());
        }
    }
}
