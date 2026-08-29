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
            using EditorSessionInteractionServices interactionServices = new EditorSessionInteractionServices();
            using EditorCoreInteractionGraphBinding interactionGraphBinding = new EditorCoreInteractionGraphBinding(core, interactionServices);
            core.SetDefaultFontAssetForEditor(DefaultFontAsset);
            ShaderBackendRegistry shaderBackendRegistry = CreateShaderBackendRegistry(bootstrap.PlatformCatalogService, options.PlatformId);
            using EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary = new EditorBuiltInShaderAssetLibrary(shaderBackendRegistry);
            using EngineGeneratedModelCache generatedModelCache = new EngineGeneratedModelCache(core);
            using EngineGeneratedMaterialCache generatedMaterialCache = new EngineGeneratedMaterialCache(core, builtInShaderAssetLibrary);
            using EditorSessionRendererResources rendererResources = new EditorSessionRendererResources(core.RenderManager3D, core.RenderManager2D, core.ObjectManager, core.EntityFactory, core.SceneEntityIdAllocator, core.Input, () => core.FrameDeltaSeconds, DefaultFontAsset, interactionServices);
            // Renderer binding is supplied to the authoring factory before recovery restores cached assets.
            // The registry is declared after its borrowed caches so reverse
            // using-declaration disposal retires the provider graph first.
            using GeneratedAssetProviderRegistry generatedAssetProviderRegistry = new GeneratedAssetProviderRegistry();
            // Recover and initialize the project authoring graph before build
            // configuration, importer, shader, or watcher work can observe it.
            using IEditorProjectAuthoringSession authoringSession =
                new EditorProjectAssetAuthoringServiceFactory(Importers).CreateSession(
                    bootstrap.ProjectRootPath,
                    generatedAssetProviderRegistry,
                    generatedModelCache,
                    generatedMaterialCache,
                    rendererResources);
            generatedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider(generatedModelCache, generatedMaterialCache));
            EditorBuildExecutionResult prebuildResult = ExecuteEditorPrebuildCommands(bootstrap, options, authoringSession, shaderBackendRegistry, core, interactionServices, rendererResources, generatedAssetProviderRegistry);
            if (!prebuildResult.Succeeded) {
                return prebuildResult;
            }
            ShaderCompileTarget runtimeTarget = ResolveShaderCompileTarget(options.PlatformId);
            ShaderTargetBuildOptions targetOptions = new ShaderTargetBuildOptions(runtimeTarget, new ShaderModel(4, 0));
            ShaderPackageBuildOptions shaderPackageBuildOptions = new ShaderPackageBuildOptions(
                new[] { targetOptions },
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                Array.Empty<ShaderDefine>());
            using ShaderModuleManager shaderModuleManager = new ShaderModuleManager(new ShaderModuleManagerOptions(
                Path.Combine(bootstrap.ProjectRootPath, "assets"),
                Path.Combine(bootstrap.ProjectRootPath, "cache", "shader-cache"),
                shaderPackageBuildOptions,
                runtimeTarget,
                shaderBackendRegistry,
                250));
            shaderModuleManager.Start();

            Console.WriteLine($"[build] building project scripts for '{options.PlatformId}'");
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

                Console.WriteLine($"[build] project scripts loaded for '{options.PlatformId}'");

                EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting();
                if (buildConfig == null) {
                    return EditorBuildExecutionResult.Failure($"No existing build settings were found for project '{bootstrap.ProjectDisplayName}'. Open the editor and configure a build first.");
                }

                EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(buildConfig, options.PlatformId);
                if (platformConfig == null) {
                    return EditorBuildExecutionResult.Failure($"No build settings exist for platform '{options.PlatformId}'.");
                }

                EditorProfileSettingsDocument sharedProfileSettings = bootstrap.ProfileSettingsService.TryLoadExisting();
                EditorPlatformProfileSettingsDocument sharedPlatformSettings = EditorCliBuildPlatformConfigOverlayService.FindPlatformSettings(
                    sharedProfileSettings,
                    options.PlatformId);
                if (sharedPlatformSettings != null) {
                    EditorCliBuildPlatformConfigOverlayService.ApplySharedProfileSettings(platformConfig, sharedPlatformSettings);
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
                if (options.EnvironmentWasExplicitlyProvided) {
                    EditorProjectEnvironmentsDocument environments = new EditorProjectEnvironmentsService(bootstrap.ProjectRootPath).Load();
                    if (!environments.Environments.Any(environment => string.Equals(environment.Id, options.EnvironmentId, StringComparison.OrdinalIgnoreCase))) {
                        string availableEnvironmentIds = string.Join(", ", environments.Environments.Select(environment => environment.Id));
                        return EditorBuildExecutionResult.Failure($"Unknown build environment '{options.EnvironmentId}'. Available environments: {availableEnvironmentIds}.");
                    }

                    platformConfig.SelectedEnvironmentId = options.EnvironmentId;
                } else {
                    platformConfig.SelectedEnvironmentId = string.Empty;
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
                    assemblyHost.ScriptTypeResolver,
                    builtInShaderAssetLibrary);

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
            string buildExecutionId = Guid.NewGuid().ToString("N");
            EditorGameSolutionService solutionService = new EditorGameSolutionService(
                bootstrap.ProjectRootPath,
                bootstrap.ProjectName,
                new EditorVisualStudioLauncher(),
                isolationPathResolver.ResolveGeneratedCodeOutputRootPath(platformId, buildExecutionId),
                isolationPathResolver.ResolveGeneratedCodeWorkspaceRootPath(platformId, buildExecutionId),
                ResolveProjectScriptCompilationMode());
            EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();
            assemblyHost = new EditorGameScriptAssemblyHost(bootstrap.ProjectRootPath);
            hotReloadService = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);
            return hotReloadService.BuildAndReload();
        }

        /// <summary>
        /// Executes the selected profile's ordered editor authoring commands before the runtime-only platform cook begins.
        /// </summary>
        /// <param name="bootstrap">Bootstrap context for the active project.</param>
        /// <param name="options">Parsed native platform build request.</param>
        /// <returns>Success when all declared prebuild commands complete; otherwise the first command failure.</returns>
        internal EditorBuildExecutionResult ExecuteEditorPrebuildCommands(
            EditorProjectBootstrapContext bootstrap,
            EditorCliBuildOptions options,
            IEditorProjectAuthoringSession authoringSession,
            ShaderBackendRegistry shaderBackendRegistry,
            Core core,
            EditorSessionInteractionServices interactionServices,
            EditorSessionRendererResources rendererResources,
            GeneratedAssetProviderRegistry generatedAssetProviders) {
            if (bootstrap == null) {
                throw new ArgumentNullException(nameof(bootstrap));
            }
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }
            if (authoringSession == null) {
                throw new ArgumentNullException(nameof(authoringSession));
            }
            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            }
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }
            if (interactionServices == null) {
                throw new ArgumentNullException(nameof(interactionServices));
            }
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }
            if (generatedAssetProviders == null) {
                throw new ArgumentNullException(nameof(generatedAssetProviders));
            }

            EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting();
            if (buildConfig == null) {
                return EditorBuildExecutionResult.Failure($"No existing build settings were found for project '{bootstrap.ProjectDisplayName}'. Open the editor and configure a build first.");
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(buildConfig, options.PlatformId);
            if (platformConfig == null) {
                return EditorBuildExecutionResult.Failure($"No build settings exist for platform '{options.PlatformId}'.");
            }

            string buildProfileId = string.IsNullOrWhiteSpace(options.BuildProfileId)
                ? platformConfig.SelectedBuildProfileId
                : options.BuildProfileId;
            IReadOnlyList<string> commandIds;
            try {
                commandIds = new EditorBuildPrebuildCommandResolver().Resolve(platformConfig, buildProfileId);
            } catch (Exception exception) {
                return EditorBuildExecutionResult.Failure($"Build profile '{buildProfileId}' could not resolve editor prebuild commands: {exception.Message}");
            }

            for (int index = 0; index < commandIds.Count; index++) {
                string commandId = commandIds[index];
                Console.WriteLine($"[build] executing editor prebuild command '{commandId}' for profile '{buildProfileId}'");
                EditorBuildExecutionResult commandResult = new EditorCliCommandRunner(
                    DefaultFontAsset,
                    new EditorProjectAssetAuthoringServiceFactory(Importers)).RunInSessionGraph(
                        bootstrap,
                        new EditorCliCommandOptions(bootstrap.ProjectRootPath, commandId),
                        authoringSession,
                        shaderBackendRegistry,
                        core,
                        interactionServices,
                        rendererResources,
                        generatedAssetProviders);
                if (!commandResult.Succeeded) {
                    return EditorBuildExecutionResult.Failure($"Editor prebuild command '{commandId}' for profile '{buildProfileId}' failed: {commandResult.Message}");
                }
            }

            return EditorBuildExecutionResult.Success($"Editor prebuild completed for profile '{buildProfileId}'.");
        }

        /// <summary>
        /// Executes editor prebuild commands against an already loaded command
        /// catalog and the caller's explicit invocation graph. This is the
        /// nested-command composition path; it cannot construct or replace an
        /// inner core.
        /// </summary>
        internal EditorBuildExecutionResult ExecuteEditorPrebuildCommands(
            EditorProjectBootstrapContext bootstrap,
            EditorCliBuildOptions options,
            IEditorProjectAuthoringSession authoringSession,
            ShaderBackendRegistry shaderBackendRegistry,
            Core core,
            EditorSessionInteractionServices interactionServices,
            EditorSessionRendererResources rendererResources,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            IEditorProjectCommandCatalogProvider commandCatalogProvider,
            IScriptTypeResolver scriptTypeResolver) {
            if (bootstrap == null) {
                throw new ArgumentNullException(nameof(bootstrap));
            }
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }
            if (authoringSession == null) {
                throw new ArgumentNullException(nameof(authoringSession));
            }
            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            }
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }
            if (interactionServices == null) {
                throw new ArgumentNullException(nameof(interactionServices));
            }
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }
            if (generatedAssetProviders == null) {
                throw new ArgumentNullException(nameof(generatedAssetProviders));
            }
            if (commandCatalogProvider == null) {
                throw new ArgumentNullException(nameof(commandCatalogProvider));
            }
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }
            if (!ReferenceEquals(rendererResources.OwningCore, core)
                || !ReferenceEquals(rendererResources.InteractionServices, interactionServices)) {
                throw new InvalidOperationException("Nested editor prebuild commands must use the outer invocation renderer and interaction graph.");
            }

            EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting();
            if (buildConfig == null) {
                return EditorBuildExecutionResult.Failure($"No existing build settings were found for project '{bootstrap.ProjectDisplayName}'. Open the editor and configure a build first.");
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(buildConfig, options.PlatformId);
            if (platformConfig == null) {
                return EditorBuildExecutionResult.Failure($"No build settings exist for platform '{options.PlatformId}'.");
            }

            string buildProfileId = string.IsNullOrWhiteSpace(options.BuildProfileId)
                ? platformConfig.SelectedBuildProfileId
                : options.BuildProfileId;
            IReadOnlyList<string> commandIds;
            try {
                commandIds = new EditorBuildPrebuildCommandResolver().Resolve(platformConfig, buildProfileId);
            } catch (Exception exception) {
                return EditorBuildExecutionResult.Failure($"Build profile '{buildProfileId}' could not resolve editor prebuild commands: {exception.Message}");
            }

            EditorCliCommandRunner commandRunner = new EditorCliCommandRunner(
                DefaultFontAsset,
                new EditorProjectAssetAuthoringServiceFactory(Importers));
            for (int index = 0; index < commandIds.Count; index++) {
                string commandId = commandIds[index];
                EditorBuildExecutionResult commandResult = commandRunner.RunInSessionGraph(
                    bootstrap,
                    new EditorCliCommandOptions(bootstrap.ProjectRootPath, commandId),
                    authoringSession,
                    shaderBackendRegistry,
                    core,
                    interactionServices,
                    rendererResources,
                    generatedAssetProviders,
                    commandCatalogProvider,
                    scriptTypeResolver);
                if (!commandResult.Succeeded) {
                    return EditorBuildExecutionResult.Failure($"Editor prebuild command '{commandId}' for profile '{buildProfileId}' failed: {commandResult.Message}");
                }
            }

            return EditorBuildExecutionResult.Success($"Editor prebuild completed for profile '{buildProfileId}'.");
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

        /// <summary>
        /// Resolves the shader compiler target required by one editor build platform.
        /// </summary>
        /// <param name="platformId">Stable platform identifier supplied by the editor build request.</param>
        /// <returns>PS Vita for a PS Vita build; otherwise the existing DirectX 11 target.</returns>
        internal static ShaderCompileTarget ResolveShaderCompileTarget(string platformId) {
            return string.Equals(platformId, "psvita", StringComparison.OrdinalIgnoreCase)
                ? ShaderCompileTarget.PsVita
                : ShaderCompileTarget.DirectX11;
        }

        /// <summary>
        /// Resolves the script-compilation mode required by a native platform cook.
        /// </summary>
        /// <returns>Runtime-only compilation because platform cooks must not require editor tools or tests.</returns>
        internal static EditorScriptCompilationMode ResolveProjectScriptCompilationMode() {
            return EditorScriptCompilationMode.RuntimeOnly;
        }
    }
}
