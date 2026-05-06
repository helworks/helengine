using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Copies built game-script binaries into an isolated snapshot and loads them into a collectible context.
    /// </summary>
    public sealed class EditorGameScriptAssemblyHost : IEditorScriptAssemblyHost {
        /// <summary>
        /// Root directory that owns all script snapshots for the current project.
        /// </summary>
        readonly string SnapshotRootPath;

        /// <summary>
        /// Currently loaded collectible context for the active script assembly.
        /// </summary>
        Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> CurrentLoadContextsByModuleId;

        /// <summary>
        /// Currently loaded scripting assemblies used for reflection-based discovery.
        /// </summary>
        Dictionary<string, Assembly> CurrentAssembliesByModuleId;

        /// <summary>
        /// Snapshot root directory that backs the currently loaded collectible contexts.
        /// </summary>
        string CurrentSnapshotRootDirectoryPath;

        /// <summary>
        /// Shared script type resolver backed by the currently loaded module assemblies.
        /// </summary>
        ScriptTypeResolver ScriptTypeResolverValue;

        /// <summary>
        /// Initializes one assembly host rooted under the supplied project directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute game project root path.</param>
        public EditorGameScriptAssemblyHost(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            SnapshotRootPath = BuildSnapshotRootPath(projectRootPath);
            DeleteDirectoryIfPresent(SnapshotRootPath);
            Directory.CreateDirectory(SnapshotRootPath);
            CurrentLoadContextsByModuleId = new Dictionary<string, EditorCollectibleScriptAssemblyLoadContext>(StringComparer.OrdinalIgnoreCase);
            CurrentAssembliesByModuleId = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            ScriptTypeResolverValue = new ScriptTypeResolver();
        }

        /// <summary>
        /// Gets the shared script type resolver backed by the currently loaded module assemblies.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver => ScriptTypeResolverValue;

        /// <summary>
        /// Reloads the current scripting assemblies by copying each build output into a snapshot and loading it.
        /// </summary>
        /// <param name="assemblies">Descriptors for the freshly built module assemblies.</param>
        public void Reload(IReadOnlyList<ScriptAssemblyDescriptor> assemblies) {
            if (assemblies == null) {
                throw new ArgumentNullException(nameof(assemblies));
            }
            if (assemblies.Count == 0) {
                throw new InvalidOperationException("At least one script assembly descriptor must be provided.");
            }

            ValidateAssemblies(assemblies);

            string snapshotRootDirectoryPath = Path.Combine(SnapshotRootPath, Guid.NewGuid().ToString("N"));
            Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> nextLoadContextsByModuleId = new Dictionary<string, EditorCollectibleScriptAssemblyLoadContext>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Assembly> nextAssembliesByModuleId = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            ScriptTypeResolver nextScriptTypeResolver = new ScriptTypeResolver();
            Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> previousLoadContextsByModuleId = CurrentLoadContextsByModuleId;
            Dictionary<string, Assembly> previousAssembliesByModuleId = CurrentAssembliesByModuleId;
            string previousSnapshotRootDirectoryPath = CurrentSnapshotRootDirectoryPath;
            try {
                for (int index = 0; index < assemblies.Count; index++) {
                    ScriptAssemblyDescriptor descriptor = assemblies[index];
                    string moduleSnapshotDirectoryPath = Path.Combine(snapshotRootDirectoryPath, descriptor.ModuleId);
                    CopyDirectory(descriptor.OutputDirectoryPath, moduleSnapshotDirectoryPath);

                    string snapshotAssemblyPath = Path.Combine(moduleSnapshotDirectoryPath, Path.GetFileName(descriptor.AssemblyPath));
                    EditorCollectibleScriptAssemblyLoadContext nextLoadContext = new EditorCollectibleScriptAssemblyLoadContext(snapshotAssemblyPath);
                    Assembly nextAssembly = nextLoadContext.LoadFromAssemblyPath(snapshotAssemblyPath);
                    nextLoadContextsByModuleId.Add(descriptor.ModuleId, nextLoadContext);
                    nextAssembliesByModuleId.Add(descriptor.ModuleId, nextAssembly);
                    nextScriptTypeResolver.Register(descriptor.ModuleId, nextAssembly);
                }

                CurrentLoadContextsByModuleId = new Dictionary<string, EditorCollectibleScriptAssemblyLoadContext>(StringComparer.OrdinalIgnoreCase);
                CurrentAssembliesByModuleId = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
                CurrentSnapshotRootDirectoryPath = null;

                UnloadContexts(previousLoadContextsByModuleId);
                DeleteDirectoryIfPresent(previousSnapshotRootDirectoryPath);

                CurrentLoadContextsByModuleId = nextLoadContextsByModuleId;
                CurrentAssembliesByModuleId = nextAssembliesByModuleId;
                CurrentSnapshotRootDirectoryPath = snapshotRootDirectoryPath;
                ScriptTypeResolverValue = nextScriptTypeResolver;
            } catch {
                UnloadContexts(nextLoadContextsByModuleId);
                DeleteDirectoryIfPresent(snapshotRootDirectoryPath);
                CurrentLoadContextsByModuleId = previousLoadContextsByModuleId;
                CurrentAssembliesByModuleId = previousAssembliesByModuleId;
                CurrentSnapshotRootDirectoryPath = previousSnapshotRootDirectoryPath;
                ScriptTypeResolverValue = BuildResolver(previousAssembliesByModuleId);
                throw;
            }
        }

        /// <summary>
        /// Releases the current collectible context when the host is disposed.
        /// </summary>
        public void Dispose() {
            if (CurrentLoadContextsByModuleId.Count == 0) {
                return;
            }

            try {
                UnloadContexts(CurrentLoadContextsByModuleId);
                DeleteDirectoryIfPresent(CurrentSnapshotRootDirectoryPath);
            } catch {
            }

            CurrentLoadContextsByModuleId = new Dictionary<string, EditorCollectibleScriptAssemblyLoadContext>(StringComparer.OrdinalIgnoreCase);
            CurrentAssembliesByModuleId = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            CurrentSnapshotRootDirectoryPath = null;
            ScriptTypeResolverValue = new ScriptTypeResolver();
        }

        /// <summary>
        /// Returns the addable component descriptors discovered from the current script assembly.
        /// </summary>
        /// <param name="entity">Entity that will receive one selected component.</param>
        /// <returns>Descriptors discovered from the active script assembly.</returns>
        public IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (CurrentAssembliesByModuleId.Count == 0) {
                return Array.Empty<EditorComponentAddDescriptor>();
            }

            List<EditorComponentAddDescriptor> descriptors = new List<EditorComponentAddDescriptor>();
            foreach (Assembly assembly in CurrentAssembliesByModuleId.Values) {
                descriptors.AddRange(EditorScriptComponentCatalog.BuildDescriptors(assembly));
            }

            descriptors.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            return descriptors;
        }

        /// <summary>
        /// Validates the supplied script assembly descriptors before loading begins.
        /// </summary>
        /// <param name="assemblies">Descriptors to validate.</param>
        void ValidateAssemblies(IReadOnlyList<ScriptAssemblyDescriptor> assemblies) {
            HashSet<string> moduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < assemblies.Count; index++) {
                ScriptAssemblyDescriptor descriptor = assemblies[index];
                if (descriptor == null) {
                    throw new InvalidOperationException("Script assembly descriptors must not contain null entries.");
                }
                if (!moduleIds.Add(descriptor.ModuleId)) {
                    throw new InvalidOperationException($"Script assembly descriptor '{descriptor.ModuleId}' was supplied more than once.");
                }
                if (!Directory.Exists(descriptor.OutputDirectoryPath)) {
                    throw new DirectoryNotFoundException($"Script build output directory '{descriptor.OutputDirectoryPath}' does not exist.");
                }
                if (!File.Exists(descriptor.AssemblyPath)) {
                    throw new FileNotFoundException($"Script assembly '{descriptor.AssemblyPath}' was not produced.", descriptor.AssemblyPath);
                }
            }
        }

        /// <summary>
        /// Copies one directory tree recursively into another directory.
        /// </summary>
        /// <param name="sourceDirectoryPath">Directory that should be copied.</param>
        /// <param name="destinationDirectoryPath">Destination directory that should receive the copied files.</param>
        void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath) {
            Directory.CreateDirectory(destinationDirectoryPath);

            string[] files = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++) {
                string sourceFilePath = files[index];
                string relativeFilePath = Path.GetRelativePath(sourceDirectoryPath, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationDirectoryPath, relativeFilePath);
                string destinationFileDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationFileDirectoryPath)) {
                    Directory.CreateDirectory(destinationFileDirectoryPath);
                }

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }

        /// <summary>
        /// Builds the transient snapshot root path for one project under the current user's temporary directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <returns>Per-project snapshot directory stored outside the project tree.</returns>
        static string BuildSnapshotRootPath(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string normalizedProjectRootPath = Path.GetFullPath(projectRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            byte[] pathBytes = Encoding.UTF8.GetBytes(normalizedProjectRootPath);
            string projectHash = Convert.ToHexString(SHA256.HashData(pathBytes));
            return Path.Combine(Path.GetTempPath(), "helengine", "script_snapshots", projectHash);
        }

        /// <summary>
        /// Deletes one directory tree when it exists.
        /// </summary>
        /// <param name="directoryPath">Directory path to delete.</param>
        void DeleteDirectoryIfPresent(string directoryPath) {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath)) {
                Directory.Delete(directoryPath, true);
            }
        }

        /// <summary>
        /// Initiates unload for one collectible context and returns a weak reference that can be polled later.
        /// </summary>
        /// <param name="loadContext">Collectible context to unload.</param>
        /// <returns>Weak reference that can be observed until the context is collected.</returns>
        WeakReference BeginUnload(EditorCollectibleScriptAssemblyLoadContext loadContext) {
            if (loadContext == null) {
                return null;
            }

            WeakReference loadContextReference = new WeakReference(loadContext);
            loadContext.Unload();
            return loadContextReference;
        }

        /// <summary>
        /// Forces garbage collection until one collectible context disappears or the retry limit is reached.
        /// </summary>
        /// <param name="loadContextReference">Weak reference returned by <see cref="BeginUnload(EditorCollectibleScriptAssemblyLoadContext)"/>.</param>
        void WaitForUnload(WeakReference loadContextReference) {
            if (loadContextReference == null) {
                return;
            }

            for (int attempt = 0; attempt < 12 && loadContextReference.IsAlive; attempt++) {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            if (loadContextReference.IsAlive) {
                throw new InvalidOperationException("The previous scripting assembly could not be unloaded.");
            }
        }

        /// <summary>
        /// Unloads all collectible contexts tracked in the supplied dictionary.
        /// </summary>
        /// <param name="loadContextsByModuleId">Load contexts keyed by module id.</param>
        void UnloadContexts(Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> loadContextsByModuleId) {
            if (loadContextsByModuleId == null || loadContextsByModuleId.Count == 0) {
                return;
            }

            List<WeakReference> references = new List<WeakReference>(loadContextsByModuleId.Count);
            foreach (EditorCollectibleScriptAssemblyLoadContext loadContext in loadContextsByModuleId.Values) {
                WeakReference loadContextReference = BeginUnload(loadContext);
                if (loadContextReference != null) {
                    references.Add(loadContextReference);
                }
            }

            for (int index = 0; index < references.Count; index++) {
                WaitForUnload(references[index]);
            }
        }

        /// <summary>
        /// Builds one script resolver from the supplied module assembly table.
        /// </summary>
        /// <param name="assembliesByModuleId">Loaded assemblies keyed by module id.</param>
        /// <returns>Resolver populated with the supplied assemblies.</returns>
        ScriptTypeResolver BuildResolver(Dictionary<string, Assembly> assembliesByModuleId) {
            if (assembliesByModuleId == null) {
                throw new ArgumentNullException(nameof(assembliesByModuleId));
            }

            ScriptTypeResolver resolver = new ScriptTypeResolver();
            foreach (KeyValuePair<string, Assembly> entry in assembliesByModuleId) {
                resolver.Register(entry.Key, entry.Value);
            }

            return resolver;
        }
    }
}
