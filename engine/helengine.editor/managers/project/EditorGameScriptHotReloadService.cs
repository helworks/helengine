namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution and reloads the resulting game assembly.
    /// </summary>
    public sealed class EditorGameScriptHotReloadService : IDisposable, IEditorScriptComponentCatalogProvider, IEditorProjectCommandCatalogProvider, IEditorProjectMenuCatalogProvider {
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
        /// Generates, builds when inputs changed, and imports the current game scripting assembly.
        /// </summary>
        /// <returns>Structured result describing the build-and-reload outcome.</returns>
        public EditorBuildExecutionResult BuildAndReload() {
            return BuildAndReload(false);
        }

        /// <summary>
        /// Generates, builds, and imports the current game scripting assembly.
        /// </summary>
        /// <param name="forceBuild">True to run the build tool even when the recorded build fingerprint still matches.</param>
        /// <returns>Structured result describing the build-and-reload outcome.</returns>
        public EditorBuildExecutionResult BuildAndReload(bool forceBuild) {
            try {
                if (!GameSolutionService.HasCodeModules) {
                    return EditorBuildExecutionResult.Success("Script hot reload skipped: the project declares no code modules.");
                }

                using EditorGeneratedCodeWorkspaceLease workspaceLease = GameSolutionService.AcquireWorkspaceLease();
                GameSolutionService.GenerateSolutionFiles(workspaceLease);
                // Build the production-only filter so test project compile errors never block script loading.
                string solutionPath = GameSolutionService.GeneratedProductionSolutionFilterFilePath;
                List<EditorScriptAssemblyDescriptor> assemblies = DescribeModuleAssemblies();
                string fingerprintFilePath = Path.Combine(GameSolutionService.GeneratedMetadataDirectoryPath, EditorScriptBuildFingerprint.FileName);
                string fingerprint = EditorScriptBuildFingerprint.Compute(GameSolutionService.DescribeBuildInputs());
                if (!forceBuild
                    && EditorScriptBuildFingerprint.MatchesStored(fingerprintFilePath, fingerprint)
                    && AllAssembliesExist(assemblies)) {
                    AssemblyHost.Reload(assemblies);
                    return EditorBuildExecutionResult.Success($"Scripts up to date, reloaded without rebuilding: {GameSolutionService.GeneratedOutputAssemblyPath}");
                }

                EditorBuildExecutionResult buildResult;
                if (BuildTool is IEditorScriptBuildToolWithWorkspaceLease leasedBuildTool) {
                    buildResult = leasedBuildTool.Build(
                        solutionPath,
                        GameSolutionService.UsesInvocationOutputOverride
                            ? GameSolutionService.GeneratedExecutionOutputRootPath
                            : string.Empty,
                        workspaceLease);
                } else if (BuildTool is IEditorScriptBuildToolWithOutputRoot isolatedBuildTool
                    && GameSolutionService.UsesInvocationOutputOverride) {
                    buildResult = isolatedBuildTool.Build(solutionPath, GameSolutionService.GeneratedExecutionOutputRootPath);
                } else {
                    buildResult = BuildTool.Build(solutionPath);
                }
                if (!buildResult.Succeeded) {
                    return buildResult;
                }

                EditorScriptBuildFingerprint.WriteStored(fingerprintFilePath, fingerprint);
                AssemblyHost.Reload(assemblies);

                return EditorBuildExecutionResult.Success($"Scripts hot-reloaded: {GameSolutionService.GeneratedOutputAssemblyPath}");
            } catch (Exception ex) {
                return EditorBuildExecutionResult.Failure($"Script hot reload failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Describes the module assemblies the current generated solution produces.
        /// </summary>
        /// <returns>One descriptor per generated production module.</returns>
        List<EditorScriptAssemblyDescriptor> DescribeModuleAssemblies() {
            IReadOnlyList<EditorGeneratedCodeModuleProject> moduleProjects = GameSolutionService.GeneratedModuleProjects;
            List<EditorScriptAssemblyDescriptor> assemblies = new List<EditorScriptAssemblyDescriptor>(moduleProjects.Count);
            for (int index = 0; index < moduleProjects.Count; index++) {
                EditorGeneratedCodeModuleProject moduleProject = moduleProjects[index];
                assemblies.Add(new EditorScriptAssemblyDescriptor(
                    moduleProject.ModuleId,
                    moduleProject.OutputDirectoryPath,
                    Path.Combine(moduleProject.OutputDirectoryPath, moduleProject.ModuleId + ".dll"),
                    moduleProject.ModuleKind));
            }

            return assemblies;
        }

        /// <summary>
        /// Determines whether every module assembly from a previous build is still present on disk.
        /// </summary>
        /// <param name="assemblies">Module assembly descriptors to check.</param>
        /// <returns>True when every assembly file exists.</returns>
        static bool AllAssembliesExist(List<EditorScriptAssemblyDescriptor> assemblies) {
            for (int index = 0; index < assemblies.Count; index++) {
                if (!File.Exists(assemblies[index].AssemblyPath)) {
                    return false;
                }
            }

            return assemblies.Count > 0;
        }

        /// <summary>
        /// Releases the currently loaded script assembly.
        /// </summary>
        public void Dispose() {
            AssemblyHost.Dispose();
        }

        /// <summary>
        /// Gets the current script type resolver backed by the loaded script assemblies.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver => AssemblyHost.ScriptTypeResolver;

        /// <summary>
        /// Returns the addable script components discovered from the current loaded assembly.
        /// </summary>
        /// <param name="entity">Entity that will receive one selected component.</param>
        /// <returns>Descriptors discovered from the current loaded assembly.</returns>
        public IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity) {
            return AssemblyHost.GetAvailableScriptComponents(entity);
        }

        /// <summary>
        /// Returns the project-authored editor commands discovered from the current loaded editor assemblies.
        /// </summary>
        /// <returns>Discovered project-authored editor commands.</returns>
        public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
            return AssemblyHost.GetAvailableEditorCommands();
        }

        /// <summary>
        /// Returns the project-authored editor menu items discovered from the current loaded editor assemblies.
        /// </summary>
        /// <returns>Discovered project-authored editor menu item descriptors.</returns>
        public IReadOnlyList<EditorMenuItemDescriptor> GetAvailableEditorMenuItems() {
            return AssemblyHost.GetAvailableEditorMenuItems();
        }
    }
}
