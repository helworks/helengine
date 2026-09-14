using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Decides which platforms the editor's build, platform and profile menus may offer for the
    /// open project, and builds the executors those menus dispatch queued builds through. A platform
    /// is offerable only when the project declares it and the local engine installation actually
    /// carries its payload, so these two independent facts — project intent and local installation —
    /// are reconciled here rather than inside the dialog handlers, and only platforms that pass the
    /// same test receive a build executor. The editor session still owns the dialogs and the
    /// persisted active-platform choice.
    /// </summary>
    public sealed class EditorBuildMenuCoordinator {
        /// <summary>
        /// Loads the platforms available to the local engine installation.
        /// </summary>
        AvailablePlatformProviderResolver AvailablePlatformProviderResolver { get; }

        /// <summary>
        /// Exact engine version the open project requires; platform payloads are resolved against it.
        /// </summary>
        string RequiredEngineVersion { get; }

        /// <summary>
        /// Absolute root path of the open project, handed to every platform build executor.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Stable project identifier reported to platform builders.
        /// </summary>
        string ProjectName { get; }

        /// <summary>
        /// Human-visible project version reported to platform builders.
        /// </summary>
        string ProjectVersion { get; }

        /// <summary>
        /// Importer registrations supplied by the editor host and reused by every platform build.
        /// </summary>
        IReadOnlyList<IAssetImporterRegistration> Importers { get; }

        /// <summary>
        /// Default font asset packaged into player builds produced by the created executors.
        /// </summary>
        FontAsset DefaultFontAsset { get; }

        /// <summary>
        /// Owns the live script type resolver shared with the gameplay modules a build must cook.
        /// </summary>
        EditorGameScriptHotReloadService ScriptHotReloadService { get; }

        /// <summary>
        /// Supplies the engine-provided shader assets every platform build links against.
        /// </summary>
        EditorBuiltInShaderAssetLibrary BuiltInShaderAssetLibrary { get; }

        /// <summary>
        /// Initializes one build-menu coordinator for the open project.
        /// </summary>
        /// <param name="availablePlatformProviderResolver">Resolver that loads locally available platforms.</param>
        /// <param name="requiredEngineVersion">Exact engine version declared by the project file.</param>
        /// <param name="projectRootPath">Absolute root path of the open project.</param>
        /// <param name="projectName">Stable project identifier reported to platform builders.</param>
        /// <param name="projectVersion">Human-visible project version reported to platform builders.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="defaultFontAsset">Default font asset packaged into player builds.</param>
        /// <param name="scriptHotReloadService">Hot-reload service that owns the shared script type resolver.</param>
        /// <param name="builtInShaderAssetLibrary">Engine-provided shader asset library used by platform builds.</param>
        public EditorBuildMenuCoordinator(
            AvailablePlatformProviderResolver availablePlatformProviderResolver,
            string requiredEngineVersion,
            string projectRootPath,
            string projectName,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            FontAsset defaultFontAsset,
            EditorGameScriptHotReloadService scriptHotReloadService,
            EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary) {
            if (availablePlatformProviderResolver == null) {
                throw new ArgumentNullException(nameof(availablePlatformProviderResolver));
            }
            if (string.IsNullOrWhiteSpace(requiredEngineVersion)) {
                throw new ArgumentException("Required engine version must be provided.", nameof(requiredEngineVersion));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(projectName)) {
                throw new ArgumentException("Project name must be provided.", nameof(projectName));
            }
            if (string.IsNullOrWhiteSpace(projectVersion)) {
                throw new ArgumentException("Project version must be provided.", nameof(projectVersion));
            }
            if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            }
            if (defaultFontAsset == null) {
                throw new ArgumentNullException(nameof(defaultFontAsset));
            }
            if (scriptHotReloadService == null) {
                throw new ArgumentNullException(nameof(scriptHotReloadService));
            }
            if (builtInShaderAssetLibrary == null) {
                throw new ArgumentNullException(nameof(builtInShaderAssetLibrary));
            }

            AvailablePlatformProviderResolver = availablePlatformProviderResolver;
            RequiredEngineVersion = requiredEngineVersion;
            ProjectRootPath = projectRootPath;
            ProjectName = projectName;
            ProjectVersion = projectVersion;
            Importers = importers;
            DefaultFontAsset = defaultFontAsset;
            ScriptHotReloadService = scriptHotReloadService;
            BuiltInShaderAssetLibrary = builtInShaderAssetLibrary;
        }

        /// <summary>
        /// Resolves the platform id that should be shown first in one filtered dialog without persisting any replacement.
        /// </summary>
        /// <param name="visiblePlatformIds">Platforms currently visible in the dialog.</param>
        /// <param name="preferredPlatformId">Preferred platform id, typically the user-local active platform.</param>
        /// <returns>Visible platform id to show first.</returns>
        public static string ResolveVisiblePlatformId(IReadOnlyList<string> visiblePlatformIds, string preferredPlatformId) {
            if (visiblePlatformIds == null) {
                throw new ArgumentNullException(nameof(visiblePlatformIds));
            }
            if (visiblePlatformIds.Count < 1) {
                throw new InvalidOperationException("At least one visible platform is required.");
            }

            if (!string.IsNullOrWhiteSpace(preferredPlatformId)) {
                for (int index = 0; index < visiblePlatformIds.Count; index++) {
                    if (string.Equals(visiblePlatformIds[index], preferredPlatformId, StringComparison.OrdinalIgnoreCase)) {
                        return visiblePlatformIds[index];
                    }
                }
            }

            return visiblePlatformIds[0];
        }

        /// <summary>
        /// Returns true when the supplied platform exists in the current engine catalog and has an installed payload.
        /// </summary>
        /// <param name="platformId">Platform identifier to inspect.</param>
        /// <returns>True when the platform is installed for the current engine; otherwise false.</returns>
        public bool IsInstalledPlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms = AvailablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            for (int index = 0; index < availablePlatforms.Count; index++) {
                if (string.Equals(availablePlatforms[index].Id, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return availablePlatforms[index].IsInstalled;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the installed platform identifiers currently available for the active engine version.
        /// </summary>
        /// <returns>Alphabetically ordered installed platform identifiers.</returns>
        public IReadOnlyList<string> ResolveInstalledPlatformIds() {
            return AvailablePlatformProviderResolver
                .LoadPlatforms(RequiredEngineVersion)
                .Where(platform => platform.IsInstalled)
                .Select(platform => platform.Id)
                .OrderBy(platformId => platformId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Resolves the platforms that are both project-enabled and currently installed on the local machine.
        /// </summary>
        /// <param name="supportedPlatforms">Platform identifiers declared by the project.</param>
        /// <returns>Alphabetically ordered visible platform identifiers.</returns>
        public IReadOnlyList<string> ResolveVisibleSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            IReadOnlyList<string> installedPlatformIds = ResolveInstalledPlatformIds();
            if (installedPlatformIds.Count < 1 || supportedPlatforms.Count < 1) {
                return Array.Empty<string>();
            }

            HashSet<string> installedPlatformIdSet = new HashSet<string>(installedPlatformIds, StringComparer.OrdinalIgnoreCase);
            List<string> visiblePlatformIds = new List<string>(supportedPlatforms.Count);
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (installedPlatformIdSet.Contains(platformId)) {
                    visiblePlatformIds.Add(platformId);
                }
            }

            visiblePlatformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return visiblePlatformIds;
        }

        /// <summary>
        /// Returns true when the supplied platform is both project-supported and installed for the current engine.
        /// </summary>
        /// <param name="supportedPlatforms">Platform identifiers declared by the project.</param>
        /// <param name="platformId">Platform identifier to validate.</param>
        /// <returns>True when the platform can be used as the current project platform.</returns>
        public bool CanUseProjectPlatform(IReadOnlyList<string> supportedPlatforms, string platformId) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            for (int index = 0; index < supportedPlatforms.Count; index++) {
                if (string.Equals(supportedPlatforms[index], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return IsInstalledPlatform(platformId);
                }
            }

            return false;
        }

        /// <summary>
        /// Creates the build executor router for the platforms that are installed for the current engine
        /// version and expose a builder assembly. Platforms without a payload are intentionally absent so
        /// the router reports an explicit "no executor registered" failure instead of building nothing.
        /// </summary>
        /// <returns>Router keyed by platform identifier.</returns>
        public EditorBuildExecutorRouter CreateBuildExecutorRouter() {
            IReadOnlyList<AvailablePlatformDescriptor> platforms = AvailablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            Dictionary<string, IEditorBuildExecutor> executorsByPlatformId = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < platforms.Count; index++) {
                AvailablePlatformDescriptor platform = platforms[index];
                if (!platform.IsInstalled || string.IsNullOrWhiteSpace(platform.BuilderAssemblyPath)) {
                    continue;
                }

                executorsByPlatformId[platform.Id] = new EditorPlatformBuildExecutor(
                    ProjectRootPath,
                    RequiredEngineVersion,
                    ProjectName,
                    ProjectVersion,
                    Importers,
                    platform,
                    DefaultFontAsset,
                    null,
                    ScriptHotReloadService.ScriptTypeResolver,
                    BuiltInShaderAssetLibrary);
            }

            return new EditorBuildExecutorRouter(executorsByPlatformId);
        }

        /// <summary>
        /// Decides whether the editor must force the Platforms workflow because the persisted project
        /// platform is no longer usable. With no installed platforms at all the workflow would trap the
        /// user with nothing to pick, so that case is reported as a broken installation instead.
        /// </summary>
        /// <param name="supportedPlatforms">Platform identifiers declared by the project.</param>
        /// <param name="activePlatformId">Platform currently persisted as the project's active platform.</param>
        /// <returns>True when the caller should open the Platforms workflow.</returns>
        public bool RequiresPlatformSelectionPrompt(IReadOnlyList<string> supportedPlatforms, string activePlatformId) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            if (CanUseProjectPlatform(supportedPlatforms, activePlatformId)) {
                return false;
            }

            if (ResolveInstalledPlatformIds().Count == 0) {
                Logger.WriteError($"No engine platforms are installed for engine version '{RequiredEngineVersion}'. The engine host platform must be installed; this editor installation is broken.");
                return false;
            }

            return true;
        }
    }
}
