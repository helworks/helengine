using System.Drawing;
using helengine;
using helengine.editor;
using helengine.platforms;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;

namespace helengine.debugtools {
    /// <summary>
    /// Builds one Windows export that boots directly into the authored scene-memory probe while still packaging the menu and cube-test scenes it navigates between.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Stable city project path used for the scene-memory probe build.
        /// </summary>
        const string ProjectPath = @"C:\dev\helprojs\city\project.heproj";

        /// <summary>
        /// Stable output directory used for the probe-oriented Windows export.
        /// </summary>
        const string OutputDirectoryPath = @"C:\dev\helprojs\output\windows_probe_nav";

        /// <summary>
        /// Stable Windows platform identifier used by the installed builder metadata.
        /// </summary>
        const string WindowsPlatformId = "windows";

        /// <summary>
        /// Stable authored scene id for the persistent probe startup scene.
        /// </summary>
        const string ProbeSceneId = "scene_memory_probe";

        /// <summary>
        /// Stable authored scene id for the desktop demo-disc main menu.
        /// </summary>
        const string MainMenuSceneId = "DemoDiscMainMenu";

        /// <summary>
        /// Stable authored scene id for the minimal cube-test rendering showcase.
        /// </summary>
        const string CubeTestSceneId = "cube_test";

        /// <summary>
        /// Builds one probe-specific Windows export and prints the resulting status message.
        /// </summary>
        /// <param name="args">Unused command-line arguments.</param>
        /// <returns>Zero when the build succeeds; otherwise one.</returns>
        public static int Main(string[] args) {
            try {
                EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(ProjectPath);
                EditorBuildExecutionResult scriptLoadResult = BuildAndLoadProjectScripts(
                    bootstrap,
                    out EditorGameScriptAssemblyHost assemblyHost,
                    out EditorGameScriptHotReloadService hotReloadService);
                using (assemblyHost)
                using (hotReloadService) {
                    if (!scriptLoadResult.Succeeded) {
                        Console.Error.WriteLine(scriptLoadResult.Message);
                        return 1;
                    }

                    EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting();
                    if (buildConfig == null) {
                        Console.Error.WriteLine($"No existing build settings were found for project '{bootstrap.ProjectDisplayName}'.");
                        return 1;
                    }

                    EditorBuildPlatformConfigDocument platformConfig = FindRequiredPlatformConfig(buildConfig, WindowsPlatformId);
                    EditorPlatformBuildSelectionModel selectionModel = bootstrap.ResolveSelectionModel(WindowsPlatformId);
                    EditorBuildQueueItemDocument queueItem = EditorBuildQueueItemDocument.Create(
                        bootstrap.SceneCatalogService,
                        platformConfig,
                        selectionModel,
                        OutputDirectoryPath);
                    queueItem.SelectedSceneIds = new List<string> {
                        FindRequiredSceneId(bootstrap, ProbeSceneId),
                        FindRequiredSceneId(bootstrap, MainMenuSceneId),
                        FindRequiredSceneId(bootstrap, CubeTestSceneId)
                    };

                    AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor(WindowsPlatformId);
                    Console.WriteLine($"Resolved codegen tool path: {platformDescriptor.CodegenToolPath}");
                    FontAsset defaultFontAsset = GDIFontProcessor.ImportFont(new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel));
                    IReadOnlyList<IAssetImporterRegistration> importers = CreateDefaultImporters();
                    EditorPlatformBuildExecutor executor = new EditorPlatformBuildExecutor(
                        bootstrap.ProjectRootPath,
                        bootstrap.RequiredEngineVersion,
                        bootstrap.ProjectName,
                        bootstrap.ProjectVersion,
                        importers,
                        platformDescriptor,
                        defaultFontAsset,
                        null,
                        assemblyHost.ScriptTypeResolver);
                    EditorBuildExecutionResult result = executor.Execute(queueItem);
                    if (!result.Succeeded) {
                        Console.Error.WriteLine(result.Message);
                        return 1;
                    }

                    Console.WriteLine(result.Message);
                    return 0;
                }
            } catch (Exception exception) {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        /// <summary>
        /// Generates, builds, and loads the current project's script assemblies so project-authored commands and components are available to the build graph.
        /// </summary>
        /// <param name="bootstrap">Bootstrap context for the active city project.</param>
        /// <param name="assemblyHost">Loaded script assembly host when initialization succeeds.</param>
        /// <param name="hotReloadService">Hot-reload service that owns the loaded script assemblies.</param>
        /// <returns>Structured result describing whether project scripts loaded successfully.</returns>
        static EditorBuildExecutionResult BuildAndLoadProjectScripts(
            EditorProjectBootstrapContext bootstrap,
            out EditorGameScriptAssemblyHost assemblyHost,
            out EditorGameScriptHotReloadService hotReloadService) {
            if (bootstrap == null) {
                throw new ArgumentNullException(nameof(bootstrap));
            }

            EditorGameSolutionService solutionService = new EditorGameSolutionService(
                bootstrap.ProjectRootPath,
                bootstrap.ProjectName,
                new EditorVisualStudioLauncher());
            EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();
            assemblyHost = new EditorGameScriptAssemblyHost(bootstrap.ProjectRootPath);
            hotReloadService = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);
            return hotReloadService.BuildAndReload();
        }

        /// <summary>
        /// Resolves the persisted build configuration entry for the requested platform.
        /// </summary>
        /// <param name="buildConfig">Loaded build configuration document.</param>
        /// <param name="platformId">Stable platform identifier to resolve.</param>
        /// <returns>Matching platform configuration.</returns>
        static EditorBuildPlatformConfigDocument FindRequiredPlatformConfig(EditorBuildConfigDocument buildConfig, string platformId) {
            if (buildConfig == null) {
                throw new ArgumentNullException(nameof(buildConfig));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            for (int index = 0; index < buildConfig.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platformConfig = buildConfig.Platforms[index];
                if (platformConfig != null && string.Equals(platformConfig.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platformConfig;
                }
            }

            throw new InvalidOperationException($"No build settings exist for platform '{platformId}'.");
        }

        /// <summary>
        /// Resolves one authored scene id from the project scene catalog and fails when the requested scene is unavailable.
        /// </summary>
        /// <param name="bootstrap">Bootstrap context whose scene catalog should be inspected.</param>
        /// <param name="requestedSceneId">Stable authored scene id to resolve.</param>
        /// <returns>Catalog scene id that matches the requested identifier.</returns>
        static string FindRequiredSceneId(EditorProjectBootstrapContext bootstrap, string requestedSceneId) {
            if (bootstrap == null) {
                throw new ArgumentNullException(nameof(bootstrap));
            } else if (string.IsNullOrWhiteSpace(requestedSceneId)) {
                throw new ArgumentException("Requested scene id must be provided.", nameof(requestedSceneId));
            }

            IReadOnlyList<string> sceneIds = bootstrap.SceneCatalogService.GetSceneIds();
            for (int index = 0; index < sceneIds.Count; index++) {
                string sceneId = sceneIds[index];
                if (string.Equals(sceneId, requestedSceneId, StringComparison.OrdinalIgnoreCase)) {
                    return sceneId;
                }
            }

            throw new InvalidOperationException($"Scene '{requestedSceneId}' was not found in the project scene catalog.");
        }

        /// <summary>
        /// Builds the default importer registrations required by the Windows editor build graph.
        /// </summary>
        /// <returns>Default asset importer registrations for textures, text, fonts, and models.</returns>
        static IReadOnlyList<IAssetImporterRegistration> CreateDefaultImporters() {
            string[] textExtensions = [".txt"];
            string[] modelExtensions = [".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".x"];
            string[] fontExtensions = [".ttf", ".otf"];
            List<IAssetImporterRegistration> registrations = new List<IAssetImporterRegistration>(EditorHostTextureImporterFactory.CreateDefault());
            registrations.AddRange([
                new TextImporterRegistration("text", new TextImporter(), textExtensions),
                new FontImporterRegistration("gdi-font", new GdiFontImporter(), fontExtensions),
                new ModelImporterRegistration(
                    "assimp",
                    new LazyModelImporter(new AssemblyModelImporterFactory("helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")),
                    modelExtensions)
            ]);
            return registrations;
        }
    }
}
