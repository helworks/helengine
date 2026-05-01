namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution and reloads the resulting game assembly.
    /// </summary>
    public sealed class EditorGameScriptHotReloadService : IDisposable {
        /// <summary>
        /// Generator used to rewrite the script solution before each build.
        /// </summary>
        readonly EditorGameSolutionService GameSolutionService;

        /// <summary>
        /// Tool used to build the generated solution.
        /// </summary>
        readonly IEditorScriptBuildTool BuildTool;

        /// <summary>
        /// Host used to import the latest built assembly.
        /// </summary>
        readonly IEditorScriptAssemblyHost AssemblyHost;

        /// <summary>
        /// Initializes one hot-reload service for the current game project.
        /// </summary>
        /// <param name="gameSolutionService">Generator used to write the current scripting solution.</param>
        /// <param name="buildTool">Build tool used to compile the generated solution.</param>
        /// <param name="assemblyHost">Host used to import the freshly built assembly.</param>
        public EditorGameScriptHotReloadService(EditorGameSolutionService gameSolutionService, IEditorScriptBuildTool buildTool, IEditorScriptAssemblyHost assemblyHost) {
            GameSolutionService = gameSolutionService ?? throw new ArgumentNullException(nameof(gameSolutionService));
            BuildTool = buildTool ?? throw new ArgumentNullException(nameof(buildTool));
            AssemblyHost = assemblyHost ?? throw new ArgumentNullException(nameof(assemblyHost));
        }

        /// <summary>
        /// Generates, builds, and imports the current game scripting assembly.
        /// </summary>
        /// <returns>Structured result describing the build-and-reload outcome.</returns>
        public EditorBuildExecutionResult BuildAndReload() {
            try {
                string solutionPath = GameSolutionService.GenerateSolutionFiles();
                EditorBuildExecutionResult buildResult = BuildTool.Build(solutionPath);
                if (!buildResult.Succeeded) {
                    return buildResult;
                }

                AssemblyHost.Reload(
                    GameSolutionService.GeneratedOutputDirectoryPath,
                    GameSolutionService.GeneratedOutputAssemblyPath);

                return EditorBuildExecutionResult.Success($"Scripts hot-reloaded: {GameSolutionService.GeneratedOutputAssemblyPath}");
            } catch (Exception ex) {
                return EditorBuildExecutionResult.Failure($"Script hot reload failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Releases the currently loaded script assembly.
        /// </summary>
        public void Dispose() {
            AssemblyHost.Dispose();
        }
    }
}
