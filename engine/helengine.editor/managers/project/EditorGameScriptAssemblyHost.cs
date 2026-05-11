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
        /// Module kinds keyed by module id for the currently loaded assemblies.
        /// </summary>
        Dictionary<string, EditorCodeModuleKind> CurrentModuleKindsByModuleId;

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
            CurrentModuleKindsByModuleId = new Dictionary<string, EditorCodeModuleKind>(StringComparer.OrdinalIgnoreCase);
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
        public void Reload(IReadOnlyList<EditorScriptAssemblyDescriptor> assemblies) {
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
            Dictionary<string, EditorCodeModuleKind> nextModuleKindsByModuleId = new Dictionary<string, EditorCodeModuleKind>(StringComparer.OrdinalIgnoreCase);
            try {
                for (int index = 0; index < assemblies.Count; index++) {
                    EditorScriptAssemblyDescriptor descriptor = assemblies[index];
                    string moduleSnapshotDirectoryPath = Path.Combine(snapshotRootDirectoryPath, descriptor.ModuleId);
                    CopyDirectory(descriptor.OutputDirectoryPath, moduleSnapshotDirectoryPath);

                    string snapshotAssemblyPath = Path.Combine(moduleSnapshotDirectoryPath, Path.GetFileName(descriptor.AssemblyPath));
                    EditorCollectibleScriptAssemblyLoadContext nextLoadContext = new EditorCollectibleScriptAssemblyLoadContext(snapshotAssemblyPath);
                    Assembly nextAssembly = nextLoadContext.LoadFromAssemblyPath(snapshotAssemblyPath);
                    nextLoadContextsByModuleId.Add(descriptor.ModuleId, nextLoadContext);
                    nextAssembliesByModuleId.Add(descriptor.ModuleId, nextAssembly);
                    nextModuleKindsByModuleId.Add(descriptor.ModuleId, descriptor.ModuleKind);
                }
            } catch {
                UnloadContexts(nextLoadContextsByModuleId);
                DeleteDirectoryIfPresent(snapshotRootDirectoryPath);
                throw;
            }

            Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> previousLoadContextsByModuleId = CurrentLoadContextsByModuleId;
            string previousSnapshotRootDirectoryPath = CurrentSnapshotRootDirectoryPath;
            CurrentLoadContextsByModuleId = new Dictionary<string, EditorCollectibleScriptAssemblyLoadContext>(StringComparer.OrdinalIgnoreCase);
            CurrentAssembliesByModuleId = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            CurrentModuleKindsByModuleId = new Dictionary<string, EditorCodeModuleKind>(StringComparer.OrdinalIgnoreCase);
            CurrentSnapshotRootDirectoryPath = null;
            ScriptTypeResolverValue.Clear();

            UnloadContexts(previousLoadContextsByModuleId);
            DeleteDirectoryIfPresent(previousSnapshotRootDirectoryPath);

            CurrentLoadContextsByModuleId = nextLoadContextsByModuleId;
            CurrentAssembliesByModuleId = nextAssembliesByModuleId;
            CurrentModuleKindsByModuleId = nextModuleKindsByModuleId;
            CurrentSnapshotRootDirectoryPath = snapshotRootDirectoryPath;
            RegisterResolverAssemblies(nextAssembliesByModuleId);
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
            CurrentModuleKindsByModuleId = new Dictionary<string, EditorCodeModuleKind>(StringComparer.OrdinalIgnoreCase);
            CurrentSnapshotRootDirectoryPath = null;
            ScriptTypeResolverValue.Clear();
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
        /// Returns the project-authored editor commands discovered from the currently loaded editor module assemblies.
        /// </summary>
        /// <returns>Discovered editor command descriptors.</returns>
        public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
            if (CurrentAssembliesByModuleId.Count == 0) {
                return Array.Empty<EditorProjectCommandDescriptor>();
            }

            List<EditorProjectCommandDescriptor> commands = [];
            foreach (KeyValuePair<string, Assembly> entry in CurrentAssembliesByModuleId) {
                if (!CurrentModuleKindsByModuleId.TryGetValue(entry.Key, out EditorCodeModuleKind moduleKind)
                    || moduleKind != EditorCodeModuleKind.Editor) {
                    continue;
                }

                Type[] types = entry.Value.GetTypes();
                for (int index = 0; index < types.Length; index++) {
                    Type candidateType = types[index];
                    if (candidateType.IsAbstract || !typeof(IEditorCommand).IsAssignableFrom(candidateType)) {
                        continue;
                    }

                    IEditorCommand command = (IEditorCommand)(Activator.CreateInstance(candidateType)
                        ?? throw new InvalidOperationException($"Editor command type '{candidateType.FullName}' could not be instantiated."));
                    commands.Add(new EditorProjectCommandDescriptor(command.CommandId, command.DisplayName, candidateType, entry.Key));
                }
            }

            commands.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            return commands;
        }

        /// <summary>
        /// Returns the project-authored editor menu items discovered from the currently loaded editor module assemblies.
        /// </summary>
        /// <returns>Discovered editor menu item descriptors.</returns>
        public IReadOnlyList<EditorMenuItemDescriptor> GetAvailableEditorMenuItems() {
            if (CurrentAssembliesByModuleId.Count == 0) {
                return Array.Empty<EditorMenuItemDescriptor>();
            }

            List<EditorMenuItemDescriptor> items = [];
            foreach (KeyValuePair<string, Assembly> entry in CurrentAssembliesByModuleId) {
                if (!CurrentModuleKindsByModuleId.TryGetValue(entry.Key, out EditorCodeModuleKind moduleKind)
                    || moduleKind != EditorCodeModuleKind.Editor) {
                    continue;
                }

                Type[] types = entry.Value.GetTypes();
                for (int index = 0; index < types.Length; index++) {
                    Type candidateType = types[index];
                    if (candidateType.IsAbstract || !typeof(IEditorMenuItemProvider).IsAssignableFrom(candidateType)) {
                        continue;
                    }

                    IEditorMenuItemProvider provider = (IEditorMenuItemProvider)(Activator.CreateInstance(candidateType)
                        ?? throw new InvalidOperationException($"Editor menu item provider type '{candidateType.FullName}' could not be instantiated."));
                    IReadOnlyList<EditorMenuItemDescriptor> providerItems = provider.GetMenuItems()
                        ?? throw new InvalidOperationException($"Editor menu item provider type '{candidateType.FullName}' returned null.");
                    for (int itemIndex = 0; itemIndex < providerItems.Count; itemIndex++) {
                        items.Add(providerItems[itemIndex] ?? throw new InvalidOperationException($"Editor menu item provider type '{candidateType.FullName}' returned a null menu descriptor."));
                    }
                }
            }

            ValidateMenuItemDescriptors(items);
            items.Sort(static (left, right) => {
                int topLevelOrderComparison = left.TopLevelMenuOrder.CompareTo(right.TopLevelMenuOrder);
                if (topLevelOrderComparison != 0) {
                    return topLevelOrderComparison;
                }

                int topLevelLabelComparison = string.Compare(left.TopLevelMenuLabel, right.TopLevelMenuLabel, StringComparison.Ordinal);
                if (topLevelLabelComparison != 0) {
                    return topLevelLabelComparison;
                }

                int itemOrderComparison = left.MenuItemOrder.CompareTo(right.MenuItemOrder);
                if (itemOrderComparison != 0) {
                    return itemOrderComparison;
                }

                return string.Compare(left.MenuItemLabel, right.MenuItemLabel, StringComparison.Ordinal);
            });
            return items;
        }

        /// <summary>
        /// Validates the supplied script assembly descriptors before loading begins.
        /// </summary>
        /// <param name="assemblies">Descriptors to validate.</param>
        void ValidateAssemblies(IReadOnlyList<EditorScriptAssemblyDescriptor> assemblies) {
            HashSet<string> moduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < assemblies.Count; index++) {
                EditorScriptAssemblyDescriptor descriptor = assemblies[index];
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
        /// Validates one fully materialized contributed menu item list.
        /// </summary>
        /// <param name="items">Contributed menu item descriptors to validate.</param>
        void ValidateMenuItemDescriptors(IReadOnlyList<EditorMenuItemDescriptor> items) {
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }

            Dictionary<string, EditorMenuItemDescriptor> itemsById = new Dictionary<string, EditorMenuItemDescriptor>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> topLevelLabelsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < items.Count; index++) {
                EditorMenuItemDescriptor item = items[index];
                if (!itemsById.TryAdd(item.MenuItemId, item)) {
                    throw new InvalidOperationException($"Editor menu item id '{item.MenuItemId}' was contributed more than once.");
                }

                if (topLevelLabelsById.TryGetValue(item.TopLevelMenuId, out string existingLabel)
                    && !string.Equals(existingLabel, item.TopLevelMenuLabel, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Editor top-level menu id '{item.TopLevelMenuId}' was contributed with conflicting labels.");
                }

                topLevelLabelsById[item.TopLevelMenuId] = item.TopLevelMenuLabel;
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

            List<WeakReference> references = BeginUnloadContexts(loadContextsByModuleId);
            for (int index = 0; index < references.Count; index++) {
                WaitForUnload(references[index]);
            }
        }


        /// <summary>
        /// Initiates unload for each tracked collectible context and clears the owning dictionary before collection polling begins.
        /// </summary>
        /// <param name="loadContextsByModuleId">Load contexts keyed by module id.</param>
        /// <returns>Weak references that can be polled until each context is collected.</returns>
        List<WeakReference> BeginUnloadContexts(Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> loadContextsByModuleId) {
            if (loadContextsByModuleId == null) {
                throw new ArgumentNullException(nameof(loadContextsByModuleId));
            }

            List<WeakReference> references = new List<WeakReference>(loadContextsByModuleId.Count);
            foreach (EditorCollectibleScriptAssemblyLoadContext loadContext in loadContextsByModuleId.Values) {
                WeakReference loadContextReference = BeginUnload(loadContext);
                if (loadContextReference != null) {
                    references.Add(loadContextReference);
                }
            }

            loadContextsByModuleId.Clear();
            return references;
        }

        /// <summary>
        /// Registers one loaded module assembly table with the shared script type resolver.
        /// </summary>
        /// <param name="assembliesByModuleId">Loaded assemblies keyed by module id.</param>
        void RegisterResolverAssemblies(Dictionary<string, Assembly> assembliesByModuleId) {
            if (assembliesByModuleId == null) {
                throw new ArgumentNullException(nameof(assembliesByModuleId));
            }

            foreach (KeyValuePair<string, Assembly> entry in assembliesByModuleId) {
                ScriptTypeResolverValue.Register(entry.Key, entry.Value);
            }
        }
    }
}
