using System.Text;
using helengine.platforms;
using helengine.projectfile;

namespace helengine.editor {
    /// <summary>
    /// Captures the shared project and platform bootstrap state used by GUI and CLI build workflows.
    /// </summary>
    public sealed class EditorProjectBootstrapContext {
        /// <summary>
        /// Initializes one bootstrap context for the supplied project state.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Absolute canonical project file path.</param>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <param name="projectDisplayName">Project file name shown by host UI.</param>
        /// <param name="projectDocument">Parsed canonical project document.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the project.</param>
        /// <param name="availablePlatforms">Installed platform catalog resolved for the current engine version.</param>
        /// <param name="availablePlatformProviderResolver">Resolver that discovered the installed platform catalog.</param>
        /// <param name="platformCatalogService">Dynamic platform builder catalog service.</param>
        /// <param name="sceneCatalogService">Project scene catalog service.</param>
        /// <param name="buildConfigService">Local build configuration service.</param>
        /// <param name="profileSettingsService">Local profile configuration service.</param>
        public EditorProjectBootstrapContext(
            string canonicalProjectFilePath,
            string projectRootPath,
            string projectDisplayName,
            ProjectFileDocument projectDocument,
            IReadOnlyList<string> supportedPlatforms,
            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms,
            AvailablePlatformProviderResolver availablePlatformProviderResolver,
            EditorPlatformCatalogService platformCatalogService,
            EditorProjectSceneCatalogService sceneCatalogService,
            EditorBuildConfigService buildConfigService,
            EditorProfileSettingsService profileSettingsService) {
            if (string.IsNullOrWhiteSpace(canonicalProjectFilePath)) {
                throw new ArgumentException("Canonical project file path must be provided.", nameof(canonicalProjectFilePath));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(projectDisplayName)) {
                throw new ArgumentException("Project display name must be provided.", nameof(projectDisplayName));
            }
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (availablePlatforms == null) {
                throw new ArgumentNullException(nameof(availablePlatforms));
            }
            if (availablePlatformProviderResolver == null) {
                throw new ArgumentNullException(nameof(availablePlatformProviderResolver));
            }
            if (platformCatalogService == null) {
                throw new ArgumentNullException(nameof(platformCatalogService));
            }
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (buildConfigService == null) {
                throw new ArgumentNullException(nameof(buildConfigService));
            }
            if (profileSettingsService == null) {
                throw new ArgumentNullException(nameof(profileSettingsService));
            }

            CanonicalProjectFilePath = Path.GetFullPath(canonicalProjectFilePath);
            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectDisplayName = projectDisplayName;
            ProjectDocument = projectDocument;
            SupportedPlatforms = supportedPlatforms;
            AvailablePlatforms = availablePlatforms;
            AvailablePlatformProviderResolver = availablePlatformProviderResolver;
            PlatformCatalogService = platformCatalogService;
            SceneCatalogService = sceneCatalogService;
            BuildConfigService = buildConfigService;
            ProfileSettingsService = profileSettingsService;
        }

        /// <summary>
        /// Gets the absolute canonical project file path.
        /// </summary>
        public string CanonicalProjectFilePath { get; }

        /// <summary>
        /// Gets the absolute project root path.
        /// </summary>
        public string ProjectRootPath { get; }

        /// <summary>
        /// Gets the project file name shown by host UI.
        /// </summary>
        public string ProjectDisplayName { get; }

        /// <summary>
        /// Gets the parsed canonical project document.
        /// </summary>
        public ProjectFileDocument ProjectDocument { get; }

        /// <summary>
        /// Gets the exact engine version required by the current project.
        /// </summary>
        public string RequiredEngineVersion => ProjectDocument.RequiredEngineVersion;

        /// <summary>
        /// Gets the stable project name loaded from the canonical project document.
        /// </summary>
        public string ProjectName => ProjectDocument.Name;

        /// <summary>
        /// Gets the human-visible project version loaded from the canonical project document.
        /// </summary>
        public string ProjectVersion => ProjectDocument.Version;

        /// <summary>
        /// Gets the supported platform identifiers declared by the project.
        /// </summary>
        public IReadOnlyList<string> SupportedPlatforms { get; }

        /// <summary>
        /// Gets the installed platform catalog resolved for the current engine version.
        /// </summary>
        public IReadOnlyList<AvailablePlatformDescriptor> AvailablePlatforms { get; }

        /// <summary>
        /// Gets the resolver that discovered the installed platform catalog.
        /// </summary>
        public AvailablePlatformProviderResolver AvailablePlatformProviderResolver { get; }

        /// <summary>
        /// Gets the dynamic platform builder catalog service.
        /// </summary>
        public EditorPlatformCatalogService PlatformCatalogService { get; }

        /// <summary>
        /// Gets the project scene catalog service.
        /// </summary>
        public EditorProjectSceneCatalogService SceneCatalogService { get; }

        /// <summary>
        /// Gets the local build configuration service.
        /// </summary>
        public EditorBuildConfigService BuildConfigService { get; }

        /// <summary>
        /// Gets the local profile settings service.
        /// </summary>
        public EditorProfileSettingsService ProfileSettingsService { get; }

        /// <summary>
        /// Resolves the installed platform descriptor for the supplied platform id.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <returns>Matching installed platform descriptor.</returns>
        public AvailablePlatformDescriptor ResolvePlatformDescriptor(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            for (int index = 0; index < AvailablePlatforms.Count; index++) {
                AvailablePlatformDescriptor platform = AvailablePlatforms[index];
                if (platform != null && string.Equals(platform.Id, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platform;
                }
            }

            throw new InvalidOperationException($"No installed platform descriptor is registered for '{platformId}'.");
        }

        /// <summary>
        /// Resolves one dynamic builder metadata selection model for the supplied platform id.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <returns>Loaded platform selection model.</returns>
        public EditorPlatformBuildSelectionModel ResolveSelectionModel(string platformId) {
            return PlatformCatalogService.ResolveSelectionModel(platformId);
        }
    }

    /// <summary>
    /// Builds the shared project and platform bootstrap context used by editor workflows.
    /// </summary>
    public static class EditorProjectBootstrapper {
        /// <summary>
        /// Creates one shared bootstrap context for the supplied project path.
        /// </summary>
        /// <param name="projectPath">Project directory or canonical project file path.</param>
        /// <returns>Resolved bootstrap context.</returns>
        public static EditorProjectBootstrapContext Create(string projectPath) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            ProjectFilePathResolver projectFilePathResolver = new ProjectFilePathResolver();
            string canonicalProjectFilePath = projectFilePathResolver.Resolve(projectPath);
            string projectRootPath = Path.GetDirectoryName(canonicalProjectFilePath);
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidOperationException("Project file path does not include a directory.");
            }

            ProjectFileDocument projectDocument = LoadProjectDocument(canonicalProjectFilePath);
            string projectDisplayName = Path.GetFileName(canonicalProjectFilePath);
            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(projectRootPath);
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(projectRootPath);
            EditorProfileSettingsService profileSettingsService = new EditorProfileSettingsService(projectRootPath);
            AvailablePlatformProviderResolver availablePlatformProviderResolver = CreateAvailablePlatformProviderResolver();
            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms = availablePlatformProviderResolver.LoadPlatforms(projectDocument.RequiredEngineVersion);
            EditorPlatformCatalogService platformCatalogService = new EditorPlatformCatalogService(availablePlatforms);

            return new EditorProjectBootstrapContext(
                canonicalProjectFilePath,
                projectRootPath,
                projectDisplayName,
                projectDocument,
                projectDocument.SupportedPlatforms,
                availablePlatforms,
                availablePlatformProviderResolver,
                platformCatalogService,
                sceneCatalogService,
                buildConfigService,
                profileSettingsService);
        }

        /// <summary>
        /// Loads and validates one canonical project document from disk.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Absolute canonical project file path.</param>
        /// <returns>Validated project document.</returns>
        static ProjectFileDocument LoadProjectDocument(string canonicalProjectFilePath) {
            ProjectFileReader projectFileReader = new ProjectFileReader();
            ProjectFileReadResult readResult = projectFileReader.ReadAsync(canonicalProjectFilePath).GetAwaiter().GetResult();
            if (!readResult.Succeeded) {
                StringBuilder messageBuilder = new StringBuilder();
                for (int index = 0; index < readResult.Errors.Count; index++) {
                    ProjectFileReadError error = readResult.Errors[index];
                    if (index > 0) {
                        messageBuilder.AppendLine();
                    }

                    messageBuilder.Append(error.Message);
                }

                throw new InvalidOperationException(messageBuilder.ToString());
            }

            if (readResult.Document == null) {
                throw new InvalidOperationException($"Project file '{canonicalProjectFilePath}' did not produce a valid project document.");
            }

            return readResult.Document;
        }

        /// <summary>
        /// Creates the available-platform resolver used by the editor host.
        /// </summary>
        /// <returns>Resolver that loads platforms from development overrides, launcher state, or built-in fallback sources.</returns>
        static AvailablePlatformProviderResolver CreateAvailablePlatformProviderResolver() {
            EditorSourceBuildWorkspaceLocator workspaceLocator = new EditorSourceBuildWorkspaceLocator();
            string helEngineRootPath = workspaceLocator.ResolveHelEngineRootPath();
            PlatformDiscoveryOptions options = new PlatformDiscoveryOptions(Path.Combine(helEngineRootPath, "user_settings"));
            WindowsLauncherInstallRootLocator launcherInstallRootLocator = new WindowsLauncherInstallRootLocator();
            return new AvailablePlatformProviderResolver(options, launcherInstallRootLocator);
        }
    }
}
