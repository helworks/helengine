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
        /// Initializes a headless editor command runner with the default font asset required by editor systems.
        /// </summary>
        /// <param name="defaultFontAsset">Font asset used by editor systems during command execution.</param>
        public EditorCliCommandRunner(FontAsset defaultFontAsset) {
            DefaultFontAsset = defaultFontAsset ?? throw new ArgumentNullException(nameof(defaultFontAsset));
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
                ContentRootPath = Path.Combine(bootstrap.ProjectRootPath, "assets")
            };
            PlatformInfo platformInfo = new PlatformInfo("editor", bootstrap.RequiredEngineVersion);
            core.Initialize(renderer3D, renderer3D.Render2D, null, platformInfo, initializationOptions);
            core.SetDefaultFontAssetForEditor(DefaultFontAsset);
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            EditorProjectPaths.Initialize(bootstrap.ProjectRootPath);
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

            EditorCommandContext commandContext = new EditorCommandContext(
                bootstrap.ProjectRootPath,
                assemblyHost.ScriptTypeResolver);
            EditorCommandExecutionService commandExecutionService = new EditorCommandExecutionService(hotReloadService, commandContext);

            try {
                commandExecutionService.Execute(options.CommandId);
                return EditorBuildExecutionResult.Success($"Editor command '{options.CommandId}' executed successfully.");
            } catch (Exception exception) {
                return EditorBuildExecutionResult.Failure($"Editor command '{options.CommandId}' failed: {exception}");
            }
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
