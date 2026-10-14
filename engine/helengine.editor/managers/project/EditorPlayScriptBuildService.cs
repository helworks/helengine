namespace helengine.editor {
    /// <summary>
    /// Compiles a project's runtime script modules for the editor's Play button. The modules are built with the
    /// selected platform's gameplay preprocessor symbols, so <c>#if</c>-gated platform code is included, into a
    /// per-platform output under <c>cache/generated_code/play/&lt;platformId&gt;</c> that never touches the editor's
    /// hot-reload output. MSBuild builds directly into that stable output, so its intermediates persist and later Play
    /// builds only recompile the modules whose inputs changed. The engine itself is never recompiled.
    /// </summary>
    public sealed class EditorPlayScriptBuildService : IEditorPlayScriptCompiler {
        /// <summary>
        /// Folder beneath the generated-code workspace that holds per-platform Play compiler outputs.
        /// </summary>
        public const string PlayFolderName = "play";

        /// <summary>
        /// Prefix of the generated-code route that owns the Play solution and project files for one platform.
        /// </summary>
        const string PlayRouteIdPrefix = "play-";

        /// <summary>
        /// Absolute authored project root whose scripts are compiled.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Display name of the project, used to name the generated solution.
        /// </summary>
        readonly string ProjectName;

        /// <summary>
        /// Tool that runs <c>dotnet build</c> and publishes outputs into the Play output root.
        /// </summary>
        readonly EditorDotNetScriptBuildTool BuildTool;

        /// <summary>
        /// Initializes one Play script builder for a project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root path.</param>
        /// <param name="projectName">Display name of the project.</param>
        public EditorPlayScriptBuildService(string projectRootPath, string projectName) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (string.IsNullOrWhiteSpace(projectName)) {
                throw new ArgumentException("Project name must be provided.", nameof(projectName));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectName = projectName;
            BuildTool = new EditorDotNetScriptBuildTool();
        }

        /// <summary>
        /// Resolves the Play compiler-output root for one platform: <c>&lt;project&gt;/cache/generated_code/play/&lt;platformId&gt;</c>.
        /// </summary>
        /// <param name="platformId">Stable platform identifier selected for Play.</param>
        /// <returns>Absolute Play output root for the platform.</returns>
        public string ResolvePlayOutputRootPath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(EditorGeneratedCodePaths.ResolveWorkspaceRootPath(ProjectRootPath), PlayFolderName, platformId);
        }

        /// <summary>
        /// Resolves the runtime module root the player loads from: <c>&lt;play output root&gt;/generated_code/bin</c>,
        /// laid out as one <c>&lt;ModuleId&gt;</c> folder per runtime module.
        /// </summary>
        /// <param name="platformId">Stable platform identifier selected for Play.</param>
        /// <returns>Absolute runtime module root for the platform.</returns>
        public string ResolveRuntimeModulesRootPath(string platformId) {
            return Path.Combine(ResolvePlayOutputRootPath(platformId), EditorGeneratedCodePaths.GeneratedCodeFolderName, "bin");
        }

        /// <summary>
        /// Regenerates the Play solution and compiles the project's runtime modules with the platform's gameplay symbols.
        /// </summary>
        /// <param name="platformId">Stable platform identifier whose gameplay symbols are defined.</param>
        /// <param name="environmentId">Selected project environment id, or empty when no environment is selected.</param>
        /// <param name="runtimeModulesRootPath">Directory holding one <c>&lt;ModuleId&gt;</c> output folder per runtime module.</param>
        /// <returns>Structured result describing the build outcome.</returns>
        public EditorBuildExecutionResult Build(string platformId, string environmentId, out string runtimeModulesRootPath) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (environmentId == null) {
                throw new ArgumentNullException(nameof(environmentId));
            }

            EditorBuildIsolationPathResolver isolationPathResolver = new EditorBuildIsolationPathResolver(ProjectRootPath);
            string routeId = PlayRouteIdPrefix + platformId;
            // The solution and project files live in their own route workspace so Play never rewrites the editor's
            // hot-reload projects, and outside the output root so the generated metadata and compiler outputs stay apart.
            EditorGameSolutionService solutionService = new EditorGameSolutionService(
                ProjectRootPath,
                ProjectName,
                new EditorVisualStudioLauncher(),
                ResolvePlayOutputRootPath(platformId),
                isolationPathResolver.ResolveGeneratedCodeProjectWorkspaceRootPath(routeId, EditorScriptCompilationMode.RuntimeOnly),
                EditorScriptCompilationMode.RuntimeOnly,
                isolationPathResolver.ResolveGeneratedCodeProjectOutputRootPath(routeId, EditorScriptCompilationMode.RuntimeOnly),
                EditorGeneratedCodeOutputLayout.ModuleFolder);
            string playOutputRootPath = solutionService.GeneratedExecutionOutputRootPath;
            runtimeModulesRootPath = ResolveRuntimeModulesRootPath(platformId);
            if (!solutionService.HasCodeModules) {
                return EditorBuildExecutionResult.Success("Play script build skipped: the project declares no code modules.");
            }

            RequirePlayerModuleLayout(solutionService.GeneratedModuleProjects, runtimeModulesRootPath);
            IReadOnlyList<string> gameplaySymbols = EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId, environmentId);
            using EditorGeneratedCodeWorkspaceLease workspaceLease = solutionService.AcquireWorkspaceLease();
            solutionService.GenerateSolutionFiles(workspaceLease);
            // Play builds in place into its stable per-platform output root, so MSBuild reuses obj/ and skips up-to-date
            // module projects; hot-reload and platform builds keep their isolated temporary build roots.
            IReadOnlyList<EditorGeneratedCodeModuleProject> moduleProjects = solutionService.GeneratedModuleProjects;
            string[] projectFilePaths = new string[moduleProjects.Count];
            for (int index = 0; index < moduleProjects.Count; index++) {
                projectFilePaths[index] = moduleProjects[index].ProjectFilePath;
            }

            return BuildTool.BuildIncrementally(
                solutionService.GeneratedProductionSolutionFilterFilePath,
                playOutputRootPath,
                workspaceLease,
                gameplaySymbols,
                projectFilePaths,
                solutionService.DescribeBuildInputs().GeneratedFilePaths);
        }

        /// <summary>
        /// Verifies, before any project file is written, that every generated module project reports its assembly
        /// directory as the <see cref="EditorPlayModuleLayout"/> module folder the player loads from. The generated
        /// projects' reported output directories describe where assemblies really land, so a layout drift fails here
        /// instead of as a missing assembly in the player.
        /// </summary>
        /// <param name="moduleProjects">Generated runtime module projects of the Play solution.</param>
        /// <param name="runtimeModulesRootPath">Runtime module root handed to the player.</param>
        /// <exception cref="InvalidOperationException">Thrown when a module would publish anywhere else.</exception>
        static void RequirePlayerModuleLayout(IReadOnlyList<EditorGeneratedCodeModuleProject> moduleProjects, string runtimeModulesRootPath) {
            string fullModulesRootPath = Path.GetFullPath(runtimeModulesRootPath);
            for (int index = 0; index < moduleProjects.Count; index++) {
                EditorGeneratedCodeModuleProject moduleProject = moduleProjects[index];
                string expectedDirectoryPath = EditorPlayModuleLayout.ResolveModuleDirectoryPath(fullModulesRootPath, moduleProject.ModuleId);
                if (!string.Equals(Path.GetFullPath(moduleProject.OutputDirectoryPath), expectedDirectoryPath, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException(
                        $"Play module '{moduleProject.ModuleId}' would publish to '{moduleProject.OutputDirectoryPath}', but the player loads it from '{expectedDirectoryPath}'.");
                }
            }
        }
    }
}
