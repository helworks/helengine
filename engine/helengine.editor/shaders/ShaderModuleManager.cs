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
        /// Semaphore used to avoid concurrent build execution.
        /// </summary>
        readonly SemaphoreSlim buildSemaphore;

        /// <summary>
        /// File system watcher used to monitor shader source changes.
        /// </summary>
        FileSystemWatcher watcher;

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
        /// Stops monitoring and unloads shader modules.
        /// </summary>
        public void Stop() {
            if (disposed) {
                return;
            }

            if (watcher != null) {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnSourceChanged;
                watcher.Created -= OnSourceChanged;
                watcher.Deleted -= OnSourceDeleted;
                watcher.Renamed -= OnSourceRenamed;
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
        /// Starts the file system watcher for shader source files.
        /// </summary>
        void StartWatcher() {
            watcher = new FileSystemWatcher(options.ShaderRootPath);
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Filter = "*.*";
            watcher.Changed += OnSourceChanged;
            watcher.Created += OnSourceChanged;
            watcher.Deleted += OnSourceDeleted;
            watcher.Renamed += OnSourceRenamed;
            watcher.EnableRaisingEvents = true;
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
            QueueAllBuilds();
        }

        /// <summary>
        /// Handles file creation or update events from the watcher.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnSourceChanged(object sender, FileSystemEventArgs e) {
            if (disposed) {
                return;
            }

            if (!IsShaderFile(e.FullPath)) {
                return;
            }

            ShaderSourceEntry entry = GetOrAddEntry(e.FullPath);
            if (entry == null) {
                return;
            }

            QueueBuild(entry);
        }

        /// <summary>
        /// Handles file delete events from the watcher.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnSourceDeleted(object sender, FileSystemEventArgs e) {
            if (disposed) {
                return;
            }

            if (!IsShaderFile(e.FullPath)) {
                return;
            }

            ShaderSourceEntry entry;
            if (RemoveEntry(e.FullPath, out entry)) {
                UnloadModule(entry.Name);
            }
        }

        /// <summary>
        /// Handles file rename events from the watcher.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnSourceRenamed(object sender, RenamedEventArgs e) {
            if (disposed) {
                return;
            }

            if (IsShaderFile(e.OldFullPath)) {
                ShaderSourceEntry oldEntry;
                if (RemoveEntry(e.OldFullPath, out oldEntry)) {
                    UnloadModule(oldEntry.Name);
                }
            }

            if (IsShaderFile(e.FullPath)) {
                ShaderSourceEntry newEntry = GetOrAddEntry(e.FullPath);
                if (newEntry != null) {
                    QueueBuild(newEntry);
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
        /// Queues builds for all known shaders.
        /// </summary>
        void QueueAllBuilds() {
            ShaderSourceEntry[] entries = GetEntriesSnapshot();
            for (int i = 0; i < entries.Length; i++) {
                QueueBuild(entries[i]);
            }
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
