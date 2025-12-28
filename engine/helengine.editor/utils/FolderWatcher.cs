namespace helengine.editor {
    /// <summary>
    /// Watches a folder tree and reports file system changes through a callback.
    /// </summary>
    public class FolderWatcher : IDisposable {
        /// <summary>
        /// Default file name filter used by the watcher.
        /// </summary>
        const string DefaultFilter = "*.*";

        /// <summary>
        /// Default notify filters used by the watcher.
        /// </summary>
        const NotifyFilters DefaultNotifyFilters = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;

        /// <summary>
        /// Root path to monitor.
        /// </summary>
        readonly string rootPath;

        /// <summary>
        /// Callback invoked for each file system change.
        /// </summary>
        readonly Action<FolderWatchEvent> callback;

        /// <summary>
        /// Value indicating whether subdirectories are monitored.
        /// </summary>
        readonly bool includeSubdirectories;

        /// <summary>
        /// Notify filters applied to the watcher.
        /// </summary>
        readonly NotifyFilters notifyFilters;

        /// <summary>
        /// File name filter pattern applied to the watcher.
        /// </summary>
        readonly string filter;

        /// <summary>
        /// Underlying system watcher instance.
        /// </summary>
        FileSystemWatcher watcher;

        /// <summary>
        /// Tracks whether the watcher is currently active.
        /// </summary>
        bool started;

        /// <summary>
        /// Tracks whether the watcher has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes a new folder watcher using default filters and recursion.
        /// </summary>
        /// <param name="rootPath">Absolute path of the folder to monitor.</param>
        /// <param name="callback">Callback invoked for each file system change.</param>
        public FolderWatcher(string rootPath, Action<FolderWatchEvent> callback)
            : this(rootPath, callback, true, DefaultNotifyFilters, DefaultFilter) {
        }

        /// <summary>
        /// Initializes a new folder watcher with explicit configuration.
        /// </summary>
        /// <param name="rootPath">Absolute path of the folder to monitor.</param>
        /// <param name="callback">Callback invoked for each file system change.</param>
        /// <param name="includeSubdirectories">True to watch subdirectories.</param>
        /// <param name="notifyFilters">Notify filters to apply.</param>
        /// <param name="filter">File name filter pattern.</param>
        public FolderWatcher(
            string rootPath,
            Action<FolderWatchEvent> callback,
            bool includeSubdirectories,
            NotifyFilters notifyFilters,
            string filter
        ) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }

            if (callback == null) {
                throw new ArgumentNullException(nameof(callback));
            }

            if (string.IsNullOrWhiteSpace(filter)) {
                throw new ArgumentException("Filter must be provided.", nameof(filter));
            }

            this.rootPath = Path.GetFullPath(rootPath);
            this.callback = callback;
            this.includeSubdirectories = includeSubdirectories;
            this.notifyFilters = notifyFilters;
            this.filter = filter;
        }

        /// <summary>
        /// Gets the root path monitored by the watcher.
        /// </summary>
        public string RootPath => rootPath;

        /// <summary>
        /// Gets a value indicating whether the watcher has been started.
        /// </summary>
        public bool IsStarted => started;

        /// <summary>
        /// Starts monitoring the folder for changes.
        /// </summary>
        public void Start() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(FolderWatcher));
            }

            if (started) {
                throw new InvalidOperationException("Folder watcher has already been started.");
            }

            if (!Directory.Exists(rootPath)) {
                throw new DirectoryNotFoundException($"Folder watcher root path does not exist: {rootPath}");
            }

            watcher = new FileSystemWatcher(rootPath);
            watcher.IncludeSubdirectories = includeSubdirectories;
            watcher.NotifyFilter = notifyFilters;
            watcher.Filter = filter;
            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;
            started = true;
        }

        /// <summary>
        /// Stops monitoring and releases the underlying system watcher.
        /// </summary>
        public void Stop() {
            if (watcher == null) {
                started = false;
                return;
            }

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnChanged;
            watcher.Created -= OnCreated;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
            watcher = null;
            started = false;
        }

        /// <summary>
        /// Disposes the watcher and stops monitoring for changes.
        /// </summary>
        public void Dispose() {
            if (disposed) {
                return;
            }

            Stop();
            disposed = true;
        }

        /// <summary>
        /// Handles change notifications for updated items.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnChanged(object sender, FileSystemEventArgs e) {
            DispatchChange(new FolderWatchEvent(FolderWatchEventKind.Changed, e.FullPath));
        }

        /// <summary>
        /// Handles change notifications for created items.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnCreated(object sender, FileSystemEventArgs e) {
            DispatchChange(new FolderWatchEvent(FolderWatchEventKind.Created, e.FullPath));
        }

        /// <summary>
        /// Handles change notifications for deleted items.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnDeleted(object sender, FileSystemEventArgs e) {
            DispatchChange(new FolderWatchEvent(FolderWatchEventKind.Deleted, e.FullPath));
        }

        /// <summary>
        /// Handles change notifications for renamed items.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">File system event data.</param>
        void OnRenamed(object sender, RenamedEventArgs e) {
            DispatchChange(new FolderWatchEvent(e.FullPath, e.OldFullPath));
        }

        /// <summary>
        /// Delivers a change event to the callback if the watcher is active.
        /// </summary>
        /// <param name="change">Change event to dispatch.</param>
        void DispatchChange(FolderWatchEvent change) {
            if (disposed || !started) {
                return;
            }

            callback(change);
        }
    }
}
