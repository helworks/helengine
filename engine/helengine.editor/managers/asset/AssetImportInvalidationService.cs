namespace helengine.editor {
    /// <summary>
    /// Tracks source invalidations and executes typed imports at an explicit owner boundary.
    /// </summary>
    public sealed class AssetImportInvalidationService : IDisposable {
        /// <summary>
        /// Execution service used when an invalidated source is consumed.
        /// </summary>
        readonly AssetImportExecutionService ExecutionService;

        /// <summary>
        /// Source paths waiting for an explicit import execution.
        /// </summary>
        readonly HashSet<string> InvalidatedSourcePaths;

        /// <summary>
        /// Indicates whether this service has been disposed.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>
        /// Creates an invalidation service for one execution pipeline.
        /// </summary>
        /// <param name="executionService">Execution service that owns import publication.</param>
        public AssetImportInvalidationService(AssetImportExecutionService executionService) {
            ExecutionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
            InvalidatedSourcePaths = new HashSet<string>(GetPathComparer());
        }

        /// <summary>
        /// Marks one source path for the next explicit typed import operation.
        /// </summary>
        /// <param name="sourcePath">Source path whose cached result is stale.</param>
        public void InvalidateSource(string sourcePath) {
            EnsureNotDisposed();
            InvalidatedSourcePaths.Add(NormalizeSourcePath(sourcePath));
        }

        /// <summary>
        /// Checks whether a source path is waiting for execution.
        /// </summary>
        /// <param name="sourcePath">Source path to inspect.</param>
        /// <returns>True when the source is marked invalid.</returns>
        public bool IsInvalidated(string sourcePath) {
            EnsureNotDisposed();
            return InvalidatedSourcePaths.Contains(NormalizeSourcePath(sourcePath));
        }

        /// <summary>
        /// Executes an invalidated texture import and clears its pending marker.
        /// </summary>
        /// <param name="sourcePath">Texture source path.</param>
        /// <param name="asset">Imported texture when a marker was consumed.</param>
        /// <returns>True when an import was executed.</returns>
        public bool TryExecuteTexture(string sourcePath, out TextureAsset asset) {
            if (!TryConsume(sourcePath)) {
                asset = null;
                return false;
            }
            asset = ExecutionService.ImportTexture(sourcePath);
            return true;
        }

        /// <summary>
        /// Executes an invalidated text import and clears its pending marker.
        /// </summary>
        /// <param name="sourcePath">Text source path.</param>
        /// <param name="asset">Imported text when a marker was consumed.</param>
        /// <returns>True when an import was executed.</returns>
        public bool TryExecuteText(string sourcePath, out TextAsset asset) {
            if (!TryConsume(sourcePath)) {
                asset = null;
                return false;
            }
            asset = ExecutionService.ImportText(sourcePath);
            return true;
        }

        /// <summary>
        /// Executes an invalidated font import and clears its pending marker.
        /// </summary>
        /// <param name="sourcePath">Font source path.</param>
        /// <param name="asset">Imported font when a marker was consumed.</param>
        /// <returns>True when an import was executed.</returns>
        public bool TryExecuteFont(string sourcePath, out FontAsset asset) {
            if (!TryConsume(sourcePath)) {
                asset = null;
                return false;
            }
            asset = ExecutionService.ImportFont(sourcePath);
            return true;
        }

        /// <summary>
        /// Executes an invalidated audio import and clears its pending marker.
        /// </summary>
        /// <param name="sourcePath">Audio source path.</param>
        /// <param name="asset">Imported audio when a marker was consumed.</param>
        /// <returns>True when an import was executed.</returns>
        public bool TryExecuteAudio(string sourcePath, out AudioAsset asset) {
            if (!TryConsume(sourcePath)) {
                asset = null;
                return false;
            }
            asset = ExecutionService.ImportAudio(sourcePath);
            return true;
        }

        /// <summary>
        /// Executes an invalidated model import and clears its pending marker.
        /// </summary>
        /// <param name="sourcePath">Model source path.</param>
        /// <param name="asset">Imported model when a marker was consumed.</param>
        /// <returns>True when an import was executed.</returns>
        public bool TryExecuteModel(string sourcePath, out ModelAsset asset) {
            if (!TryConsume(sourcePath)) {
                asset = null;
                return false;
            }
            asset = ExecutionService.ImportModel(sourcePath);
            return true;
        }

        /// <summary>
        /// Clears pending invalidations and prevents further scheduling.
        /// </summary>
        public void Dispose() {
            if (IsDisposedValue) {
                return;
            }
            InvalidatedSourcePaths.Clear();
            IsDisposedValue = true;
        }

        /// <summary>
        /// Consumes one pending marker without executing its typed import.
        /// </summary>
        /// <param name="sourcePath">Source path to consume.</param>
        /// <returns>True when a pending marker existed.</returns>
        bool TryConsume(string sourcePath) {
            EnsureNotDisposed();
            return InvalidatedSourcePaths.Remove(NormalizeSourcePath(sourcePath));
        }

        /// <summary>
        /// Normalizes source paths consistently with the manager cache identity boundary.
        /// </summary>
        /// <param name="sourcePath">Source path to normalize.</param>
        /// <returns>Full source path.</returns>
        static string NormalizeSourcePath(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            return Path.GetFullPath(sourcePath);
        }

        /// <summary>
        /// Gets the path comparer for the current host platform.
        /// </summary>
        /// <returns>Operating-system path comparer.</returns>
        static StringComparer GetPathComparer() {
            return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        }

        /// <summary>
        /// Rejects operations after disposal.
        /// </summary>
        void EnsureNotDisposed() {
            if (IsDisposedValue) {
                throw new ObjectDisposedException(nameof(AssetImportInvalidationService));
            }
        }
    }
}