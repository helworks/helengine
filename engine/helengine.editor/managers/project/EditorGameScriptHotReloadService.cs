namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution and reloads the resulting game assembly.
    /// </summary>
    public sealed class EditorGameScriptHotReloadService : IDisposable, IEditorScriptComponentCatalogProvider {
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

                List<ScriptAssemblyDescriptor> assemblies = new List<ScriptAssemblyDescriptor>(GameSolutionService.GeneratedModuleProjects.Count);
                for (int index = 0; index < GameSolutionService.GeneratedModuleProjects.Count; index++) {
                    EditorGeneratedCodeModuleProject moduleProject = GameSolutionService.GeneratedModuleProjects[index];
                    assemblies.Add(new ScriptAssemblyDescriptor(
                        moduleProject.ModuleId,
                        moduleProject.OutputDirectoryPath,
                        Path.Combine(moduleProject.OutputDirectoryPath, moduleProject.ModuleId + ".dll")));
                }

                AssemblyHost.Reload(assemblies);

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

        /// <summary>
        /// Returns the addable script components discovered from the current loaded assembly.
        /// </summary>
        /// <param name="entity">Entity that will receive one selected component.</param>
        /// <returns>Descriptors discovered from the current loaded assembly.</returns>
        public IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity) {
            return AssemblyHost.GetAvailableScriptComponents(entity);
        }
    }
}
