using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Targets;
using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Executes one queued build item through a dynamically loaded platform builder assembly.
    /// </summary>
    public sealed class EditorPlatformBuildExecutor : IEditorBuildExecutor {
        /// <summary>
        /// Working-root folder name used by the platform content staging step.
        /// </summary>
        const string StagingRootFolderName = "staging";

        /// <summary>
        /// Working-root folder name used by the platform builder execution.
        /// </summary>
        const string BuilderWorkingFolderName = "builder";

        /// <summary>
        /// Absolute source project root path.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Exact engine version required by the current project build.
        /// </summary>
        readonly string RequiredEngineVersion;

        /// <summary>
        /// Stable project identifier reported to the builder.
        /// </summary>
        readonly string ProjectId;

        /// <summary>
        /// Human-visible project version reported to the builder.
        /// </summary>
        readonly string ProjectVersion;

        /// <summary>
        /// Importer registrations supplied by the editor host.
        /// </summary>
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;

        /// <summary>
        /// Platform descriptor loaded from the editor platform catalog.
        /// </summary>
        readonly AvailablePlatformDescriptor PlatformDescriptor;

        /// <summary>
        /// Loads platform builders from their resolved assembly path.
        /// </summary>
        readonly EditorPlatformAssetBuilderLoader BuilderLoader;

        /// <summary>
        /// Initializes one platform build executor for the supplied platform descriptor.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative source project root path.</param>
        /// <param name="requiredEngineVersion">Exact engine version required by the current project build.</param>
        /// <param name="projectId">Stable project identifier reported to the builder.</param>
        /// <param name="projectVersion">Human-visible project version reported to the builder.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="platformDescriptor">Loaded platform descriptor that carries the builder assembly path.</param>
        public EditorPlatformBuildExecutor(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            AvailablePlatformDescriptor platformDescriptor) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(requiredEngineVersion)) {
                throw new ArgumentException("Required engine version must be provided.", nameof(requiredEngineVersion));
            }
            if (string.IsNullOrWhiteSpace(projectId)) {
                throw new ArgumentException("Project id must be provided.", nameof(projectId));
            }
            if (string.IsNullOrWhiteSpace(projectVersion)) {
                throw new ArgumentException("Project version must be provided.", nameof(projectVersion));
            }
            if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            }
            if (platformDescriptor == null) {
                throw new ArgumentNullException(nameof(platformDescriptor));
            }
            if (string.IsNullOrWhiteSpace(platformDescriptor.BuilderAssemblyPath)) {
                throw new ArgumentException("Platform descriptor must provide a builder assembly path.", nameof(platformDescriptor));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            RequiredEngineVersion = requiredEngineVersion;
            ProjectId = projectId;
            ProjectVersion = projectVersion;
            Importers = importers;
            PlatformDescriptor = platformDescriptor;
            BuilderLoader = new EditorPlatformAssetBuilderLoader();
        }

        /// <summary>
        /// Executes one queued build item through the loaded platform builder assembly.
        /// </summary>
        /// <param name="queueItem">Queued build item that should be executed.</param>
        /// <returns>Structured execution result describing success or failure.</returns>
        public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            try {
                ValidateQueueItem(queueItem);

                IPlatformAssetBuilder builder = BuilderLoader.Load(PlatformDescriptor.BuilderAssemblyPath);
                string executionRoot = Path.Combine(Path.GetTempPath(), "helengine-platform-build", PlatformDescriptor.Id, queueItem.QueueItemId);
                string stagingRoot = Path.Combine(executionRoot, StagingRootFolderName);
                string builderWorkingRoot = Path.Combine(executionRoot, BuilderWorkingFolderName);
                ResetExecutionDirectories(executionRoot, stagingRoot, builderWorkingRoot, queueItem.OutputDirectoryPath);

                EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath, Importers, PlatformDescriptor.Id);
                packager.Package(queueItem.SelectedSceneIds, stagingRoot);

                PlatformBuildRequest request = BuildRequest(queueItem, stagingRoot, builderWorkingRoot, builder.Definition);
                EditorPlatformBuildProgressReporter progressReporter = new();
                EditorPlatformBuildDiagnosticCollector diagnosticCollector = new();

                string previousWorkingDirectory = Directory.GetCurrentDirectory();
                try {
                    Directory.SetCurrentDirectory(stagingRoot);
                    PlatformBuildReport report = builder.BuildAsync(request, progressReporter, diagnosticCollector, CancellationToken.None).GetAwaiter().GetResult();
                    if (!report.Succeeded) {
                        return EditorBuildExecutionResult.Failure(BuildFailureMessage(report));
                    }

                    return EditorBuildExecutionResult.Success($"Build completed for platform '{PlatformDescriptor.Id}': {queueItem.OutputDirectoryPath}");
                } finally {
                    Directory.SetCurrentDirectory(previousWorkingDirectory);
                }
            } catch (Exception ex) {
                return EditorBuildExecutionResult.Failure($"Build for platform '{PlatformDescriptor.Id}' failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates one queued build item before any filesystem or builder work starts.
        /// </summary>
        /// <param name="queueItem">Queued build item to validate.</param>
        void ValidateQueueItem(EditorBuildQueueItemDocument queueItem) {
            if (!string.Equals(queueItem.PlatformId, PlatformDescriptor.Id, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Platform build executor cannot handle platform '{queueItem.PlatformId}'.");
            }
            if (queueItem.SelectedSceneIds == null || queueItem.SelectedSceneIds.Count == 0) {
                throw new InvalidOperationException($"Platform '{PlatformDescriptor.Id}' requires at least one selected scene.");
            }
            if (string.IsNullOrWhiteSpace(queueItem.OutputDirectoryPath)) {
                throw new InvalidOperationException($"Platform '{PlatformDescriptor.Id}' requires an output directory.");
            }
        }

        /// <summary>
        /// Recreates the staging and working directories used by one platform build execution.
        /// </summary>
        /// <param name="executionRoot">Temporary execution root.</param>
        /// <param name="stagingRoot">Content staging root.</param>
        /// <param name="builderWorkingRoot">Builder working root.</param>
        /// <param name="outputRoot">Final build output root.</param>
        void ResetExecutionDirectories(string executionRoot, string stagingRoot, string builderWorkingRoot, string outputRoot) {
            DeleteDirectoryIfPresent(executionRoot);
            DeleteDirectoryIfPresent(outputRoot);

            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(builderWorkingRoot);
            Directory.CreateDirectory(outputRoot);
        }

        /// <summary>
        /// Builds one fully resolved builder request from the staged platform content root.
        /// </summary>
        /// <param name="queueItem">Queued build item being executed.</param>
        /// <param name="stagingRoot">Absolute staging root produced by the content packager.</param>
        /// <param name="builderWorkingRoot">Absolute temporary working directory for the builder.</param>
        /// <param name="builderDefinition">Typed metadata exposed by the loaded builder assembly.</param>
        /// <returns>Resolved builder request.</returns>
        PlatformBuildRequest BuildRequest(
            EditorBuildQueueItemDocument queueItem,
            string stagingRoot,
            string builderWorkingRoot,
            PlatformDefinition builderDefinition) {
            string[] stagedFilePaths = Directory.GetFiles(stagingRoot, "*", SearchOption.AllDirectories);
            Array.Sort(stagedFilePaths, StringComparer.OrdinalIgnoreCase);

            PlatformBuildPayloadReference[] stagedPayloadReferences = new PlatformBuildPayloadReference[stagedFilePaths.Length];
            for (int index = 0; index < stagedFilePaths.Length; index++) {
                string stagedFilePath = stagedFilePaths[index];
                string relativePath = NormalizeRelativePath(Path.GetRelativePath(stagingRoot, stagedFilePath));
                stagedPayloadReferences[index] = new PlatformBuildPayloadReference(relativePath, relativePath);
            }

            string selectedBuildProfileId = ResolveBuildProfileId(builderDefinition, queueItem.DebugBuild);
            string selectedGraphicsProfileId = ResolveGraphicsProfileId(builderDefinition, selectedBuildProfileId);

            PlatformBuildScene[] scenes = new PlatformBuildScene[queueItem.SelectedSceneIds.Count];
            for (int index = 0; index < queueItem.SelectedSceneIds.Count; index++) {
                string sceneId = queueItem.SelectedSceneIds[index];
                string sceneSourceIdentity = NormalizeRelativePath(Path.Combine("scenes", sceneId));
                PlatformBuildPayloadReference[] payloadReferences = index == 0 ? stagedPayloadReferences : [];

                scenes[index] = new PlatformBuildScene(
                    sceneId,
                    Path.GetFileNameWithoutExtension(sceneId),
                    sceneSourceIdentity,
                    payloadReferences,
                    []);
            }

            PlatformBuildManifest manifest = new(
                1,
                ProjectId,
                ProjectVersion,
                RequiredEngineVersion,
                scenes,
                []);

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

            return new PlatformBuildRequest(manifest, targetVariants, cookProfiles, queueItem.OutputDirectoryPath, builderWorkingRoot);
        }

        /// <summary>
        /// Chooses one build profile id from the builder metadata.
        /// </summary>
        /// <param name="definition">Typed builder metadata.</param>
        /// <param name="debugBuild">True when the queue item requested a debug build.</param>
        /// <returns>Selected build profile id.</returns>
        static string ResolveBuildProfileId(PlatformDefinition definition, bool debugBuild) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }
            if (definition.BuildProfiles.Length == 0) {
                return debugBuild ? "debug" : "release";
            }

            string desiredProfileId = debugBuild ? "debug" : "release";
            for (int index = 0; index < definition.BuildProfiles.Length; index++) {
                PlatformBuildProfileDefinition profile = definition.BuildProfiles[index];
                if (string.Equals(profile.ProfileId, desiredProfileId, StringComparison.OrdinalIgnoreCase)) {
                    return profile.ProfileId;
                }
            }

            return definition.BuildProfiles[0].ProfileId;
        }

        /// <summary>
        /// Chooses one graphics profile id from the builder metadata.
        /// </summary>
        /// <param name="definition">Typed builder metadata.</param>
        /// <param name="selectedBuildProfileId">Build profile id chosen for this queue item.</param>
        /// <returns>Selected graphics profile id.</returns>
        static string ResolveGraphicsProfileId(PlatformDefinition definition, string selectedBuildProfileId) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            for (int index = 0; index < definition.BuildProfiles.Length; index++) {
                PlatformBuildProfileDefinition profile = definition.BuildProfiles[index];
                if (!string.Equals(profile.ProfileId, selectedBuildProfileId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(profile.GraphicsProfileId)) {
                    return profile.GraphicsProfileId;
                }
            }

            if (definition.GraphicsProfiles.Length > 0) {
                return definition.GraphicsProfiles[0].ProfileId;
            }

            return selectedBuildProfileId;
        }

        /// <summary>
        /// Builds one failure message from a completed platform builder report.
        /// </summary>
        /// <param name="report">Builder report returned by the platform assembly.</param>
        /// <returns>User-facing failure message.</returns>
        static string BuildFailureMessage(PlatformBuildReport report) {
            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            for (int index = 0; index < report.Diagnostics.Length; index++) {
                PlatformBuildDiagnostic diagnostic = report.Diagnostics[index];
                if (diagnostic.Severity == PlatformBuildDiagnosticSeverity.Error) {
                    return $"Platform build failed: {diagnostic.Message}";
                }
            }

            return "Platform build reported a failed build.";
        }

        /// <summary>
        /// Deletes one directory if it already exists.
        /// </summary>
        /// <param name="directoryPath">Directory path to delete.</param>
        static void DeleteDirectoryIfPresent(string directoryPath) {
            if (Directory.Exists(directoryPath)) {
                Directory.Delete(directoryPath, recursive: true);
            }
        }

        /// <summary>
        /// Normalizes one relative path for cross-platform build request metadata.
        /// </summary>
        /// <param name="path">Path to normalize.</param>
        /// <returns>Normalized relative path.</returns>
        static string NormalizeRelativePath(string path) {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
