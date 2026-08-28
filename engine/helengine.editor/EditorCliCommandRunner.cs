using helengine.baseplatform.Definitions;
using helengine.baseplatform.Requests;
using helengine.directx11;
using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Builds project scripts, loads editor modules, and executes one project-authored editor command in headless mode.
    /// </summary>
    public sealed class EditorCliCommandRunner {
        /// <summary>
        /// Font asset used to satisfy editor UI and scene generation dependencies during headless command execution.
        /// </summary>
        readonly FontAsset DefaultFontAsset;

        /// <summary>
        /// Factory that creates the host-configured project asset-authoring capability for each command run.
        /// </summary>
        readonly IEditorProjectAuthoringSessionFactory AuthoringSessionFactory;

        /// <summary>
        /// Initializes a headless editor command runner with the default font asset required by editor systems.
        /// </summary>
        /// <param name="defaultFontAsset">Font asset used by editor systems during command execution.</param>
        /// <param name="authoringSessionFactory">Factory backed by importer registrations supplied by the editor host.</param>
        public EditorCliCommandRunner(
            FontAsset defaultFontAsset,
            IEditorProjectAuthoringSessionFactory authoringSessionFactory) {
            DefaultFontAsset = defaultFontAsset ?? throw new ArgumentNullException(nameof(defaultFontAsset));
            AuthoringSessionFactory = authoringSessionFactory ?? throw new ArgumentNullException(nameof(authoringSessionFactory));
        }

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
            using DirectX11Renderer3D renderer3D = new DirectX11Renderer3D();
            using EditorCore core = new EditorCore(null);
            CoreInitializationOptions initializationOptions = new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(Path.Combine(bootstrap.ProjectRootPath, "assets"))
            };
            PlatformInfo platformInfo = new PlatformInfo("editor", bootstrap.RequiredEngineVersion);
            core.Initialize(renderer3D, renderer3D.Render2D, null, platformInfo, initializationOptions);
            core.SetDefaultFontAssetForEditor(DefaultFontAsset);
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            ShaderBackendRegistry shaderBackendRegistry = CreateShaderBackendRegistry(bootstrap.PlatformCatalogService);
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
            ShaderCompileTarget runtimeTarget = ShaderCompileTarget.DirectX11;
            ShaderTargetBuildOptions targetOptions = new ShaderTargetBuildOptions(runtimeTarget, new ShaderModel(4, 0));
            ShaderPackageBuildOptions shaderPackageBuildOptions = new ShaderPackageBuildOptions(
                new[] { targetOptions },
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                Array.Empty<ShaderDefine>());
            ShaderModuleManager shaderModuleManager = new ShaderModuleManager(new ShaderModuleManagerOptions(
                Path.Combine(bootstrap.ProjectRootPath, "assets"),
                Path.Combine(bootstrap.ProjectRootPath, "cache", "shader-cache"),
                shaderPackageBuildOptions,
                runtimeTarget,
                shaderBackendRegistry,
                250));
            EditorShaderPackageService.Initialize(shaderModuleManager, runtimeTarget, core.ContentManager);
            shaderModuleManager.Start();

            EditorBuildIsolationPathResolver isolationPathResolver = new EditorBuildIsolationPathResolver(bootstrap.ProjectRootPath);
            string commandExecutionId = Guid.NewGuid().ToString("N");
            string generatedOutputRootPath = isolationPathResolver.ResolveGeneratedCodeOutputRootPath("editor-command", commandExecutionId);
            string generatedWorkspaceRootPath = isolationPathResolver.ResolveGeneratedCodeWorkspaceRootPath("editor-command", commandExecutionId);
            EditorGameSolutionService solutionService = new EditorGameSolutionService(
                bootstrap.ProjectRootPath,
                bootstrap.ProjectName,
                new EditorVisualStudioLauncher(),
                generatedOutputRootPath,
                generatedWorkspaceRootPath,
                EditorScriptCompilationMode.EditorFull);
            EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();
            using EditorGameScriptAssemblyHost assemblyHost = new EditorGameScriptAssemblyHost(bootstrap.ProjectRootPath);
            using EditorGameScriptHotReloadService hotReloadService = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);
            EditorBuildExecutionResult buildResult = hotReloadService.BuildAndReload();
            if (!buildResult.Succeeded) {
                return buildResult;
            }

            using IEditorProjectAuthoringSession authoring = AuthoringSessionFactory.CreateSession(bootstrap.ProjectRootPath);
            EditorCommandContext commandContext = new EditorCommandContext(
                bootstrap.ProjectRootPath,
                assemblyHost.ScriptTypeResolver,
                authoring);
            EditorCommandExecutionService commandExecutionService = new EditorCommandExecutionService(hotReloadService, commandContext);

            try {
                commandExecutionService.Execute(options.CommandId);
                string repairSummary = authoring.RepairReport.CreateSummary();
                if (!string.IsNullOrWhiteSpace(repairSummary)) {
                    Console.WriteLine(repairSummary);
                }
                string completionMessage = AppendRepairSummary(
                    $"Editor command '{options.CommandId}' executed successfully.",
                    authoring.RepairReport);
                return EditorBuildExecutionResult.Success(completionMessage);
            } catch (Exception exception) {
                string repairSummary = authoring.RepairReport.CreateSummary();
                if (!string.IsNullOrWhiteSpace(repairSummary)) {
                    Console.WriteLine(repairSummary);
                }
                string failureMessage = AppendRepairSummary(
                    $"Editor command '{options.CommandId}' failed: {exception}",
                    authoring.RepairReport);
                return EditorBuildExecutionResult.Failure(failureMessage);
            }
        }

        /// <summary>
        /// Appends the current repair summary to one command completion message when repairs occurred.
        /// </summary>
        /// <param name="message">Base command completion message.</param>
        /// <param name="repairReport">Session report to summarize.</param>
        /// <returns>Completion message with an optional concise repair summary.</returns>
        internal static string AppendRepairSummary(string message, EditorAssetRepairReport repairReport) {
            if (message == null) {
                throw new ArgumentNullException(nameof(message));
            }
            if (repairReport == null) {
                throw new ArgumentNullException(nameof(repairReport));
            }

            string summary = repairReport.CreateSummary();
            return string.IsNullOrWhiteSpace(summary) ? message : $"{message} {summary}";
        }

        /// <summary>
        /// Creates the shader backend registry required by the headless editor command runner.
        /// </summary>
        /// <param name="platformCatalogService">Dynamic platform catalog that can contribute additional shader backends from loaded platform builders.</param>
        /// <returns>Registry populated with the desktop shader backends supported by the command runner.</returns>
        static ShaderBackendRegistry CreateShaderBackendRegistry(EditorPlatformCatalogService platformCatalogService) {
            if (platformCatalogService == null) {
                throw new ArgumentNullException(nameof(platformCatalogService));
            }

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            platformCatalogService.RegisterShaderBackends(shaderBackendRegistry);
            return shaderBackendRegistry;
        }
    }
}
