using System.Diagnostics;
using System.Timers;

namespace helengine.editor {
    /// <summary>
    /// Watches shader source files, runs the shader tool, and hot-loads shader modules in the editor.
    /// </summary>
    public class ShaderModuleManager : IDisposable {
        /// <summary>
        /// Command name used to invoke the shader tool build action.
        /// </summary>
        const string ShaderToolBuildCommand = "build";

        /// <summary>
        /// Command line argument that specifies the manifest path.
        /// </summary>
        const string ShaderToolManifestArgument = "--manifest";

        /// <summary>
        /// Command line argument that specifies a shader name filter.
        /// </summary>
        const string ShaderToolShaderArgument = "--shader";

        /// <summary>
        /// Command line argument that enables module emission.
        /// </summary>
        const string ShaderToolEmitModulesArgument = "--emit-modules";

        /// <summary>
        /// Shader file extensions recognized by the manager.
        /// </summary>
        static readonly string[] ShaderExtensions = new[] { ".hlsl", ".fx" };

        /// <summary>
        /// Options controlling shader module compilation.
        /// </summary>
        readonly ShaderModuleManagerOptions options;

        /// <summary>
        /// Loader used to load compiled shader modules.
        /// </summary>
        readonly ShaderModuleLoader loader;

        /// <summary>
        /// Tracks loaded shader module handles by shader name.
        /// </summary>
        readonly Dictionary<string, ShaderModuleHandle> loadedModules;

        /// <summary>
        /// Tracks queued build timestamps for shader names.
        /// </summary>
        readonly Dictionary<string, DateTime> pendingBuilds;

        /// <summary>
        /// Synchronization object for build queue access.
        /// </summary>
        readonly object pendingLock;

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
        /// Loaded manifest index.
        /// </summary>
        ShaderManifestIndex manifestIndex;

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
            loader = new ShaderModuleLoader();
            loadedModules = new Dictionary<string, ShaderModuleHandle>(StringComparer.OrdinalIgnoreCase);
            pendingBuilds = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            pendingLock = new object();
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

            manifestIndex = ShaderManifestIndex.Load(options.ManifestPath);
            ValidateShaderToolPath();
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
        /// Starts the file system watcher for shader source files.
        /// </summary>
        void StartWatcher() {
            watcher = new FileSystemWatcher(manifestIndex.RootPath);
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
        /// Validates that the shader tool path exists on disk.
        /// </summary>
        void ValidateShaderToolPath() {
            if (!File.Exists(options.ShaderToolPath)) {
                throw new FileNotFoundException("Shader tool was not found.", options.ShaderToolPath);
            }
        }

        /// <summary>
        /// Queues initial builds for all shaders in the manifest.
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

            ShaderManifestIndexEntry entry;
            if (!manifestIndex.TryGetBySourcePath(e.FullPath, out entry)) {
                QueueAllBuilds();
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

            ShaderManifestIndexEntry entry;
            if (!manifestIndex.TryGetBySourcePath(e.FullPath, out entry)) {
                QueueAllBuilds();
                return;
            }

            UnloadModule(entry.Name);
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
                ShaderManifestIndexEntry oldEntry;
                if (manifestIndex.TryGetBySourcePath(e.OldFullPath, out oldEntry)) {
                    UnloadModule(oldEntry.Name);
                }
            }

            if (IsShaderFile(e.FullPath)) {
                ShaderManifestIndexEntry newEntry;
                if (manifestIndex.TryGetBySourcePath(e.FullPath, out newEntry)) {
                    QueueBuild(newEntry);
                } else {
                    QueueAllBuilds();
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
        /// <param name="entry">Manifest entry to build.</param>
        void QueueBuild(ShaderManifestIndexEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (pendingLock) {
                pendingBuilds[entry.Name] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Queues builds for all shaders in the manifest.
        /// </summary>
        void QueueAllBuilds() {
            IReadOnlyList<ShaderManifestIndexEntry> entries = manifestIndex.Entries;
            for (int i = 0; i < entries.Count; i++) {
                QueueBuild(entries[i]);
            }
        }

        /// <summary>
        /// Executes the shader tool for a specific shader and loads the resulting module.
        /// </summary>
        /// <param name="shaderName">Shader name to build.</param>
        void BuildShader(string shaderName) {
            ShaderManifestIndexEntry entry;
            if (!manifestIndex.TryGetByName(shaderName, out entry)) {
                return;
            }

            ShaderToolResult result = RunShaderTool(shaderName);
            if (result.ExitCode != 0) {
                Logger.WriteError($"Shader build failed for '{shaderName}': {result.ErrorOutput}");
                return;
            }

            if (!File.Exists(entry.ModuleAssemblyPath)) {
                Logger.WriteError($"Shader module was not produced: {entry.ModuleAssemblyPath}");
                return;
            }

            ShaderModuleHandle handle = loader.LoadModule(entry.ModuleAssemblyPath);
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
        /// Runs the shader tool to rebuild a specific shader.
        /// </summary>
        /// <param name="shaderName">Shader name to build.</param>
        /// <returns>Result of the shader tool invocation.</returns>
        ShaderToolResult RunShaderTool(string shaderName) {
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = options.ShaderToolPath,
                Arguments = BuildShaderToolArguments(shaderName),
                WorkingDirectory = manifestIndex.RootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo)) {
                if (process == null) {
                    throw new InvalidOperationException("Failed to start the shader tool process.");
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new ShaderToolResult(process.ExitCode, output, error);
            }
        }

        /// <summary>
        /// Builds the shader tool command line arguments for a shader name.
        /// </summary>
        /// <param name="shaderName">Shader name to build.</param>
        /// <returns>Command line arguments.</returns>
        string BuildShaderToolArguments(string shaderName) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            return $"{ShaderToolBuildCommand} {ShaderToolManifestArgument} \"{options.ManifestPath}\" {ShaderToolShaderArgument} \"{shaderName}\" {ShaderToolEmitModulesArgument}";
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
        /// Replaces an existing module handle with a new one.
        /// </summary>
        /// <param name="shaderName">Shader name to replace.</param>
        /// <param name="handle">New module handle.</param>
        void ReplaceModule(string shaderName, ShaderModuleHandle handle) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (handle == null) {
                throw new ArgumentNullException(nameof(handle));
            }

            ShaderModuleHandle existing;
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

            ShaderModuleHandle handle;
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
    }
}
