using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.directx11;
using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Runs one headless editor build using the persisted editor settings for a project.
    /// </summary>
    public sealed class EditorCliBuildRunner {
        /// <summary>
        /// Importer registrations used for the headless build.
        /// </summary>
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;

        /// <summary>
        /// Default font asset used to package scenes that reference the editor's built-in font.
        /// </summary>
        readonly FontAsset DefaultFontAsset;

        /// <summary>
        /// Initializes one headless build runner.
        /// </summary>
        /// <param name="importers">Importer registrations used for the headless build.</param>
        /// <param name="defaultFontAsset">Font asset used to satisfy packaged editor-font references.</param>
        public EditorCliBuildRunner(IReadOnlyList<IAssetImporterRegistration> importers, FontAsset defaultFontAsset) {
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            DefaultFontAsset = defaultFontAsset ?? throw new ArgumentNullException(nameof(defaultFontAsset));
        }

        /// <summary>
        /// Executes one build using the persisted editor settings for the supplied project.
        /// </summary>
        /// <param name="options">Parsed headless build request.</param>
        /// <returns>Structured execution result.</returns>
        public EditorBuildExecutionResult Run(EditorCliBuildOptions options) {
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
            EditorProjectPaths.Initialize(bootstrap.ProjectRootPath);
            ShaderBackendRegistry shaderBackendRegistry = CreateShaderBackendRegistry(bootstrap.PlatformCatalogService, options.PlatformId);
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

            EditorBuildExecutionResult scriptLoadResult = BuildAndLoadProjectScripts(
                bootstrap,
                options.PlatformId,
                out EditorGameScriptAssemblyHost assemblyHost,
                out EditorGameScriptHotReloadService hotReloadService);
            using (assemblyHost)
            using (hotReloadService) {
                if (!scriptLoadResult.Succeeded) {
                    return scriptLoadResult;
                }

                EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting();
                if (buildConfig == null) {
                    return EditorBuildExecutionResult.Failure($"No existing build settings were found for project '{bootstrap.ProjectDisplayName}'. Open the editor and configure a build first.");
                }

                EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(buildConfig, options.PlatformId);
                if (platformConfig == null) {
                    return EditorBuildExecutionResult.Failure($"No build settings exist for platform '{options.PlatformId}'.");
                }

                EditorPlatformBuildSelectionModel selectionModel;
                try {
                    selectionModel = bootstrap.ResolveSelectionModel(options.PlatformId);
                } catch (Exception ex) {
                    return EditorBuildExecutionResult.Failure($"Platform '{options.PlatformId}' could not load its builder metadata: {ex}");
                }

                if (!string.IsNullOrWhiteSpace(options.BuildProfileId)) {
                    platformConfig.SelectedBuildProfileId = options.BuildProfileId;
                }

                EditorBuildQueueItemDocument queueItem = EditorBuildQueueItemDocument.Create(
                    bootstrap.SceneCatalogService,
                    platformConfig,
                    selectionModel,
                    options.OutputDirectoryPath);
                AvailablePlatformDescriptor platformDescriptor;
                try {
                    platformDescriptor = bootstrap.ResolvePlatformDescriptor(options.PlatformId);
                } catch (Exception ex) {
                    return EditorBuildExecutionResult.Failure(ex.Message);
                }

                EditorPlatformBuildExecutor executor = new EditorPlatformBuildExecutor(
                    bootstrap.ProjectRootPath,
                    bootstrap.RequiredEngineVersion,
                    bootstrap.ProjectName,
                    bootstrap.ProjectVersion,
                    Importers,
                    platformDescriptor,
                    DefaultFontAsset,
                    null,
                    assemblyHost.ScriptTypeResolver);

                EditorBuildExecutionResult result = executor.Execute(queueItem);
                if (result.Succeeded && options.UseCommonOutputDirectory) {
                    return EditorBuildExecutionResult.Success($"{result.Message} Full graph common-output mode was requested.");
                }

                return result;
            }
        }

        /// <summary>
        /// Generates, builds, and loads the current project's script libraries for headless build execution.
        /// </summary>
        /// <param name="bootstrap">Bootstrap context for the active project.</param>
        /// <param name="assemblyHost">Loaded script assembly host when initialization succeeds.</param>
        /// <param name="hotReloadService">Hot-reload service that owns the loaded project libraries.</param>
        /// <returns>Structured result describing whether project libraries loaded successfully.</returns>
        EditorBuildExecutionResult BuildAndLoadProjectScripts(
            EditorProjectBootstrapContext bootstrap,
            string platformId,
            out EditorGameScriptAssemblyHost assemblyHost,
            out EditorGameScriptHotReloadService hotReloadService) {
            if (bootstrap == null) {
                throw new ArgumentNullException(nameof(bootstrap));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorBuildIsolationPathResolver isolationPathResolver = new EditorBuildIsolationPathResolver(bootstrap.ProjectRootPath);
            EditorGameSolutionService solutionService = new EditorGameSolutionService(
                bootstrap.ProjectRootPath,
                bootstrap.ProjectName,
                new EditorVisualStudioLauncher(),
                isolationPathResolver.ResolveGeneratedCodeOutputRootPath(platformId));
            EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();
            assemblyHost = new EditorGameScriptAssemblyHost(bootstrap.ProjectRootPath);
            hotReloadService = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);
            return hotReloadService.BuildAndReload();
        }

        /// <summary>
        /// Finds one persisted platform configuration entry for the requested platform id.
        /// </summary>
        /// <param name="buildConfig">Loaded build configuration document.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <returns>Matching platform configuration when present; otherwise null.</returns>
        static EditorBuildPlatformConfigDocument FindPlatformConfig(EditorBuildConfigDocument buildConfig, string platformId) {
            if (buildConfig == null) {
                throw new ArgumentNullException(nameof(buildConfig));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                return null;
            }

            for (int index = 0; index < buildConfig.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platformConfig = buildConfig.Platforms[index];
                if (platformConfig != null && string.Equals(platformConfig.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platformConfig;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates the shader backend registry required by the headless editor build runner.
        /// </summary>
        /// <param name="platformCatalogService">Dynamic platform catalog that can contribute additional shader backends from loaded platform builders.</param>
        /// <param name="platformId">Stable target platform identifier for the active headless build.</param>
        /// <returns>Registry populated with the desktop shader backends supported by the build runner.</returns>
        static ShaderBackendRegistry CreateShaderBackendRegistry(EditorPlatformCatalogService platformCatalogService, string platformId) {
            if (platformCatalogService == null) {
                throw new ArgumentNullException(nameof(platformCatalogService));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            platformCatalogService.RegisterShaderBackends(shaderBackendRegistry, platformId);
            return shaderBackendRegistry;
        }
    }
}
