using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Executes one queued build item through the shared editor-owned platform build graph.
    /// </summary>
    public sealed class EditorPlatformBuildExecutor : IEditorBuildExecutor {
        /// <summary>
        /// Platform descriptor loaded from the editor platform catalog.
        /// </summary>
        readonly AvailablePlatformDescriptor PlatformDescriptor;

        /// <summary>
        /// Executes the shared platform build graph for this platform.
        /// </summary>
        readonly EditorPlatformBuildGraphRunner BuildGraphRunner;

        /// <summary>
        /// Initializes one platform build executor for the supplied platform descriptor.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative source project root path.</param>
        /// <param name="requiredEngineVersion">Exact engine version required by the current project build.</param>
        /// <param name="projectId">Stable project identifier reported to the builder.</param>
        /// <param name="projectVersion">Human-visible project version reported to the builder.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="platformDescriptor">Loaded platform descriptor that carries the builder assembly path.</param>
        /// <param name="defaultFontAsset">Default font asset packaged for player builds.</param>
        /// <param name="buildGraphRunner">Optional override used by tests.</param>
        public EditorPlatformBuildExecutor(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            AvailablePlatformDescriptor platformDescriptor,
            FontAsset defaultFontAsset = null,
            EditorPlatformBuildGraphRunner buildGraphRunner = null) {
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
            if (string.IsNullOrWhiteSpace(platformDescriptor.CodegenToolPath)) {
                throw new ArgumentException("Platform descriptor must provide a csharpcodegen tool path.", nameof(platformDescriptor));
            }

            PlatformDescriptor = platformDescriptor;
            BuildGraphRunner = buildGraphRunner ?? new EditorPlatformBuildGraphRunner(
                Path.GetFullPath(projectRootPath),
                requiredEngineVersion,
                projectId,
                projectVersion,
                importers,
                platformDescriptor,
                defaultFontAsset,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());
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
                return BuildGraphRunner.Execute(queueItem);
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
    }
}
