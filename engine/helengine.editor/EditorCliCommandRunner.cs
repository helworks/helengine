namespace helengine.editor {
    /// <summary>
    /// Builds project scripts, loads editor modules, and executes one project-authored editor command in headless mode.
    /// </summary>
    public sealed class EditorCliCommandRunner {
        /// <summary>
        /// Executes one headless editor-command invocation for the supplied project.
        /// </summary>
        /// <param name="options">Parsed headless editor-command request.</param>
        /// <returns>Structured execution result.</returns>
        public EditorBuildExecutionResult Run(EditorCliCommandOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(options.ProjectPath);
            EditorGameSolutionService solutionService = new EditorGameSolutionService(
                bootstrap.ProjectRootPath,
                bootstrap.ProjectName,
                new EditorVisualStudioLauncher());
            EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();
            using EditorGameScriptAssemblyHost assemblyHost = new EditorGameScriptAssemblyHost(bootstrap.ProjectRootPath);
            using EditorGameScriptHotReloadService hotReloadService = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);

            EditorBuildExecutionResult buildResult = hotReloadService.BuildAndReload();
            if (!buildResult.Succeeded) {
                return buildResult;
            }

            EditorMenuSceneRegenerationService menuSceneRegenerationService = new EditorMenuSceneRegenerationService(
                bootstrap.ProjectRootPath,
                assemblyHost.ScriptTypeResolver);
            EditorCommandContext commandContext = new EditorCommandContext(
                bootstrap.ProjectRootPath,
                assemblyHost.ScriptTypeResolver,
                menuSceneRegenerationService);
            EditorCommandExecutionService commandExecutionService = new EditorCommandExecutionService(hotReloadService, commandContext);

            try {
                commandExecutionService.Execute(options.CommandId);
                return EditorBuildExecutionResult.Success($"Editor command '{options.CommandId}' executed successfully.");
            } catch (Exception exception) {
                return EditorBuildExecutionResult.Failure($"Editor command '{options.CommandId}' failed: {exception.Message}");
            }
        }
    }
}
