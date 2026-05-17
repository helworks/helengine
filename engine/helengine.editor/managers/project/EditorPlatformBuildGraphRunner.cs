using helengine;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Targets;
using helengine.files;
using helengine.platforms;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Executes the shared editor-owned build graph for one queued platform build item.
    /// </summary>
    public class EditorPlatformBuildGraphRunner {
        /// <summary>
        /// Environment variable used by the PS2 builder to resolve its native repository root when loaded from the editor app.
        /// </summary>
        const string Ps2RepositoryRootEnvironmentVariableName = "HELENGINE_PS2_REPOSITORY_ROOT";
        /// <summary>
        /// Environment variable used by the Nintendo DS builder to resolve its native repository root when loaded from the editor app.
        /// </summary>
        const string DsRepositoryRootEnvironmentVariableName = "HELENGINE_DS_REPOSITORY_ROOT";
        /// <summary>
        /// Environment variable used by the GameCube builder to resolve its native repository root when loaded from the editor app.
        /// </summary>
        const string GameCubeRepositoryRootEnvironmentVariableName = "HELENGINE_GAMECUBE_REPOSITORY_ROOT";

        readonly string ProjectRootPath;
        readonly string RequiredEngineVersion;
        readonly string ProjectId;
        readonly string ProjectVersion;
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;
        readonly AvailablePlatformDescriptor PlatformDescriptor;
        readonly FontAsset DefaultFontAsset;
        readonly EditorPlatformAssetBuilderLoader BuilderLoader;
        readonly EditorGeneratedCoreRegenerationService GeneratedCoreRegenerationService;
        readonly EditorGeneratedMenuScenePreparationService GeneratedMenuScenePreparationService;
        readonly EditorPhysics3DCodegenFeatureSymbolService Physics3DCodegenFeatureSymbolService;
        readonly EditorPlatformBuildGraphWorkspaceFactory WorkspaceFactory;
        readonly EditorPlatformAssetCookService AssetCookService;
        readonly EditorCodeModuleManifestService CodeModuleManifestService;
        readonly EditorPlatformCodeCookService CodeCookService;
        readonly EditorPlatformLayoutPlanService LayoutPlanService;
        readonly EditorPlatformContainerWriter ContainerWriter;
        readonly EditorPlatformArtifactVariantResolver ArtifactVariantResolver;
        readonly IScriptTypeResolver ScriptTypeResolver;

        /// <summary>
        /// Initializes one build-graph runner for the supplied project and platform descriptor.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative source project root path.</param>
        /// <param name="requiredEngineVersion">Exact engine version required by the current project build.</param>
        /// <param name="projectId">Stable project identifier reported to builders.</param>
        /// <param name="projectVersion">Human-visible project version reported to builders.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="platformDescriptor">Installed platform descriptor selected for execution.</param>
        /// <param name="defaultFontAsset">Default font asset packaged for player builds.</param>
        /// <param name="builderLoader">Builder loader used to hydrate platform asset builders.</param>
        /// <param name="generatedCoreRegenerationService">Generated-core regeneration service used during codegen.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        public EditorPlatformBuildGraphRunner(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            AvailablePlatformDescriptor platformDescriptor,
            FontAsset defaultFontAsset,
            EditorPlatformAssetBuilderLoader builderLoader,
            EditorGeneratedCoreRegenerationService generatedCoreRegenerationService,
            IScriptTypeResolver scriptTypeResolver = null)
            : this(
                projectRootPath,
                requiredEngineVersion,
                projectId,
                projectVersion,
                importers,
                platformDescriptor,
                defaultFontAsset,
                builderLoader,
                generatedCoreRegenerationService,
                null,
                scriptTypeResolver) {
        }

        internal EditorPlatformBuildGraphRunner(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            AvailablePlatformDescriptor platformDescriptor,
            FontAsset defaultFontAsset,
            EditorPlatformAssetBuilderLoader builderLoader,
            EditorGeneratedCoreRegenerationService generatedCoreRegenerationService,
            EditorPlatformBuildGraphWorkspaceFactory workspaceFactory,
            IScriptTypeResolver scriptTypeResolver = null) {
            ProjectRootPath = projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath));
            RequiredEngineVersion = requiredEngineVersion ?? throw new ArgumentNullException(nameof(requiredEngineVersion));
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            ProjectVersion = projectVersion ?? throw new ArgumentNullException(nameof(projectVersion));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            PlatformDescriptor = platformDescriptor ?? throw new ArgumentNullException(nameof(platformDescriptor));
            DefaultFontAsset = defaultFontAsset;
            BuilderLoader = builderLoader ?? throw new ArgumentNullException(nameof(builderLoader));
            GeneratedCoreRegenerationService = generatedCoreRegenerationService ?? throw new ArgumentNullException(nameof(generatedCoreRegenerationService));
            GeneratedMenuScenePreparationService = new EditorGeneratedMenuScenePreparationService(ProjectRootPath, scriptTypeResolver);
            Physics3DCodegenFeatureSymbolService = new EditorPhysics3DCodegenFeatureSymbolService(ProjectRootPath);
            WorkspaceFactory = workspaceFactory ?? new EditorPlatformBuildGraphWorkspaceFactory();
            AssetCookService = new EditorPlatformAssetCookService(
                ProjectRootPath,
                RequiredEngineVersion,
                ProjectId,
                ProjectVersion,
                Importers,
                DefaultFontAsset,
                scriptTypeResolver);
            CodeModuleManifestService = new EditorCodeModuleManifestService(ProjectRootPath);
            CodeCookService = new EditorPlatformCodeCookService(ProjectRootPath);
            LayoutPlanService = new EditorPlatformLayoutPlanService();
            ContainerWriter = new EditorPlatformContainerWriter();
            ArtifactVariantResolver = new EditorPlatformArtifactVariantResolver();
            ScriptTypeResolver = scriptTypeResolver;
        }

        /// <summary>
        /// Executes the shared build graph for one queue item.
        /// </summary>
        public virtual EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            IPlatformAssetBuilder builder = BuilderLoader.Load(PlatformDescriptor.BuilderAssemblyPath);
            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(builder.Definition);
            string selectedBuildProfileId = ResolveSelectedBuildProfileId(queueItem, selectionModel);
            string selectedGraphicsProfileId = ResolveSelectedGraphicsProfileId(queueItem, selectedBuildProfileId, selectionModel);
            string selectedCodegenProfileId = ResolveSelectedCodegenProfileId(queueItem, selectedBuildProfileId, selectionModel);
            string selectedStorageProfileId = ResolveSelectedStorageProfileId(queueItem, selectionModel);
            string selectedMediaProfileId = ResolveSelectedMediaProfileId(queueItem, selectionModel);
            PlatformStorageProfileDefinition selectedStorageProfile = selectionModel.ResolveStorageProfile(selectedStorageProfileId);
            PlatformMediaProfileDefinition selectedMediaProfile = selectionModel.ResolveMediaProfile(selectedMediaProfileId);
            PlatformCodegenProfileDefinition selectedCodegenProfile = selectionModel.ResolveCodegenProfile(selectedCodegenProfileId);
            EditorPlatformBuildGraphWorkspace workspace = WorkspaceFactory.Create(PlatformDescriptor.Id, queueItem.QueueItemId);

            ResetExecutionDirectories(workspace.ExecutionRootPath, workspace.CookRootPath, workspace.PackageRootPath, workspace.BuilderWorkingRootPath, queueItem.OutputDirectoryPath);
            Directory.CreateDirectory(workspace.GeneratedCoreRootPath);
            Directory.CreateDirectory(workspace.CodeRootPath);
            Directory.CreateDirectory(workspace.VariantRootPath);
            Directory.CreateDirectory(workspace.LayoutRootPath);
            Directory.CreateDirectory(workspace.BuilderWorkingRootPath);
            Directory.CreateDirectory(workspace.LogsRootPath);

            GeneratedMenuScenePreparationService.EnsurePrepared(queueItem.SelectedSceneIds ?? []);
            RunRegenerateCore(builder.Definition, selectedCodegenProfile, queueItem, workspace);
            PlatformBuildManifest cookedManifest = RunCookAssets(
                builder,
                builder.Definition,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                queueItem,
                workspace);
            PlatformBuildCodeModule[] codeModules = RunCompileCode(cookedManifest, selectedCodegenProfile, selectedStorageProfile, queueItem, workspace);
            CopySceneReferencedRuntimeModuleSourcesIntoGeneratedCore(cookedManifest, codeModules, workspace.GeneratedCoreRootPath, workspace.CodeRootPath, workspace.ExecutionRootPath);
            EmitGeneratedRuntimeComponentDeserializersForCookedScenes(cookedManifest, workspace.GeneratedCoreRootPath, workspace.ExecutionRootPath);
            cookedManifest = ReplaceCodeModules(cookedManifest, codeModules);
            cookedManifest = RunResolveVariants(cookedManifest, workspace);
            cookedManifest = RunLayoutMedia(cookedManifest, selectedStorageProfile, selectedMediaProfile, workspace);
            WriteRuntimeNativeManifestSources(cookedManifest, workspace.GeneratedCoreRootPath);
            WriteRuntimeGraphicsRendererManifestSource(workspace.GeneratedCoreRootPath, selectionModel);
            FinalizeGeneratedCoreSources(workspace.GeneratedCoreRootPath);
            RunWriteContainers(cookedManifest, selectedStorageProfile, selectedMediaProfile, workspace);

            EditorBuildExecutionResult packageResult = RunPackagePlatform(
                builder,
                queueItem,
                workspace,
                cookedManifest,
                builder.Definition,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                selectedCodegenProfileId,
                selectedMediaProfileId,
                selectedStorageProfileId);
            return FinalizeBuildExecution(selectionModel, queueItem, packageResult);
        }

        /// <summary>
        /// Finalizes one build-graph execution after the packaged output has been produced.
        /// </summary>
        /// <param name="selectionModel">Resolved builder metadata for the active platform.</param>
        /// <param name="workspace">Workspace that owns the packaged outputs.</param>
        /// <param name="queueItem">Queued build item being executed.</param>
        /// <param name="packageResult">Result returned by the normal package/build phase.</param>
        /// <returns>Final build execution result for the queue item.</returns>
        protected virtual EditorBuildExecutionResult FinalizeBuildExecution(
            EditorPlatformBuildSelectionModel selectionModel,
            EditorBuildQueueItemDocument queueItem,
            EditorBuildExecutionResult packageResult) {
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (packageResult == null) {
                throw new ArgumentNullException(nameof(packageResult));
            }

            if (!packageResult.Succeeded || queueItem.ExecutionMode != EditorBuildExecutionMode.HostDebug) {
                return packageResult;
            }

            return LaunchHostDebugRunner(selectionModel, queueItem, queueItem.OutputDirectoryPath, packageResult);
        }

        /// <summary>
        /// Launches one platform host-debug runner after the normal packaged output has been produced.
        /// </summary>
        /// <param name="selectionModel">Resolved builder metadata for the active platform.</param>
        /// <param name="workspace">Workspace that owns the packaged outputs.</param>
        /// <param name="queueItem">Queued build item being executed.</param>
        /// <param name="packageResult">Successful package/build result that produced the packaged runtime output.</param>
        /// <returns>Final build execution result for the queue item.</returns>
        protected virtual EditorBuildExecutionResult LaunchHostDebugRunner(
            EditorPlatformBuildSelectionModel selectionModel,
            EditorBuildQueueItemDocument queueItem,
            string outputDirectoryPath,
            EditorBuildExecutionResult packageResult) {
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }
            if (packageResult == null) {
                throw new ArgumentNullException(nameof(packageResult));
            }

            PlatformHostDebugCapability capability = selectionModel.HostDebugCapability;
            if (capability == null || !capability.SupportsHostDebug) {
                return EditorBuildExecutionResult.Failure($"Platform '{selectionModel.PlatformId}' does not support host-debug.");
            }
            if (!capability.RequiresPackagedExportArtifacts) {
                return EditorBuildExecutionResult.Failure($"Platform '{selectionModel.PlatformId}' host-debug requires packaged export artifacts.");
            }
            if (capability.RunnerKind != PlatformHostDebugRunnerKind.NativeExecutable) {
                return EditorBuildExecutionResult.Failure($"Platform '{selectionModel.PlatformId}' host-debug runner kind '{capability.RunnerKind}' is not supported.");
            }

            string executablePath = ResolveHostDebugRunnerExecutablePath(capability);
            if (!File.Exists(executablePath)) {
                return EditorBuildExecutionResult.Failure($"Host-debug runner '{executablePath}' was not found.");
            }

            string arguments = BuildHostDebugRunnerArguments(outputDirectoryPath);
            StartHostDebugProcess(executablePath, arguments);
            return EditorBuildExecutionResult.Success(packageResult.Message + " Host-debug runner launched.");
        }

        /// <summary>
        /// Resolves the published host-debug runner executable path for the active platform.
        /// </summary>
        /// <param name="capability">Host-debug capability metadata published by the platform builder.</param>
        /// <returns>Absolute executable path for the published host-debug runner.</returns>
        protected virtual string ResolveHostDebugRunnerExecutablePath(PlatformHostDebugCapability capability) {
            if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            }

            string nativeRepositoryRootPath = PlatformDescriptor.PlayerSourceRootPath;
            if (string.IsNullOrWhiteSpace(nativeRepositoryRootPath)) {
                string builderAssemblyDirectoryPath = Path.GetDirectoryName(PlatformDescriptor.BuilderAssemblyPath)
                    ?? throw new InvalidOperationException("Builder assembly directory could not be resolved.");
                nativeRepositoryRootPath = Path.GetFullPath(Path.Combine(builderAssemblyDirectoryPath, "..", "..", "..", ".."));
            }

            string runnerFileName = capability.RunnerId + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);
            return Path.Combine(
                Path.GetFullPath(nativeRepositoryRootPath),
                "tools",
                capability.RunnerId,
                "bin",
                runnerFileName);
        }

        /// <summary>
        /// Builds the command-line arguments for one host-debug runner invocation.
        /// </summary>
        /// <param name="outputDirectoryPath">Packaged platform output root produced by the normal build phase.</param>
        /// <returns>Command-line arguments passed to the published host-debug runner.</returns>
        protected virtual string BuildHostDebugRunnerArguments(string outputDirectoryPath) {
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }

            return $"--export-root \"{Path.GetFullPath(outputDirectoryPath)}\" --mode load-only";
        }

        /// <summary>
        /// Starts one published host-debug runner process.
        /// </summary>
        /// <param name="executablePath">Absolute runner executable path.</param>
        /// <param name="arguments">Command-line arguments passed to the runner.</param>
        protected virtual void StartHostDebugProcess(string executablePath, string arguments) {
            if (string.IsNullOrWhiteSpace(executablePath)) {
                throw new ArgumentException("Executable path must be provided.", nameof(executablePath));
            }

            string workingDirectoryPath = Path.GetDirectoryName(executablePath)
                ?? throw new InvalidOperationException("Host-debug runner working directory could not be resolved.");
            Process.Start(new ProcessStartInfo(executablePath, arguments) {
                UseShellExecute = false,
                WorkingDirectory = workingDirectoryPath
            });
        }

        /// <summary>
        /// Executes the generated-core regeneration phase.
        /// </summary>
        void RunRegenerateCore(
            PlatformDefinition builderDefinition,
            PlatformCodegenProfileDefinition selectedCodegenProfile,
            EditorBuildQueueItemDocument queueItem,
            EditorPlatformBuildGraphWorkspace workspace) {
            IReadOnlyList<string> physics3DCodegenSymbols = Physics3DCodegenFeatureSymbolService.ResolveSymbols(queueItem.SelectedSceneIds ?? []);
            GeneratedCoreRegenerationService.Regenerate(
                builderDefinition,
                selectedCodegenProfile,
                queueItem.SelectedCodegenOptionValues,
                workspace.GeneratedCoreRootPath,
                PlatformDescriptor.CodegenToolPath,
                physics3DCodegenSymbols,
                CancellationToken.None);
        }

        /// <summary>
        /// Executes the content-cooking phase using the current scene packager.
        /// </summary>
        PlatformBuildManifest RunCookAssets(
            IPlatformAssetBuilder builder,
            PlatformDefinition builderDefinition,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            EditorBuildQueueItemDocument queueItem,
            EditorPlatformBuildGraphWorkspace workspace) {
            return AssetCookService.Cook(
                builderDefinition,
                queueItem.SelectedSceneIds,
                workspace.CookRootPath,
                [PlatformDescriptor.Id],
                builder,
                selectedBuildProfileId,
                selectedGraphicsProfileId);
        }

        /// <summary>
        /// Finalizes the generated native core tree after runtime-specific source writers update it.
        /// </summary>
        /// <param name="generatedCoreRootPath">Generated core source root that will be compiled into the native player.</param>
        void FinalizeGeneratedCoreSources(string generatedCoreRootPath) {
            EditorGeneratedCoreRegenerationService.NormalizeGeneratedNativeSources(generatedCoreRootPath, PlatformDescriptor.Id);
            EditorGeneratedCoreRegenerationService.RewriteAmalgamatedTranslationUnit(generatedCoreRootPath);
        }

        /// <summary>
        /// Executes the authored-code phase.
        /// </summary>
        PlatformBuildCodeModule[] RunCompileCode(
            PlatformBuildManifest cookedManifest,
            PlatformCodegenProfileDefinition selectedCodegenProfile,
            PlatformStorageProfileDefinition selectedStorageProfile,
            EditorBuildQueueItemDocument queueItem,
            EditorPlatformBuildGraphWorkspace workspace) {
            EditorCodeModuleManifestDocument manifestDocument = CodeModuleManifestService.Load();
            IReadOnlyList<string> inferredRootModuleIds = DiscoverReferencedRuntimeModuleIdsFromCookedScenes(cookedManifest, workspace.ExecutionRootPath);
            return CodeCookService.CompileModules(
                manifestDocument,
                PlatformDescriptor.Id,
                selectedStorageProfile?.RuntimeSpecializationId ?? string.Empty,
                PlatformDescriptor.CodegenToolPath,
                selectedCodegenProfile,
                inferredRootModuleIds,
                queueItem.SelectedCodegenOptionValues,
                workspace.CodeRootPath);
        }

        /// <summary>
        /// Discovers the distinct runtime module ids referenced by the cooked scene payloads in the current manifest.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest whose selected scene payloads should be inspected.</param>
        /// <param name="packagedContentRootPath">Packaged content root that contains the cooked scene payloads beneath a top-level <c>cooked/</c> segment.</param>
        /// <returns>Distinct scene-referenced runtime module ids.</returns>
        IReadOnlyList<string> DiscoverReferencedRuntimeModuleIdsFromCookedScenes(PlatformBuildManifest cookedManifest, string packagedContentRootPath) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (string.IsNullOrWhiteSpace(packagedContentRootPath)) {
                throw new ArgumentException("Packaged content root path must be provided.", nameof(packagedContentRootPath));
            }

            List<string> cookedSceneAssetPaths = new List<string>(cookedManifest.Scenes.Length);
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                if (scene == null) {
                    continue;
                }

                cookedSceneAssetPaths.Add(ResolveCookedSceneAssetPath(scene, packagedContentRootPath));
            }

            return EditorGeneratedCoreRegenerationService.DiscoverReferencedRuntimeModuleIdsFromCookedScenes(
                cookedSceneAssetPaths,
                ScriptTypeResolver);
        }

        /// <summary>
        /// Copies the scene-referenced generated module source files into the shared generated-core root so native player builds can compile gameplay components against the shared runtime support tree.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest whose scene payloads should drive gameplay source inclusion.</param>
        /// <param name="codeModules">Compiled runtime code modules included in the current build.</param>
        /// <param name="generatedCoreRootPath">Generated core source root that will be compiled into the native player.</param>
        /// <param name="codeRootPath">Generated module output root produced by the authored-code phase.</param>
        /// <param name="packagedContentRootPath">Packaged content root that contains the cooked scene payloads beneath a top-level <c>cooked/</c> segment.</param>
        void CopySceneReferencedRuntimeModuleSourcesIntoGeneratedCore(
            PlatformBuildManifest cookedManifest,
            PlatformBuildCodeModule[] codeModules,
            string generatedCoreRootPath,
            string codeRootPath,
            string packagedContentRootPath) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (codeModules == null || codeModules.Length == 0) {
                return;
            }
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (string.IsNullOrWhiteSpace(codeRootPath)) {
                throw new ArgumentException("Code root path must be provided.", nameof(codeRootPath));
            }
            if (string.IsNullOrWhiteSpace(packagedContentRootPath)) {
                throw new ArgumentException("Packaged content root path must be provided.", nameof(packagedContentRootPath));
            }

            Dictionary<string, string> generatedModuleRootsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < codeModules.Length; index++) {
                PlatformBuildCodeModule codeModule = codeModules[index];
                if (codeModule != null && !string.IsNullOrWhiteSpace(codeModule.ModuleId)) {
                    generatedModuleRootsById[codeModule.ModuleId] = Path.Combine(codeRootPath, codeModule.ModuleId);
                }
            }

            List<string> cookedSceneAssetPaths = new List<string>(cookedManifest.Scenes.Length);
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                if (scene != null) {
                    cookedSceneAssetPaths.Add(ResolveCookedSceneAssetPath(scene, packagedContentRootPath));
                }
            }

            IReadOnlyList<Type> sceneReferencedComponentTypes = EditorGeneratedCoreRegenerationService.DiscoverAutomaticRuntimeComponentTypesFromCookedScenes(
                cookedSceneAssetPaths,
                ScriptTypeResolver);
            for (int typeIndex = 0; typeIndex < sceneReferencedComponentTypes.Count; typeIndex++) {
                Type componentType = sceneReferencedComponentTypes[typeIndex];
                string moduleId = componentType.Assembly.GetName().Name ?? string.Empty;
                if (!generatedModuleRootsById.TryGetValue(moduleId, out string generatedModuleRootPath)) {
                    continue;
                }
                if (!Directory.Exists(generatedModuleRootPath)) {
                    throw new DirectoryNotFoundException($"Compiled runtime code module root '{generatedModuleRootPath}' was not found.");
                }

                CopyGeneratedModuleSourceIfPresent(generatedModuleRootPath, generatedCoreRootPath, componentType.Name + ".hpp");
                CopyGeneratedModuleSourceIfPresent(generatedModuleRootPath, generatedCoreRootPath, componentType.Name + ".cpp");
            }
        }

        /// <summary>
        /// Regenerates native automatic runtime component deserializers for the assembly-qualified scripted component types referenced by the cooked scenes.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest whose scene payloads should drive generated deserializer coverage.</param>
        /// <param name="generatedCoreRootPath">Generated core source root that will be compiled into the native player.</param>
        /// <param name="packagedContentRootPath">Packaged content root that contains the cooked scene payloads beneath a top-level <c>cooked/</c> segment.</param>
        void EmitGeneratedRuntimeComponentDeserializersForCookedScenes(
            PlatformBuildManifest cookedManifest,
            string generatedCoreRootPath,
            string packagedContentRootPath) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (string.IsNullOrWhiteSpace(packagedContentRootPath)) {
                throw new ArgumentException("Packaged content root path must be provided.", nameof(packagedContentRootPath));
            }

            List<string> cookedSceneAssetPaths = new List<string>(cookedManifest.Scenes.Length);
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                if (scene == null) {
                    continue;
                }

                cookedSceneAssetPaths.Add(ResolveCookedSceneAssetPath(scene, packagedContentRootPath));
            }

            EditorGeneratedCoreRegenerationService.EmitCookedSceneAutomaticRuntimeComponentDeserializers(
                generatedCoreRootPath,
                cookedSceneAssetPaths,
                ScriptTypeResolver);
        }

        /// <summary>
        /// Copies one generated module source file into the shared generated-core root when the file exists and does not conflict with an existing core file.
        /// </summary>
        /// <param name="generatedModuleRootPath">Generated runtime module output root produced by the authored-code phase.</param>
        /// <param name="generatedCoreRootPath">Shared generated-core root consumed by the native player build.</param>
        /// <param name="fileName">Generated source file name to mirror.</param>
        void CopyGeneratedModuleSourceIfPresent(string generatedModuleRootPath, string generatedCoreRootPath, string fileName) {
            if (string.IsNullOrWhiteSpace(generatedModuleRootPath)) {
                throw new ArgumentException("Generated module root path must be provided.", nameof(generatedModuleRootPath));
            }
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(generatedModuleRootPath, fileName);
            if (!File.Exists(sourcePath)) {
                return;
            }

            string destinationPath = Path.Combine(generatedCoreRootPath, fileName);
            if (File.Exists(destinationPath)) {
                string existingContents = File.ReadAllText(destinationPath);
                string newContents = File.ReadAllText(sourcePath);
                if (!string.Equals(existingContents, newContents, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Generated runtime module source '{fileName}' conflicts with an existing generated-core source file.");
                }

                return;
            }

            File.Copy(sourcePath, destinationPath, true);
        }

        /// <summary>
        /// Resolves one cooked scene asset file path from the manifest scene entry and packaged content root.
        /// </summary>
        /// <param name="scene">Manifest scene entry whose cooked payload should be read.</param>
        /// <param name="packagedContentRootPath">Packaged content root directory that contains a top-level <c>cooked</c> tree.</param>
        /// <returns>Absolute cooked scene asset path.</returns>
        static string ResolveCookedSceneAssetPath(PlatformBuildScene scene, string packagedContentRootPath) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }
            if (string.IsNullOrWhiteSpace(packagedContentRootPath)) {
                throw new ArgumentException("Packaged content root path must be provided.", nameof(packagedContentRootPath));
            }

            string cookedRelativePath = ResolveCookedSceneRelativePath(scene);
            return Path.Combine(packagedContentRootPath, cookedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Resolves the cooked runtime-relative path recorded for one scene manifest entry.
        /// </summary>
        /// <param name="scene">Manifest scene entry whose metadata should be read.</param>
        /// <returns>Cooked runtime-relative scene path.</returns>
        static string ResolveCookedSceneRelativePath(PlatformBuildScene scene) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }

            if (scene.ResolvedMetadata != null) {
                for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
                    KeyValuePair<string, string> metadata = scene.ResolvedMetadata[index];
                    if (!string.Equals(metadata.Key, PlatformBuildSceneMetadataKeys.CookedRelativePath, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(metadata.Value)) {
                        return metadata.Value.Replace('\\', '/');
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(scene.SourceIdentity)) {
                return scene.SourceIdentity.Replace('\\', '/');
            }

            throw new InvalidOperationException($"Scene '{scene.SceneId}' did not define a cooked scene asset path.");
        }

        /// <summary>
        /// Executes the cooked-artifact variant resolution phase.
        /// </summary>
        PlatformBuildManifest RunResolveVariants(PlatformBuildManifest cookedManifest, EditorPlatformBuildGraphWorkspace workspace) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            EditorResolvedArtifactSet resolvedArtifactSet = ArtifactVariantResolver.Resolve(cookedManifest.CookedArtifacts ?? []);
            PlatformBuildArtifact[] resolvedArtifacts = [
                .. resolvedArtifactSet.SharedArtifacts,
                .. resolvedArtifactSet.PlatformVariants
            ];

            return new PlatformBuildManifest(
                cookedManifest.ManifestVersion,
                cookedManifest.ProjectId,
                cookedManifest.ProjectVersion,
                cookedManifest.RequiredEngineVersion,
                cookedManifest.PlatformName,
                cookedManifest.PlatformVersion,
                cookedManifest.StartupSceneId,
                cookedManifest.Scenes,
                cookedManifest.LooseAssets,
                resolvedArtifacts,
                cookedManifest.CodeModules,
                cookedManifest.ArtifactPlacements,
                cookedManifest.ContainerWritePlan,
                cookedManifest.PlatformCookWorkItems);
        }

        /// <summary>
        /// Executes the media-layout phase.
        /// </summary>
        PlatformBuildManifest RunLayoutMedia(
            PlatformBuildManifest cookedManifest,
            PlatformStorageProfileDefinition selectedStorageProfile,
            PlatformMediaProfileDefinition selectedMediaProfile,
            EditorPlatformBuildGraphWorkspace workspace) {
            return LayoutPlanService.Plan(cookedManifest, selectedStorageProfile, selectedMediaProfile);
        }

        /// <summary>
        /// Writes the generated runtime native manifest source into the combined generated-core tree.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest that already contains the final runtime scene layout.</param>
        /// <param name="generatedCoreRootPath">Generated core source root that will be compiled into the native player.</param>
        void WriteRuntimeNativeManifestSources(PlatformBuildManifest cookedManifest, string generatedCoreRootPath) {
            EditorRuntimeNativeManifestWriter writer = new();
            writer.Write(generatedCoreRootPath, cookedManifest);
        }

        /// <summary>
        /// Writes generated runtime renderer-default source into the combined generated-core tree.
        /// </summary>
        /// <param name="generatedCoreRootPath">Generated core source root that will be compiled into the native player.</param>
        /// <param name="selectionModel">Resolved builder metadata for the active platform.</param>
        void WriteRuntimeGraphicsRendererManifestSource(string generatedCoreRootPath, EditorPlatformBuildSelectionModel selectionModel) {
            EditorRuntimeGraphicsRendererManifestWriter writer = new();
            writer.Write(generatedCoreRootPath, ResolveRuntimeGraphicsRendererManifest(selectionModel));
        }

        /// <summary>
        /// Executes the container-writing phase using the current storage profile.
        /// </summary>
        void RunWriteContainers(
            PlatformBuildManifest cookedManifest,
            PlatformStorageProfileDefinition selectedStorageProfile,
            PlatformMediaProfileDefinition selectedMediaProfile,
            EditorPlatformBuildGraphWorkspace workspace) {
            CopyDirectoryTree(workspace.CodeRootPath, Path.Combine(workspace.PackageRootPath, "code"));
            ContainerWriter.Write(
                cookedManifest,
                workspace.CookRootPath,
                workspace.PackageRootPath,
                selectedStorageProfile,
                selectedMediaProfile);
            CopyDirectoryTree(workspace.CookRootPath, Path.Combine(workspace.PackageRootPath, "cooked"));
        }

        /// <summary>
        /// Mirrors a directory tree into a destination root preserving relative paths.
        /// </summary>
        static void CopyDirectoryTree(string sourceRootPath, string destinationRootPath) {
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                return;
            }

            if (!Directory.Exists(sourceRootPath)) {
                return;
            }

            Directory.CreateDirectory(destinationRootPath);
            string[] sourceFiles = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
                if (ShouldSkipPackagedCodePath(relativePath)) {
                    continue;
                }

                string destinationFilePath = Path.Combine(destinationRootPath, relativePath);
                string? destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                    Directory.CreateDirectory(destinationDirectoryPath);
                }

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }

        /// <summary>
        /// Resolves the renderer-default manifest for the active platform from persisted profile settings.
        /// </summary>
        /// <returns>Renderer-default manifest consumed by native manifest generation.</returns>
        /// <param name="selectionModel">Resolved builder metadata for the active platform.</param>
        RuntimeGraphicsRendererManifest ResolveRuntimeGraphicsRendererManifest(EditorPlatformBuildSelectionModel selectionModel) {
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            EditorGraphicsProfileSettingsDocument graphicsSettings = LoadPlatformGraphicsSettings(selectionModel);
            return new RuntimeGraphicsRendererManifest(
                graphicsSettings.RendererDepthPrepassMode,
                graphicsSettings.RendererShadowQualityTier,
                graphicsSettings.RendererHdrEnabled,
                graphicsSettings.RendererPostProcessTier,
                ResolvePs2DepthHandlerMode(graphicsSettings));
        }

        /// <summary>
        /// Loads the persisted graphics-profile settings for the active platform, falling back to defaults when unavailable.
        /// </summary>
        /// <param name="selectionModel">Resolved builder metadata for the active platform.</param>
        /// <returns>Normalized platform graphics-profile settings.</returns>
        EditorGraphicsProfileSettingsDocument LoadPlatformGraphicsSettings(EditorPlatformBuildSelectionModel selectionModel) {
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            EditorProfileSettingsService profileSettingsService = new EditorProfileSettingsService(ProjectRootPath);
            EditorProfileSettingsDocument document = profileSettingsService.TryLoadExisting();
            if (document == null || document.Platforms == null) {
                return new EditorGraphicsProfileSettingsDocument();
            }

            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorPlatformProfileSettingsDocument platform = document.Platforms[index];
                if (platform == null) {
                    continue;
                }
                if (!string.Equals(platform.PlatformId, PlatformDescriptor.Id, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (platform.Graphics == null) {
                    return new EditorGraphicsProfileSettingsDocument();
                }
                PlatformGraphicsProfileDefinition graphicsProfile = selectionModel.ResolveGraphicsProfile(platform.Graphics.SelectedGraphicsProfileId);
                if (graphicsProfile != null) {
                    EnsureSettingDefaults(platform.Graphics.SelectedOptionValues, graphicsProfile.Settings);
                }
                if (string.IsNullOrWhiteSpace(platform.Graphics.RendererShadowQualityTier)) {
                    platform.Graphics.RendererShadowQualityTier = "medium";
                }

                return platform.Graphics;
            }

            return new EditorGraphicsProfileSettingsDocument();
        }

        /// <summary>
        /// Seeds missing graphics-option values from the supplied setting definitions.
        /// </summary>
        /// <param name="values">Persisted option values.</param>
        /// <param name="settings">Builder-provided graphics setting definitions.</param>
        static void EnsureSettingDefaults(Dictionary<string, string> values, PlatformSettingDefinition[] settings) {
            if (values == null || settings == null) {
                return;
            }

            for (int index = 0; index < settings.Length; index++) {
                PlatformSettingDefinition setting = settings[index];
                if (!values.TryGetValue(setting.SettingId, out string existingValue) || string.IsNullOrWhiteSpace(existingValue)) {
                    values[setting.SettingId] = setting.DefaultValue;
                }
            }
        }

        /// <summary>
        /// Resolves the PS2 depth-handler mode from the persisted graphics-profile options.
        /// </summary>
        /// <param name="graphicsSettings">Normalized graphics-profile settings.</param>
        /// <returns>Resolved PS2 depth-handler mode.</returns>
        static Ps2DepthHandlerMode ResolvePs2DepthHandlerMode(EditorGraphicsProfileSettingsDocument graphicsSettings) {
            if (graphicsSettings == null) {
                throw new ArgumentNullException(nameof(graphicsSettings));
            }

            if (graphicsSettings.SelectedOptionValues != null &&
                graphicsSettings.SelectedOptionValues.TryGetValue("depth-handler-mode", out string selectedValue) &&
                !string.IsNullOrWhiteSpace(selectedValue) &&
                string.Equals(selectedValue, "software", StringComparison.OrdinalIgnoreCase)) {
                return Ps2DepthHandlerMode.Software;
            }

            return Ps2DepthHandlerMode.Hardware;
        }

        /// <summary>
        /// Returns true when one code-output path is part of the build scaffolding instead of runtime payload.
        /// </summary>
        static bool ShouldSkipPackagedCodePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return true;
            }

            string[] segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < segments.Length; index++) {
                string segment = segments[index];
                if (string.Equals(segment, "_project", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Executes the final platform package/build phase.
        /// </summary>
        EditorBuildExecutionResult RunPackagePlatform(
            IPlatformAssetBuilder builder,
            EditorBuildQueueItemDocument queueItem,
            EditorPlatformBuildGraphWorkspace workspace,
            PlatformBuildManifest cookedManifest,
            PlatformDefinition builderDefinition,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            string selectedCodegenProfileId,
            string selectedMediaProfileId,
            string selectedStorageProfileId) {
            PlatformBuildRequest request = BuildRequest(
                queueItem,
                cookedManifest,
                workspace.CookRootPath,
                workspace.BuilderWorkingRootPath,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                selectedCodegenProfileId,
                selectedMediaProfileId,
                workspace.GeneratedCoreRootPath,
                selectedStorageProfileId);
            EditorPlatformBuildProgressReporter progressReporter = new();
            EditorPlatformBuildDiagnosticCollector diagnosticCollector = new();
            string detectedFeatureSummary = BuildDetectedFeatureSummary(workspace.GeneratedCoreRootPath);

            string previousWorkingDirectory = Directory.GetCurrentDirectory();
            string previousPs2RepositoryRootPath = Environment.GetEnvironmentVariable(Ps2RepositoryRootEnvironmentVariableName) ?? string.Empty;
            string previousDsRepositoryRootPath = Environment.GetEnvironmentVariable(DsRepositoryRootEnvironmentVariableName) ?? string.Empty;
            string previousGameCubeRepositoryRootPath = Environment.GetEnvironmentVariable(GameCubeRepositoryRootEnvironmentVariableName) ?? string.Empty;
            try {
                StageBuilderPackageSourceRoot(workspace.PackageRootPath, workspace.BuilderWorkingRootPath);
                Directory.SetCurrentDirectory(workspace.PackageRootPath);
                ApplyBuilderEnvironmentOverrides();
                PlatformBuildReport report = builder.BuildAsync(request, progressReporter, diagnosticCollector, CancellationToken.None).GetAwaiter().GetResult();
                if (!report.Succeeded) {
                    return EditorBuildExecutionResult.Failure(AppendFeatureSummary(BuildFailureMessage(report), detectedFeatureSummary));
                }

                return EditorBuildExecutionResult.Success(
                    AppendFeatureSummary(
                        $"Build completed for platform '{PlatformDescriptor.Id}': {queueItem.OutputDirectoryPath}",
                        detectedFeatureSummary));
            } finally {
                RestoreBuilderEnvironmentOverrides(previousPs2RepositoryRootPath, previousDsRepositoryRootPath, previousGameCubeRepositoryRootPath);
                Directory.SetCurrentDirectory(previousWorkingDirectory);
            }
        }

        /// <summary>
        /// Applies temporary environment overrides required by builder implementations loaded into the editor process.
        /// </summary>
        void ApplyBuilderEnvironmentOverrides() {
            if (string.Equals(PlatformDescriptor.Id, "ps2", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(PlatformDescriptor.PlayerSourceRootPath)) {
                Environment.SetEnvironmentVariable(
                    Ps2RepositoryRootEnvironmentVariableName,
                    Path.GetFullPath(PlatformDescriptor.PlayerSourceRootPath));
            } else if (string.Equals(PlatformDescriptor.Id, "ds", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(PlatformDescriptor.PlayerSourceRootPath)) {
                Environment.SetEnvironmentVariable(
                    DsRepositoryRootEnvironmentVariableName,
                    Path.GetFullPath(PlatformDescriptor.PlayerSourceRootPath));
            } else if (string.Equals(PlatformDescriptor.Id, "gamecube", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(PlatformDescriptor.PlayerSourceRootPath)) {
                Environment.SetEnvironmentVariable(
                    GameCubeRepositoryRootEnvironmentVariableName,
                    Path.GetFullPath(PlatformDescriptor.PlayerSourceRootPath));
            }
        }

        /// <summary>
        /// Restores temporary builder environment overrides after one build graph execution finishes.
        /// </summary>
        /// <param name="previousPs2RepositoryRootPath">Previous PS2 repository-root environment variable value.</param>
        /// <param name="previousDsRepositoryRootPath">Previous Nintendo DS repository-root environment variable value.</param>
        /// <param name="previousGameCubeRepositoryRootPath">Previous GameCube repository-root environment variable value.</param>
        void RestoreBuilderEnvironmentOverrides(string previousPs2RepositoryRootPath, string previousDsRepositoryRootPath, string previousGameCubeRepositoryRootPath) {
            if (string.Equals(PlatformDescriptor.Id, "ps2", StringComparison.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(previousPs2RepositoryRootPath)) {
                    Environment.SetEnvironmentVariable(Ps2RepositoryRootEnvironmentVariableName, null);
                    return;
                }

                Environment.SetEnvironmentVariable(Ps2RepositoryRootEnvironmentVariableName, previousPs2RepositoryRootPath);
                return;
            } else if (string.Equals(PlatformDescriptor.Id, "ds", StringComparison.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(previousDsRepositoryRootPath)) {
                    Environment.SetEnvironmentVariable(DsRepositoryRootEnvironmentVariableName, null);
                    return;
                }

                Environment.SetEnvironmentVariable(DsRepositoryRootEnvironmentVariableName, previousDsRepositoryRootPath);
                return;
            } else if (string.Equals(PlatformDescriptor.Id, "gamecube", StringComparison.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(previousGameCubeRepositoryRootPath)) {
                    Environment.SetEnvironmentVariable(GameCubeRepositoryRootEnvironmentVariableName, null);
                    return;
                }

                Environment.SetEnvironmentVariable(GameCubeRepositoryRootEnvironmentVariableName, previousGameCubeRepositoryRootPath);
            }
        }

        static void ResetExecutionDirectories(string executionRoot, string cookRoot, string packageRoot, string builderWorkingRoot, string outputRoot) {
            DeleteDirectoryIfPresent(executionRoot);
            DeleteDirectoryIfPresent(outputRoot);

            Directory.CreateDirectory(cookRoot);
            Directory.CreateDirectory(packageRoot);
            Directory.CreateDirectory(builderWorkingRoot);
            Directory.CreateDirectory(outputRoot);
        }

        static void DeleteDirectoryIfPresent(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }

            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
        }

        static void StageBuilderPackageSourceRoot(string packageRootPath, string builderWorkingRootPath) {
            if (string.IsNullOrWhiteSpace(packageRootPath)) {
                throw new ArgumentException("Package root path must be provided.", nameof(packageRootPath));
            } else if (string.IsNullOrWhiteSpace(builderWorkingRootPath)) {
                throw new ArgumentException("Builder working root path must be provided.", nameof(builderWorkingRootPath));
            }

            string fullPackageRootPath = Path.GetFullPath(packageRootPath);
            string fullBuilderWorkingRootPath = Path.GetFullPath(builderWorkingRootPath);
            string builderPackageSourceRootPath = Path.Combine(fullBuilderWorkingRootPath, "package-source");
            if (!Directory.Exists(fullPackageRootPath)) {
                throw new DirectoryNotFoundException($"Package root '{fullPackageRootPath}' was not found.");
            }

            DeleteDirectoryIfPresent(builderPackageSourceRootPath);
            CopyDirectoryTree(fullPackageRootPath, builderPackageSourceRootPath);
        }

        PlatformBuildRequest BuildRequest(
            EditorBuildQueueItemDocument queueItem,
            PlatformBuildManifest cookedManifest,
            string stagingRoot,
            string builderWorkingRoot,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            string selectedCodegenProfileId,
            string selectedMediaProfileId,
            string generatedCoreRootPath,
            string selectedStorageProfileId) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            string[] stagedFilePaths = Directory.GetFiles(stagingRoot, "*", SearchOption.AllDirectories);
            Array.Sort(stagedFilePaths, StringComparer.OrdinalIgnoreCase);

            PlatformBuildPayloadReference[] stagedPayloadReferences = new PlatformBuildPayloadReference[stagedFilePaths.Length];
            HashSet<string> stagedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < stagedFilePaths.Length; index++) {
                string stagedFilePath = stagedFilePaths[index];
                string relativePath = NormalizeStagedManifestRelativePath(
                    stagingRoot,
                    NormalizeRelativePath(Path.GetRelativePath(stagingRoot, stagedFilePath)));
                stagedRelativePaths.Add(relativePath);
                stagedPayloadReferences[index] = new PlatformBuildPayloadReference(relativePath, relativePath);
            }

            PlatformBuildArtifact[] stagedCookedArtifacts = ResolveStagedCookedArtifacts(cookedManifest.CookedArtifacts, stagedRelativePaths);

            PlatformBuildScene[] manifestScenes = new PlatformBuildScene[cookedManifest.Scenes.Length];
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                PlatformBuildPayloadReference[] payloadReferences = index == 0 ? stagedPayloadReferences : [];
                manifestScenes[index] = new PlatformBuildScene(
                    scene.SceneId,
                    scene.SceneName,
                    scene.SourceIdentity,
                    payloadReferences,
                    scene.ResolvedMetadata);
            }

            PlatformBuildManifest manifest = new(
                cookedManifest.ManifestVersion,
                cookedManifest.ProjectId,
                cookedManifest.ProjectVersion,
                cookedManifest.RequiredEngineVersion,
                cookedManifest.PlatformName,
                cookedManifest.PlatformVersion,
                cookedManifest.StartupSceneId,
                manifestScenes,
                cookedManifest.LooseAssets,
                stagedCookedArtifacts,
                cookedManifest.CodeModules,
                cookedManifest.ArtifactPlacements,
                cookedManifest.ContainerWritePlan,
                cookedManifest.PlatformCookWorkItems);

            PlatformBuildTargetVariant[] targetVariants = [
                new PlatformBuildTargetVariant(
                    selectedBuildProfileId,
                    PlatformDescriptor.Id,
                    PlatformDescriptor.Id,
                    selectedBuildProfileId)
            ];

            PlatformCookProfile[] cookProfiles = [
                new PlatformCookProfile(
                    selectedBuildProfileId,
                    selectedBuildProfileId,
                    new PlatformCookProfileCapabilities(
                        PlatformDescriptor.Id,
                        selectedGraphicsProfileId,
                        "raw",
                        $"{PlatformDescriptor.Id}-scene-v1",
                        PlatformSerializationEndianness.LittleEndian))
            ];

            return new PlatformBuildRequest(
                manifest,
                targetVariants,
                cookProfiles,
                queueItem.OutputDirectoryPath,
                builderWorkingRoot,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                selectedCodegenProfileId,
                queueItem.SelectedBuildOptionValues,
                queueItem.SelectedGraphicsOptionValues,
                queueItem.SelectedCodegenOptionValues,
                generatedCoreRootPath,
                selectedMediaProfileId,
                selectedStorageProfileId);
        }

        static string BuildFailureMessage(PlatformBuildReport report) {
            if (report == null) {
                return "Build failed with no report.";
            }

            PlatformBuildDiagnostic diagnostic = report.Diagnostics?.FirstOrDefault();
            if (diagnostic != null && !string.IsNullOrWhiteSpace(diagnostic.Message)) {
                return diagnostic.Message;
            }

            PlatformBuildItemOutcome failedSceneOutcome = report.SceneOutcomes?.FirstOrDefault(outcome => outcome?.OutcomeKind == PlatformBuildItemOutcomeKind.Failed);
            if (failedSceneOutcome != null) {
                return $"Build failed for scene '{failedSceneOutcome.ItemId}'.";
            }

            PlatformBuildItemOutcome failedLooseAssetOutcome = report.LooseAssetOutcomes?.FirstOrDefault(outcome => outcome?.OutcomeKind == PlatformBuildItemOutcomeKind.Failed);
            if (failedLooseAssetOutcome != null) {
                return $"Build failed for asset '{failedLooseAssetOutcome.ItemId}'.";
            }

            return "Build failed.";
        }

        /// <summary>
        /// Builds one human-readable runtime feature summary from the generated conversion report when it is available.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute generated-core root path that may contain the conversion report.</param>
        /// <returns>Human-readable feature summary, or an empty string when no report is available.</returns>
        static string BuildDetectedFeatureSummary(string generatedCoreRootPath) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                return string.Empty;
            }

            string reportPath = Path.Combine(generatedCoreRootPath, "cpp-conversion-report.json");
            if (!File.Exists(reportPath)) {
                return string.Empty;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportPath));
            if (!document.RootElement.TryGetProperty("buildFeatures", out JsonElement buildFeatures)
                || !buildFeatures.TryGetProperty("decisions", out JsonElement decisions)
                || decisions.ValueKind != JsonValueKind.Array) {
                return string.Empty;
            }

            List<string> enabledFeatures = [];
            List<string> disabledFeatures = [];
            foreach (JsonElement decision in decisions.EnumerateArray()) {
                if (!TryReadFeatureDecision(decision, out string featureName, out bool enabled, out string origin)) {
                    continue;
                }

                string description = $"{featureName} ({origin})";
                if (enabled) {
                    enabledFeatures.Add(description);
                } else {
                    disabledFeatures.Add(description);
                }
            }

            if (enabledFeatures.Count == 0 && disabledFeatures.Count == 0) {
                return string.Empty;
            }

            StringBuilder summaryBuilder = new();
            if (enabledFeatures.Count > 0) {
                summaryBuilder.Append("Enabled runtime features: ");
                summaryBuilder.Append(string.Join(", ", enabledFeatures));
                summaryBuilder.Append('.');
            }

            if (disabledFeatures.Count > 0) {
                if (summaryBuilder.Length > 0) {
                    summaryBuilder.Append(' ');
                }

                summaryBuilder.Append("Disabled runtime features: ");
                summaryBuilder.Append(string.Join(", ", disabledFeatures));
                summaryBuilder.Append('.');
            }

            return summaryBuilder.ToString();
        }

        /// <summary>
        /// Attempts to read one build-feature decision from the generated conversion report.
        /// </summary>
        /// <param name="decision">JSON element that may describe one feature decision.</param>
        /// <param name="featureName">Resolved feature name when present.</param>
        /// <param name="enabled">Resolved enabled state when present.</param>
        /// <param name="origin">Resolved decision origin when present.</param>
        /// <returns>True when the decision payload is complete and valid.</returns>
        static bool TryReadFeatureDecision(JsonElement decision, out string featureName, out bool enabled, out string origin) {
            featureName = string.Empty;
            enabled = false;
            origin = string.Empty;

            if (!decision.TryGetProperty("feature", out JsonElement featureElement)
                || !decision.TryGetProperty("enabled", out JsonElement enabledElement)
                || !decision.TryGetProperty("origin", out JsonElement originElement)
                || (enabledElement.ValueKind != JsonValueKind.False && enabledElement.ValueKind != JsonValueKind.True)) {
                return false;
            }

            featureName = featureElement.GetString() ?? string.Empty;
            origin = originElement.GetString() ?? string.Empty;
            enabled = enabledElement.GetBoolean();
            return !string.IsNullOrWhiteSpace(featureName) && !string.IsNullOrWhiteSpace(origin);
        }

        /// <summary>
        /// Appends one optional feature summary beneath an existing build result message.
        /// </summary>
        /// <param name="message">Primary build result message.</param>
        /// <param name="featureSummary">Optional feature summary.</param>
        /// <returns>Combined build result message.</returns>
        static string AppendFeatureSummary(string message, string featureSummary) {
            if (string.IsNullOrWhiteSpace(featureSummary)) {
                return message;
            }

            return message + Environment.NewLine + featureSummary;
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        static string NormalizeStagedManifestRelativePath(string stagingRoot, string relativePath) {
            if (string.IsNullOrWhiteSpace(stagingRoot)) {
                throw new ArgumentException("Staging root path must be provided.", nameof(stagingRoot));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedRelativePath = NormalizeRelativePath(relativePath);
            string stagingRootName = Path.GetFileName(Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(stagingRootName, "cooked", StringComparison.OrdinalIgnoreCase)) {
                return "cooked/" + normalizedRelativePath;
            }

            return normalizedRelativePath;
        }

        static PlatformBuildArtifact[] ResolveStagedCookedArtifacts(PlatformBuildArtifact[] cookedArtifacts, ISet<string> stagedRelativePaths) {
            if (cookedArtifacts == null || cookedArtifacts.Length == 0) {
                return [];
            }
            if (stagedRelativePaths == null) {
                throw new ArgumentNullException(nameof(stagedRelativePaths));
            }

            List<PlatformBuildArtifact> stagedArtifacts = [];
            for (int index = 0; index < cookedArtifacts.Length; index++) {
                PlatformBuildArtifact artifact = cookedArtifacts[index];
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.RelativePath)) {
                    continue;
                }

                if (!stagedRelativePaths.Contains(artifact.RelativePath)) {
                    continue;
                }

                stagedArtifacts.Add(artifact);
            }

            return [.. stagedArtifacts];
        }

        static PlatformBuildManifest ReplaceCodeModules(PlatformBuildManifest manifest, PlatformBuildCodeModule[] codeModules) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            return new PlatformBuildManifest(
                manifest.ManifestVersion,
                manifest.ProjectId,
                manifest.ProjectVersion,
                manifest.RequiredEngineVersion,
                manifest.PlatformName,
                manifest.PlatformVersion,
                manifest.StartupSceneId,
                manifest.Scenes,
                manifest.LooseAssets,
                manifest.CookedArtifacts,
                codeModules ?? [],
                manifest.ArtifactPlacements,
                manifest.ContainerWritePlan,
                manifest.PlatformCookWorkItems);
        }

        static string ResolveSelectedBuildProfileId(EditorBuildQueueItemDocument queueItem, EditorPlatformBuildSelectionModel selectionModel) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedBuildProfileId)) {
                PlatformBuildProfileDefinition requestedBuildProfile = selectionModel.ResolveBuildProfile(queueItem.SelectedBuildProfileId);
                if (requestedBuildProfile != null) {
                    return requestedBuildProfile.ProfileId;
                }
            }

            if (queueItem.DebugBuild) {
                PlatformBuildProfileDefinition requestedDebugProfile = selectionModel.ResolveBuildProfile("debug");
                if (requestedDebugProfile != null) {
                    return requestedDebugProfile.ProfileId;
                }
            } else {
                PlatformBuildProfileDefinition requestedReleaseProfile = selectionModel.ResolveBuildProfile("release");
                if (requestedReleaseProfile != null) {
                    return requestedReleaseProfile.ProfileId;
                }
            }

            PlatformBuildProfileDefinition fallbackBuildProfile = selectionModel.ResolveBuildProfile(string.Empty);
            if (fallbackBuildProfile != null) {
                return fallbackBuildProfile.ProfileId;
            }

            return queueItem.DebugBuild ? "debug" : "release";
        }

        static string ResolveSelectedGraphicsProfileId(
            EditorBuildQueueItemDocument queueItem,
            string selectedBuildProfileId,
            EditorPlatformBuildSelectionModel selectionModel) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            PlatformBuildProfileDefinition resolvedBuildProfile = selectionModel.ResolveBuildProfile(selectedBuildProfileId);
            if (resolvedBuildProfile != null && !string.IsNullOrWhiteSpace(resolvedBuildProfile.GraphicsProfileId)) {
                return resolvedBuildProfile.GraphicsProfileId;
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedGraphicsProfileId)) {
                PlatformGraphicsProfileDefinition selectedGraphicsProfile = selectionModel.ResolveGraphicsProfile(queueItem.SelectedGraphicsProfileId);
                if (selectedGraphicsProfile != null) {
                    return selectedGraphicsProfile.ProfileId;
                }
            }

            PlatformGraphicsProfileDefinition defaultGraphicsProfile = selectionModel.ResolveGraphicsProfile(string.Empty);
            if (defaultGraphicsProfile != null) {
                return defaultGraphicsProfile.ProfileId;
            }

            return string.Empty;
        }

        static string ResolveSelectedCodegenProfileId(
            EditorBuildQueueItemDocument queueItem,
            string selectedBuildProfileId,
            EditorPlatformBuildSelectionModel selectionModel) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedCodegenProfileId)) {
                PlatformCodegenProfileDefinition selectedCodegenProfile = selectionModel.ResolveCodegenProfile(queueItem.SelectedCodegenProfileId);
                if (selectedCodegenProfile != null) {
                    return selectedCodegenProfile.ProfileId;
                }
            }

            PlatformBuildProfileDefinition resolvedBuildProfile = selectionModel.ResolveBuildProfile(selectedBuildProfileId);
            if (resolvedBuildProfile != null && !string.IsNullOrWhiteSpace(resolvedBuildProfile.CodegenProfileId)) {
                return resolvedBuildProfile.CodegenProfileId;
            }

            PlatformCodegenProfileDefinition defaultCodegenProfile = selectionModel.ResolveCodegenProfile(string.Empty);
            if (defaultCodegenProfile != null) {
                return defaultCodegenProfile.ProfileId;
            }

            return string.Empty;
        }

        static string ResolveSelectedMediaProfileId(EditorBuildQueueItemDocument queueItem, EditorPlatformBuildSelectionModel selectionModel) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedMediaProfileId)) {
                PlatformMediaProfileDefinition requestedMediaProfile = selectionModel.ResolveMediaProfile(queueItem.SelectedMediaProfileId);
                if (requestedMediaProfile != null) {
                    return requestedMediaProfile.ProfileId;
                }
            }

            PlatformMediaProfileDefinition defaultMediaProfile = selectionModel.ResolveMediaProfile(string.Empty);
            return defaultMediaProfile?.ProfileId ?? string.Empty;
        }

        static string ResolveSelectedStorageProfileId(EditorBuildQueueItemDocument queueItem, EditorPlatformBuildSelectionModel selectionModel) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedStorageProfileId)) {
                PlatformStorageProfileDefinition requestedStorageProfile = selectionModel.ResolveStorageProfile(queueItem.SelectedStorageProfileId);
                if (requestedStorageProfile != null) {
                    return requestedStorageProfile.ProfileId;
                }
            }

            PlatformStorageProfileDefinition defaultStorageProfile = selectionModel.ResolveStorageProfile(string.Empty);
            return defaultStorageProfile?.ProfileId ?? string.Empty;
        }

    }
}
