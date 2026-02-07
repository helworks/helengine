using System.Timers;
using Timer = System.Timers.Timer;

namespace helengine.editor {
    /// <summary>
    /// Watches shader source files, builds shader packages, and hot-loads shader metadata in the editor.
    /// </summary>
    public class ShaderModuleManager : IDisposable {
        /// <summary>
        /// Shader file extensions recognized by the manager.
        /// </summary>
        static readonly string[] ShaderExtensions = new[] { ".hlsl" };
        /// <summary>
        /// Interval in milliseconds between active shader validation checks.
        /// </summary>
        const int ActiveShaderCheckIntervalMilliseconds = 1000;

        /// <summary>
        /// Options controlling shader module compilation.
        /// </summary>
        readonly ShaderModuleManagerOptions options;

        /// <summary>
        /// Loader used to load compiled shader packages.
        /// </summary>
        readonly ShaderPackageLoader loader;

        /// <summary>
        /// Tracks loaded shader module handles by shader name.
        /// </summary>
        readonly Dictionary<string, ShaderPackageHandle> loadedModules;

        /// <summary>
        /// Tracks queued build timestamps for shader names.
        /// </summary>
        readonly Dictionary<string, DateTime> pendingBuilds;

        /// <summary>
        /// Tracks shader entries by absolute source path.
        /// </summary>
        readonly Dictionary<string, ShaderSourceEntry> entriesBySourcePath;

        /// <summary>
        /// Tracks shader entries by logical shader name.
        /// </summary>
        readonly Dictionary<string, ShaderSourceEntry> entriesByName;

        /// <summary>
        /// Synchronization object for build queue access.
        /// </summary>
        readonly object pendingLock;

        /// <summary>
        /// Synchronization object for shader entry access.
        /// </summary>
        readonly object entriesLock;
        /// <summary>
        /// Tracks shaders that are actively referenced by runtime materials.
        /// </summary>
        readonly Dictionary<string, DateTime> ActiveShaderChecks;
        /// <summary>
        /// Synchronization object for active shader tracking.
        /// </summary>
        readonly object ActiveLock;
        /// <summary>
        /// Hashes shader source files to detect content changes.
        /// </summary>
        readonly AssetFileHasher SourceHasher;
        /// <summary>
        /// Reads and writes cached shader metadata.
        /// </summary>
        readonly ShaderCacheMetadataStore MetadataStore;
        /// <summary>
        /// Synchronization object for metadata access.
        /// </summary>
        readonly object MetadataLock;

        /// <summary>
        /// Semaphore used to avoid concurrent build execution.
        /// </summary>
        readonly SemaphoreSlim buildSemaphore;

        /// <summary>
        /// Folder watcher used to monitor shader source changes.
        /// </summary>
        FolderWatcher watcher;

        /// <summary>
        /// Timer that processes queued build requests.
        /// </summary>
        Timer buildTimer;

        /// <summary>
        /// Package builder used to compile shader packages.
        /// </summary>
        ShaderPackageBuilder packageBuilder;

        /// <summary>
        /// Tracks whether the manager has been started.
        /// </summary>
        bool started;

        /// <summary>
        /// Tracks whether the manager has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Raised when a shader package is rebuilt successfully.
        /// </summary>
        public event Action<string, string> ShaderBuilt;

        /// <summary>
        /// Initializes a new shader module manager.
        /// </summary>
        /// <param name="options">Manager options.</param>
        public ShaderModuleManager(ShaderModuleManagerOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            this.options = options;
            loader = new ShaderPackageLoader(new ShaderModulePackageReader());
            loadedModules = new Dictionary<string, ShaderPackageHandle>(StringComparer.OrdinalIgnoreCase);
            pendingBuilds = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            entriesBySourcePath = new Dictionary<string, ShaderSourceEntry>(StringComparer.OrdinalIgnoreCase);
            entriesByName = new Dictionary<string, ShaderSourceEntry>(StringComparer.OrdinalIgnoreCase);
            pendingLock = new object();
            entriesLock = new object();
            ActiveShaderChecks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            ActiveLock = new object();
            SourceHasher = new AssetFileHasher();
            MetadataStore = new ShaderCacheMetadataStore(options.PackageOutputPath, options.RuntimeTarget);
            MetadataLock = new object();
            buildSemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Starts shader file monitoring and triggers initial compilation.
        /// </summary>
        public void Start() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(ShaderModuleManager));
            }

            if (started) {
                throw new InvalidOperationException("Shader module manager has already been started.");
            }

            EnsureDirectories();
            LoadSourceEntries();
            InitializePackageBuilder();
            StartWatcher();
            StartTimer();
            QueueInitialBuilds();
            started = true;
        }

        /// <summary>
        /// Ensures a shader package is compiled and loaded for the specified shader name.
        /// </summary>
        /// <param name="shaderName">Shader name to validate and compile.</param>
        /// <returns>True when the shader package is available.</returns>
        public bool EnsureShaderCompiled(string shaderName) {
            if (disposed) {
                throw new ObjectDisposedException(nameof(ShaderModuleManager));
            }

            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            ShaderSourceEntry entry;
            if (!TryGetEntryByName(shaderName, out entry)) {
                return false;
            }

            buildSemaphore.Wait();
            try {
                if (TryLoadCachedModule(entry, true)) {
                    return true;
                }

                BuildShader(entry.Name);
                string packagePath = GetRuntimePackagePath(entry.Name);
                return File.Exists(packagePath);
            } catch (Exception ex) {
                Logger.WriteError($"Shader ensure failed for '{shaderName}': {ex.Message}");
                return false;
            } finally {
                buildSemaphore.Release();
            }
        }

        /// <summary>
        /// Records that a shader is actively used so it can be validated without relying on file system events.
        /// </summary>
        /// <param name="shaderName">Shader name to track.</param>
        public void TrackShaderUsage(string shaderName) {
            if (disposed) {
                throw new ObjectDisposedException(nameof(ShaderModuleManager));
            }

            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            lock (ActiveLock) {
                ActiveShaderChecks[shaderName] = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Stops monitoring and unloads shader modules.
        /// </summary>
        public void Stop() {
            if (disposed) {
                return;
            }

            if (watcher != null) {
                watcher.Dispose();
                watcher = null;
            }

            if (buildTimer != null) {
                buildTimer.Elapsed -= OnBuildTimerElapsed;
                buildTimer.Stop();
                buildTimer.Dispose();
                buildTimer = null;
            }

            UnloadAllModules();
            started = false;
        }

        /// <inheritdoc />
        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            Stop();
            buildSemaphore.Dispose();
        }

        /// <summary>
        /// Ensures the shader source and output directories exist.
        /// </summary>
        void EnsureDirectories() {
            Directory.CreateDirectory(options.ShaderRootPath);
            Directory.CreateDirectory(options.PackageOutputPath);
        }

        /// <summary>
        /// Loads the initial shader source catalog.
        /// </summary>
        void LoadSourceEntries() {
            string[] files = EnumerateShaderFiles();
            lock (entriesLock) {
                entriesBySourcePath.Clear();
                entriesByName.Clear();
                for (int i = 0; i < files.Length; i++) {
                    AddEntry(files[i]);
                }
            }
        }

        /// <summary>
        /// Starts the folder watcher for shader source files.
        /// </summary>
        void StartWatcher() {
            watcher = new FolderWatcher(
                options.ShaderRootPath,
                HandleSourceEvent,
                true,
                NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                "*.*"
            );
            watcher.Start();
        }

        /// <summary>
        /// Starts the build debounce timer.
        /// </summary>
        void StartTimer() {
            buildTimer = new Timer(options.BuildDelayMilliseconds);
            buildTimer.AutoReset = true;
            buildTimer.Elapsed += OnBuildTimerElapsed;
            buildTimer.Start();
        }

        /// <summary>
        /// Initializes the shader package builder for the current shader root.
        /// </summary>
        void InitializePackageBuilder() {
            ShaderCompileService compileService = BuildCompileService();
            packageBuilder = new ShaderPackageBuilder(compileService, new ShaderModulePackageWriter(), options.BuildOptions);
        }

        /// <summary>
        /// Queues initial builds for all discovered shaders.
        /// </summary>
        void QueueInitialBuilds() {
            ShaderSourceEntry[] entries = GetEntriesSnapshot();
            for (int i = 0; i < entries.Length; i++) {
                ShaderSourceEntry entry = entries[i];
                if (TryLoadCachedModule(entry, false)) {
                    continue;
                }

                QueueBuild(entry);
            }
        }

        /// <summary>
        /// Routes folder watcher events to the appropriate shader handlers.
        /// </summary>
        /// <param name="change">Folder change event data.</param>
        void HandleSourceEvent(FolderWatchEvent change) {
            if (change == null) {
                throw new ArgumentNullException(nameof(change));
            }

            switch (change.Kind) {
                case FolderWatchEventKind.Created:
                case FolderWatchEventKind.Changed:
                    OnSourceChanged(change);
                    break;
                case FolderWatchEventKind.Deleted:
                    OnSourceDeleted(change);
                    break;
                case FolderWatchEventKind.Renamed:
                    OnSourceRenamed(change);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Handles file creation or update events from the watcher.
        /// </summary>
        /// <param name="change">Folder change event data.</param>
        void OnSourceChanged(FolderWatchEvent change) {
            if (disposed) {
                return;
            }

            if (!IsShaderFile(change.FullPath)) {
                return;
            }

            ShaderSourceEntry entry = GetOrAddEntry(change.FullPath);
            if (entry == null) {
                return;
            }

            if (ShouldQueueBuild(entry)) {
                QueueBuild(entry);
            }
        }

        /// <summary>
        /// Handles file delete events from the watcher.
        /// </summary>
        /// <param name="change">Folder change event data.</param>
        void OnSourceDeleted(FolderWatchEvent change) {
            if (disposed) {
                return;
            }

            if (!IsShaderFile(change.FullPath)) {
                return;
            }

            ShaderSourceEntry entry;
            if (RemoveEntry(change.FullPath, out entry)) {
                UnloadModule(entry.Name);
                DeleteShaderMetadata(entry.Name);
            }
        }

        /// <summary>
        /// Handles file rename events from the watcher.
        /// </summary>
        /// <param name="change">Folder change event data.</param>
        void OnSourceRenamed(FolderWatchEvent change) {
            if (disposed) {
                return;
            }

            if (change == null) {
                throw new ArgumentNullException(nameof(change));
            }

            if (!change.HasOldFullPath) {
                throw new ArgumentException("Rename event did not include an old path.", nameof(change));
            }

            if (IsShaderFile(change.OldFullPath)) {
                ShaderSourceEntry oldEntry;
                if (RemoveEntry(change.OldFullPath, out oldEntry)) {
                    UnloadModule(oldEntry.Name);
                    DeleteShaderMetadata(oldEntry.Name);
                }
            }

            if (IsShaderFile(change.FullPath)) {
                ShaderSourceEntry newEntry = GetOrAddEntry(change.FullPath);
                if (newEntry != null) {
                    if (ShouldQueueBuild(newEntry)) {
                        QueueBuild(newEntry);
                    }
                }
            }
        }

        /// <summary>
        /// Processes queued shader builds on a timer tick.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Timer event data.</param>
        void OnBuildTimerElapsed(object sender, ElapsedEventArgs e) {
            if (disposed) {
                return;
            }

            if (!buildSemaphore.Wait(0)) {
                return;
            }

            try {
                DateTime now = DateTime.UtcNow;
                List<string> readyShaders = new List<string>();

                lock (pendingLock) {
                    foreach (var pair in pendingBuilds) {
                        if ((now - pair.Value).TotalMilliseconds >= options.BuildDelayMilliseconds) {
                            readyShaders.Add(pair.Key);
                        }
                    }

                    for (int i = 0; i < readyShaders.Count; i++) {
                        pendingBuilds.Remove(readyShaders[i]);
                    }
                }

                for (int i = 0; i < readyShaders.Count; i++) {
                    TryBuildShader(readyShaders[i]);
                }

                ValidateActiveShaders(now);
            } finally {
                buildSemaphore.Release();
            }
        }

        /// <summary>
        /// Queues a shader build for the specified entry.
        /// </summary>
        /// <param name="entry">Shader source entry to build.</param>
        void QueueBuild(ShaderSourceEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (pendingLock) {
                pendingBuilds[entry.Name] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Queues a shader build when one is not already pending.
        /// </summary>
        /// <param name="entry">Shader source entry to build.</param>
        void QueueBuildIfNotPending(ShaderSourceEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (pendingLock) {
                if (pendingBuilds.ContainsKey(entry.Name)) {
                    return;
                }

                pendingBuilds[entry.Name] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Determines whether a shader should be queued for rebuild based on its cached hash.
        /// </summary>
        /// <param name="entry">Shader entry to evaluate.</param>
        /// <returns>True when the shader should be rebuilt.</returns>
        bool ShouldQueueBuild(ShaderSourceEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!File.Exists(entry.SourcePath)) {
                return false;
            }

            string packagePath = GetRuntimePackagePath(entry.Name);
            ShaderCacheMetadata metadata;
            if (!TryLoadShaderMetadata(entry.Name, out metadata)) {
                return true;
            }

            string sourceHash;
            long writeTicks;
            long length;
            if (!TryComputeSourceHash(entry.SourcePath, out sourceHash, out writeTicks, out length)) {
                return true;
            }

            if (!IsHashMatch(sourceHash, metadata.SourceHash)) {
                return true;
            }

            bool metadataUpdated = false;
            if (metadata.SourceWriteTimeUtcTicks != writeTicks) {
                metadata.SourceWriteTimeUtcTicks = writeTicks;
                metadataUpdated = true;
            }

            if (metadata.SourceLengthBytes != length) {
                metadata.SourceLengthBytes = length;
                metadataUpdated = true;
            }

            if (metadataUpdated) {
                SaveShaderMetadata(entry.Name, metadata);
            }

            return !File.Exists(packagePath);
        }

        /// <summary>
        /// Attempts to load a cached shader module if the source hash is up to date.
        /// </summary>
        /// <param name="entry">Shader entry to load.</param>
        /// <param name="forceHashCheck">True to validate the source hash even when timestamps match.</param>
        /// <returns>True when a cached module is loaded.</returns>
        bool TryLoadCachedModule(ShaderSourceEntry entry, bool forceHashCheck) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            string packagePath = GetRuntimePackagePath(entry.Name);
            if (!File.Exists(packagePath)) {
                return false;
            }

            ShaderCacheMetadata metadata;
            if (!TryLoadShaderMetadata(entry.Name, out metadata)) {
                return false;
            }

            long writeTicks;
            long length;
            if (!TryGetSourceInfo(entry.SourcePath, out writeTicks, out length)) {
                return false;
            }

            bool needsHashCheck = forceHashCheck
                || metadata.SourceWriteTimeUtcTicks != writeTicks
                || metadata.SourceLengthBytes != length;
            if (needsHashCheck) {
                string sourceHash;
                long resolvedWriteTicks;
                long resolvedLength;
                if (!TryComputeSourceHash(entry.SourcePath, out sourceHash, out resolvedWriteTicks, out resolvedLength)) {
                    return false;
                }

                if (!IsHashMatch(sourceHash, metadata.SourceHash)) {
                    return false;
                }

                bool metadataUpdated = false;
                if (metadata.SourceWriteTimeUtcTicks != resolvedWriteTicks) {
                    metadata.SourceWriteTimeUtcTicks = resolvedWriteTicks;
                    metadataUpdated = true;
                }

                if (metadata.SourceLengthBytes != resolvedLength) {
                    metadata.SourceLengthBytes = resolvedLength;
                    metadataUpdated = true;
                }

                if (metadataUpdated) {
                    SaveShaderMetadata(entry.Name, metadata);
                }
            }

            return TryLoadPackage(entry.Name, packagePath);
        }

        /// <summary>
        /// Executes shader compilation for a specific shader and loads the resulting package.
        /// </summary>
        /// <param name="shaderName">Shader name to build.</param>
        void BuildShader(string shaderName) {
            ShaderSourceEntry entry;
            if (!TryGetEntryByName(shaderName, out entry)) {
                return;
            }

            ShaderPackageBuildResult buildResult = BuildShaderPackages(entry);
            if (!buildResult.Success) {
                Logger.WriteError($"Shader build failed for '{shaderName}': {buildResult.ErrorMessage}");
                LogDiagnostics(buildResult.Results);
                return;
            }

            if (!File.Exists(buildResult.PackagePath)) {
                Logger.WriteError($"Shader package was not produced: {buildResult.PackagePath}");
                return;
            }

            ShaderPackageHandle handle = loader.Load(buildResult.PackagePath);
            ReplaceModule(entry.Name, handle);
            UpdateShaderMetadata(entry);
            Logger.WriteLine($"Shader build succeeded for '{shaderName}'.");
            ShaderBuilt?.Invoke(shaderName, buildResult.PackagePath);
        }

        /// <summary>
        /// Attempts to build a shader module and logs errors.
        /// </summary>
        /// <param name="shaderName">Shader name to build.</param>
        void TryBuildShader(string shaderName) {
            try {
                BuildShader(shaderName);
            } catch (Exception ex) {
                Logger.WriteError($"Shader build failed for '{shaderName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Builds shader packages for the specified shader entry.
        /// </summary>
        /// <param name="entry">Shader entry to build.</param>
        /// <returns>Build result for the runtime target.</returns>
        ShaderPackageBuildResult BuildShaderPackages(ShaderSourceEntry entry) {
            if (packageBuilder == null) {
                throw new InvalidOperationException("Shader package builder has not been initialized.");
            }

            ShaderPackageBuildResult[] results = packageBuilder.BuildPackages(entry, options.PackageOutputPath);
            return SelectRuntimeResult(results);
        }

        /// <summary>
        /// Gets the runtime package path for a shader name.
        /// </summary>
        /// <param name="shaderName">Shader name to resolve.</param>
        /// <returns>Absolute package path for the runtime target.</returns>
        string GetRuntimePackagePath(string shaderName) {
            return ShaderPackagePaths.GetPackagePath(options.PackageOutputPath, shaderName, options.RuntimeTarget);
        }

        /// <summary>
        /// Attempts to load a shader package and replace the active module.
        /// </summary>
        /// <param name="shaderName">Shader name to load.</param>
        /// <param name="packagePath">Package path to load.</param>
        /// <returns>True when the package is loaded successfully.</returns>
        bool TryLoadPackage(string shaderName, string packagePath) {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath)) {
                return false;
            }

            try {
                ShaderPackageHandle handle = loader.Load(packagePath);
                ReplaceModule(shaderName, handle);
                return true;
            } catch (Exception ex) {
                Logger.WriteError($"Shader package load failed for '{shaderName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates cached metadata for a shader entry after a successful build.
        /// </summary>
        /// <param name="entry">Shader entry that was built.</param>
        void UpdateShaderMetadata(ShaderSourceEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            ShaderCacheMetadata metadata;
            if (!TryCreateShaderMetadata(entry.SourcePath, out metadata)) {
                return;
            }

            SaveShaderMetadata(entry.Name, metadata);
        }

        /// <summary>
        /// Attempts to create shader metadata for the specified source path.
        /// </summary>
        /// <param name="sourcePath">Shader source path.</param>
        /// <param name="metadata">Created metadata instance.</param>
        /// <returns>True when metadata was created.</returns>
        bool TryCreateShaderMetadata(string sourcePath, out ShaderCacheMetadata metadata) {
            string sourceHash;
            long writeTicks;
            long length;
            if (!TryComputeSourceHash(sourcePath, out sourceHash, out writeTicks, out length)) {
                metadata = null;
                return false;
            }

            metadata = new ShaderCacheMetadata {
                SourceHash = sourceHash,
                SourceWriteTimeUtcTicks = writeTicks,
                SourceLengthBytes = length
            };
            return true;
        }

        /// <summary>
        /// Attempts to compute a source hash and write time for a shader file.
        /// </summary>
        /// <param name="sourcePath">Shader source path.</param>
        /// <param name="sourceHash">Computed source hash.</param>
        /// <param name="writeTicks">Last write time in UTC ticks.</param>
        /// <param name="length">Source length in bytes.</param>
        /// <returns>True when the hash and write time were computed.</returns>
        bool TryComputeSourceHash(string sourcePath, out string sourceHash, out long writeTicks, out long length) {
            sourceHash = string.Empty;
            writeTicks = 0;
            length = 0;

            if (!TryGetSourceInfo(sourcePath, out writeTicks, out length)) {
                return false;
            }

            try {
                sourceHash = SourceHasher.ComputeHash(sourcePath);
                return true;
            } catch (Exception ex) {
                Logger.WriteError($"Shader hash compute failed for '{sourcePath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to read the last write time and length for a shader source file.
        /// </summary>
        /// <param name="sourcePath">Shader source path.</param>
        /// <param name="writeTicks">Resolved write time in UTC ticks.</param>
        /// <param name="length">Resolved source length in bytes.</param>
        /// <returns>True when the source info was resolved.</returns>
        bool TryGetSourceInfo(string sourcePath, out long writeTicks, out long length) {
            writeTicks = 0;
            length = 0;
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                return false;
            }

            FileInfo info = new FileInfo(sourcePath);
            if (!info.Exists) {
                return false;
            }

            writeTicks = info.LastWriteTimeUtc.Ticks;
            length = info.Length;
            return true;
        }

        /// <summary>
        /// Attempts to load cached shader metadata.
        /// </summary>
        /// <param name="shaderName">Shader name to resolve.</param>
        /// <param name="metadata">Loaded metadata instance.</param>
        /// <returns>True when metadata was loaded.</returns>
        bool TryLoadShaderMetadata(string shaderName, out ShaderCacheMetadata metadata) {
            lock (MetadataLock) {
                try {
                    return MetadataStore.TryLoad(shaderName, out metadata);
                } catch (Exception ex) {
                    Logger.WriteError($"Shader metadata load failed for '{shaderName}': {ex.Message}");
                    metadata = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Saves shader metadata to the cache store.
        /// </summary>
        /// <param name="shaderName">Shader name to save.</param>
        /// <param name="metadata">Metadata to persist.</param>
        void SaveShaderMetadata(string shaderName, ShaderCacheMetadata metadata) {
            lock (MetadataLock) {
                try {
                    MetadataStore.Save(shaderName, metadata);
                } catch (Exception ex) {
                    Logger.WriteError($"Shader metadata save failed for '{shaderName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Deletes cached metadata for the specified shader.
        /// </summary>
        /// <param name="shaderName">Shader name to delete.</param>
        void DeleteShaderMetadata(string shaderName) {
            lock (MetadataLock) {
                try {
                    MetadataStore.Delete(shaderName);
                } catch (Exception ex) {
                    Logger.WriteError($"Shader metadata delete failed for '{shaderName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Compares two shader hashes for equality.
        /// </summary>
        /// <param name="left">First hash.</param>
        /// <param name="right">Second hash.</param>
        /// <returns>True when the hashes match.</returns>
        bool IsHashMatch(string left, string right) {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a file path represents a shader source file.
        /// </summary>
        /// <param name="path">File path to evaluate.</param>
        /// <returns>True when the file is a shader source.</returns>
        static bool IsShaderFile(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            string extension = Path.GetExtension(path);
            for (int i = 0; i < ShaderExtensions.Length; i++) {
                if (string.Equals(extension, ShaderExtensions[i], StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Replaces an existing package handle with a new one.
        /// </summary>
        /// <param name="shaderName">Shader name to replace.</param>
        /// <param name="handle">New package handle.</param>
        void ReplaceModule(string shaderName, ShaderPackageHandle handle) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (handle == null) {
                throw new ArgumentNullException(nameof(handle));
            }

            ShaderPackageHandle existing;
            if (loadedModules.TryGetValue(shaderName, out existing)) {
                existing.Dispose();
            }

            loadedModules[shaderName] = handle;
        }

        /// <summary>
        /// Unloads a module by shader name.
        /// </summary>
        /// <param name="shaderName">Shader name to unload.</param>
        void UnloadModule(string shaderName) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                return;
            }

            ShaderPackageHandle handle;
            if (loadedModules.TryGetValue(shaderName, out handle)) {
                handle.Dispose();
                loadedModules.Remove(shaderName);
            }
        }

        /// <summary>
        /// Unloads all shader modules currently loaded by the manager.
        /// </summary>
        void UnloadAllModules() {
            foreach (var pair in loadedModules) {
                pair.Value.Dispose();
            }

            loadedModules.Clear();
        }

        /// <summary>
        /// Builds a compile service configured for the shader source root.
        /// </summary>
        /// <returns>Initialized compile service.</returns>
        ShaderCompileService BuildCompileService() {
            ShaderFilesystemIncludeResolver includeResolver = new ShaderFilesystemIncludeResolver(options.ShaderRootPath);
            ShaderMemoryCompileCache cache = new ShaderMemoryCompileCache();
            ShaderSourceHasher hasher = new ShaderSourceHasher();
            ShaderCompileService service = new ShaderCompileService(includeResolver, cache, hasher);
            service.RegisterBackend(new helengine.directx11.DirectX11ShaderBackend());
            return service;
        }

        /// <summary>
        /// Selects the build result that matches the runtime target.
        /// </summary>
        /// <param name="results">Build results to search.</param>
        /// <returns>Matching build result.</returns>
        ShaderPackageBuildResult SelectRuntimeResult(IReadOnlyList<ShaderPackageBuildResult> results) {
            for (int i = 0; i < results.Count; i++) {
                ShaderPackageBuildResult result = results[i];
                if (result.Target == options.RuntimeTarget) {
                    return result;
                }
            }

            throw new InvalidOperationException("No shader package build result matched the runtime target.");
        }

        /// <summary>
        /// Logs compilation diagnostics for the provided results.
        /// </summary>
        /// <param name="results">Compilation results to log.</param>
        void LogDiagnostics(IReadOnlyList<ShaderCompileResult> results) {
            for (int i = 0; i < results.Count; i++) {
                ShaderCompileResult result = results[i];
                IReadOnlyList<ShaderCompileDiagnostic> diagnostics = result.Diagnostics;
                for (int j = 0; j < diagnostics.Count; j++) {
                    ShaderCompileDiagnostic diagnostic = diagnostics[j];
                    LogDiagnostic(diagnostic);
                }
            }
        }

        /// <summary>
        /// Writes a single diagnostic message to the logger.
        /// </summary>
        /// <param name="diagnostic">Diagnostic to log.</param>
        void LogDiagnostic(ShaderCompileDiagnostic diagnostic) {
            if (diagnostic == null) {
                return;
            }

            string message = diagnostic.Message;
            switch (diagnostic.Severity) {
                case ShaderDiagnosticSeverity.Info:
                    Logger.WriteLine(message);
                    break;
                case ShaderDiagnosticSeverity.Warning:
                    Logger.WriteWarning(message);
                    break;
                case ShaderDiagnosticSeverity.Error:
                    Logger.WriteError(message);
                    break;
                default:
                    Logger.WriteLine(message);
                    break;
            }
        }

        /// <summary>
        /// Enumerates shader source files under the shader root.
        /// </summary>
        /// <returns>Array of shader source file paths.</returns>
        string[] EnumerateShaderFiles() {
            List<string> files = new List<string>();
            IEnumerable<string> candidates = Directory.EnumerateFiles(options.ShaderRootPath, "*.*", SearchOption.AllDirectories);
            foreach (string candidate in candidates) {
                if (IsShaderFile(candidate)) {
                    files.Add(candidate);
                }
            }

            return files.ToArray();
        }

        /// <summary>
        /// Returns a snapshot array of the current shader entries.
        /// </summary>
        /// <returns>Array of shader entries.</returns>
        ShaderSourceEntry[] GetEntriesSnapshot() {
            lock (entriesLock) {
                ShaderSourceEntry[] entries = new ShaderSourceEntry[entriesByName.Count];
                int index = 0;
                foreach (var pair in entriesByName) {
                    entries[index++] = pair.Value;
                }

                return entries;
            }
        }

        /// <summary>
        /// Validates active shaders on a fixed interval and queues rebuilds when they change.
        /// </summary>
        /// <param name="now">Current UTC timestamp.</param>
        void ValidateActiveShaders(DateTime now) {
            List<string> shadersToCheck = new List<string>();
            lock (ActiveLock) {
                foreach (var pair in ActiveShaderChecks) {
                    if ((now - pair.Value).TotalMilliseconds >= ActiveShaderCheckIntervalMilliseconds) {
                        shadersToCheck.Add(pair.Key);
                    }
                }

                for (int i = 0; i < shadersToCheck.Count; i++) {
                    ActiveShaderChecks[shadersToCheck[i]] = now;
                }
            }

            for (int i = 0; i < shadersToCheck.Count; i++) {
                string shaderName = shadersToCheck[i];
                ShaderSourceEntry entry;
                if (!TryGetEntryByName(shaderName, out entry)) {
                    RemoveActiveShader(shaderName);
                    continue;
                }

                if (ShouldQueueBuild(entry)) {
                    QueueBuildIfNotPending(entry);
                }
            }
        }

        /// <summary>
        /// Removes a shader from the active validation list.
        /// </summary>
        /// <param name="shaderName">Shader name to remove.</param>
        void RemoveActiveShader(string shaderName) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                return;
            }

            lock (ActiveLock) {
                ActiveShaderChecks.Remove(shaderName);
            }
        }

        /// <summary>
        /// Attempts to locate a shader entry by its logical name.
        /// </summary>
        /// <param name="shaderName">Shader name to find.</param>
        /// <param name="entry">Matching entry when found.</param>
        /// <returns>True when a matching entry is found.</returns>
        bool TryGetEntryByName(string shaderName, out ShaderSourceEntry entry) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                entry = null;
                return false;
            }

            lock (entriesLock) {
                return entriesByName.TryGetValue(shaderName, out entry);
            }
        }

        /// <summary>
        /// Gets an existing shader entry or creates one for a new file.
        /// </summary>
        /// <param name="sourcePath">Absolute shader source path.</param>
        /// <returns>Resolved shader entry, or null when the entry cannot be created.</returns>
        ShaderSourceEntry GetOrAddEntry(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                return null;
            }

            lock (entriesLock) {
                ShaderSourceEntry existing;
                if (entriesBySourcePath.TryGetValue(sourcePath, out existing)) {
                    return existing;
                }

                return AddEntry(sourcePath);
            }
        }

        /// <summary>
        /// Removes a shader entry by its source path.
        /// </summary>
        /// <param name="sourcePath">Absolute shader source path.</param>
        /// <param name="entry">Removed entry when found.</param>
        /// <returns>True when the entry is removed.</returns>
        bool RemoveEntry(string sourcePath, out ShaderSourceEntry entry) {
            lock (entriesLock) {
                if (!entriesBySourcePath.TryGetValue(sourcePath, out entry)) {
                    return false;
                }

                entriesBySourcePath.Remove(sourcePath);
                entriesByName.Remove(entry.Name);
                return true;
            }
        }

        /// <summary>
        /// Adds a new shader entry for the provided source path.
        /// </summary>
        /// <param name="sourcePath">Absolute shader source path.</param>
        /// <returns>New shader entry, or null when a name conflict exists.</returns>
        ShaderSourceEntry AddEntry(string sourcePath) {
            string relativePath = Path.GetRelativePath(options.ShaderRootPath, sourcePath);
            string shaderName = BuildShaderName(relativePath);
            ShaderSourceEntry existing;
            if (entriesByName.TryGetValue(shaderName, out existing)) {
                if (!string.Equals(existing.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)) {
                    Logger.WriteWarning($"Shader name conflict detected for '{shaderName}'.");
                    return null;
                }

                return existing;
            }

            ShaderSourceEntry entry = new ShaderSourceEntry(shaderName, relativePath, sourcePath);
            entriesBySourcePath[sourcePath] = entry;
            entriesByName[shaderName] = entry;
            return entry;
        }

        /// <summary>
        /// Builds a logical shader name from a relative path.
        /// </summary>
        /// <param name="relativePath">Path relative to the shader root.</param>
        /// <returns>Logical shader name.</returns>
        string BuildShaderName(string relativePath) {
            string withoutExtension = Path.ChangeExtension(relativePath, null);
            if (string.IsNullOrWhiteSpace(withoutExtension)) {
                throw new InvalidOperationException("Shader name could not be resolved from the path.");
            }

            string normalized = withoutExtension.Replace(Path.DirectorySeparatorChar, '.');
            normalized = normalized.Replace(Path.AltDirectorySeparatorChar, '.');
            return normalized;
        }
    }
}
