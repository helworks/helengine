using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads the project-local authored code-module manifests used by the shared build graph.
    /// </summary>
    public sealed class EditorCodeModuleManifestService {
        /// <summary>
        /// File name used by folder-scoped module manifests.
        /// </summary>
        const string ManifestFileName = "code.module.json";

        /// <summary>
        /// Reserved module identifier used by loose scripts beneath the project assets root.
        /// </summary>
        const string DefaultModuleId = "gameplay";

        /// <summary>
        /// Relative assets root scanned for authored code modules.
        /// </summary>
        const string DefaultSourceRoot = "assets";

        /// <summary>
        /// Default runtime residency scope applied when manifests omit one.
        /// </summary>
        const string DefaultLoadScope = "always-loaded";

        /// <summary>
        /// Absolute project root path that owns the authored assets tree.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one manifest service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorCodeModuleManifestService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Gets the absolute manifest file path rooted at the project directory.
        /// </summary>
        public string ManifestFilePath => Path.Combine(ProjectRootPath, ManifestFileName);

        /// <summary>
        /// Discovers authored code modules for the current project.
        /// </summary>
        /// <returns>Discovered authored code-module manifest document.</returns>
        public EditorCodeModuleManifestDocument Load() {
            string assetsRootPath = Path.Combine(ProjectRootPath, DefaultSourceRoot);
            if (!Directory.Exists(assetsRootPath)) {
                return new EditorCodeModuleManifestDocument([]);
            }

            EditorCodeModuleManifestEntry[] discoveredModules = LoadFolderScopedManifests(assetsRootPath);
            if (discoveredModules.Length > 0) {
                EditorCodeModuleManifestEntry[] finalModules = discoveredModules;
                if (ProjectContainsScriptsOutsideModules(assetsRootPath, discoveredModules)) {
                    finalModules = BuildRootFallbackManifestEntries(discoveredModules);
                }

                ValidateDependencyKinds(finalModules);
                return new EditorCodeModuleManifestDocument(finalModules);
            }

            if (ProjectContainsAnyScripts()) {
                EditorCodeModuleManifestEntry[] defaultModules = [
                    new EditorCodeModuleManifestEntry(
                        DefaultModuleId,
                        DefaultSourceRoot,
                        [],
                        [DefaultLoadScope])
                ];
                ValidateDependencyKinds(defaultModules);
                return new EditorCodeModuleManifestDocument(defaultModules);
            }

            return new EditorCodeModuleManifestDocument([]);
        }

        /// <summary>
        /// Loads all folder-scoped manifest entries beneath the assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root path.</param>
        /// <returns>Resolved authored module entries ordered by folder depth.</returns>
        EditorCodeModuleManifestEntry[] LoadFolderScopedManifests(string assetsRootPath) {
            List<EditorCodeModuleManifestEntry> discoveredEntries = [];
            HashSet<string> discoveredModuleIds = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> discoveredFolderPaths = new(StringComparer.OrdinalIgnoreCase);
            foreach (string manifestFilePath in Directory.EnumerateFiles(assetsRootPath, ManifestFileName, SearchOption.AllDirectories)) {
                string manifestFolderPath = Path.GetDirectoryName(manifestFilePath) ?? assetsRootPath;
                string folderPath = NormalizeRelativePath(Path.GetRelativePath(ProjectRootPath, manifestFolderPath));
                EditorCodeModuleManifestFileRecord manifestRecord = ReadManifestFile(manifestFilePath);
                if (!discoveredModuleIds.Add(manifestRecord.ModuleId)) {
                    throw new InvalidOperationException($"Duplicate code module id '{manifestRecord.ModuleId}' was discovered.");
                }
                if (!discoveredFolderPaths.Add(folderPath)) {
                    throw new InvalidOperationException($"Duplicate code module folder boundary '{folderPath}' was discovered.");
                }

                discoveredEntries.Add(new EditorCodeModuleManifestEntry(
                    manifestRecord.ModuleId,
                    folderPath,
                    manifestRecord.DependencyModuleIds ?? [],
                    manifestRecord.LoadScopes is { Length: > 0 } ? manifestRecord.LoadScopes : [DefaultLoadScope],
                    ParseModuleKind(manifestRecord.ModuleKind, manifestFilePath)));
            }

            if (discoveredEntries.Count == 0) {
                return [];
            }

            List<EditorCodeModuleManifestEntry> resolvedEntries = [];
            for (int index = 0; index < discoveredEntries.Count; index++) {
                EditorCodeModuleManifestEntry entry = discoveredEntries[index];
                List<string> nestedFolderPaths = [];
                for (int candidateIndex = 0; candidateIndex < discoveredEntries.Count; candidateIndex++) {
                    if (index == candidateIndex) {
                        continue;
                    }

                    EditorCodeModuleManifestEntry candidate = discoveredEntries[candidateIndex];
                    if (IsDescendantFolder(entry.FolderPath, candidate.FolderPath)) {
                        nestedFolderPaths.Add(candidate.FolderPath);
                    }
                }

                nestedFolderPaths.Sort(StringComparer.OrdinalIgnoreCase);
                resolvedEntries.Add(new EditorCodeModuleManifestEntry(
                    entry.ModuleId,
                    entry.FolderPath,
                    entry.DependencyModuleIds,
                    entry.LoadScopes,
                    [.. nestedFolderPaths],
                    entry.ModuleKind));
            }

            resolvedEntries.Sort((left, right) => {
                int leftDepth = GetFolderDepth(left.FolderPath);
                int rightDepth = GetFolderDepth(right.FolderPath);
                int depthComparison = leftDepth.CompareTo(rightDepth);
                if (depthComparison != 0) {
                    return depthComparison;
                }

                return string.Compare(left.FolderPath, right.FolderPath, StringComparison.OrdinalIgnoreCase);
            });

            return [.. resolvedEntries];
        }

        /// <summary>
        /// Builds the manifest entry list when root-owned scripts coexist with folder-scoped modules.
        /// </summary>
        /// <param name="discoveredModules">Folder-scoped modules discovered beneath the assets root.</param>
        /// <returns>Combined manifest entry list including the root gameplay module.</returns>
        EditorCodeModuleManifestEntry[] BuildRootFallbackManifestEntries(EditorCodeModuleManifestEntry[] discoveredModules) {
            if (discoveredModules == null) {
                throw new ArgumentNullException(nameof(discoveredModules));
            }

            if (discoveredModules.Any(module => string.Equals(module.ModuleId, DefaultModuleId, StringComparison.OrdinalIgnoreCase))) {
                throw new InvalidOperationException($"Folder-scoped code module id '{DefaultModuleId}' conflicts with the reserved main project module id required by loose scripts.");
            }

            List<EditorCodeModuleManifestEntry> combinedEntries = [
                new EditorCodeModuleManifestEntry(
                    DefaultModuleId,
                    DefaultSourceRoot,
                    [],
                    [DefaultLoadScope],
                    [.. discoveredModules.Select(module => module.FolderPath).OrderBy(static folderPath => folderPath, StringComparer.OrdinalIgnoreCase)],
                    EditorCodeModuleKind.Runtime)
            ];
            combinedEntries.AddRange(discoveredModules);
            return [.. combinedEntries];
        }

        /// <summary>
        /// Determines whether any C# source files exist beneath the assets root.
        /// </summary>
        /// <returns><c>true</c> when any authored scripts are present.</returns>
        bool ProjectContainsAnyScripts() {
            string assetsRootPath = Path.Combine(ProjectRootPath, DefaultSourceRoot);
            return Directory.Exists(assetsRootPath)
                && Directory.EnumerateFiles(assetsRootPath, "*.cs", SearchOption.AllDirectories).Any();
        }

        /// <summary>
        /// Determines whether any script file remains outside all discovered folder-scoped module boundaries.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root path.</param>
        /// <param name="discoveredModules">Folder-scoped modules discovered beneath the assets root.</param>
        /// <returns><c>true</c> when at least one script belongs to the root gameplay module.</returns>
        bool ProjectContainsScriptsOutsideModules(string assetsRootPath, EditorCodeModuleManifestEntry[] discoveredModules) {
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath));
            }
            if (discoveredModules == null) {
                throw new ArgumentNullException(nameof(discoveredModules));
            }

            foreach (string scriptFilePath in Directory.EnumerateFiles(assetsRootPath, "*.cs", SearchOption.AllDirectories)) {
                string relativeScriptPath = NormalizeRelativePath(Path.GetRelativePath(ProjectRootPath, scriptFilePath));
                if (!IsOwnedByDiscoveredModule(relativeScriptPath, discoveredModules)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one script path is contained by any discovered folder-scoped module boundary.
        /// </summary>
        /// <param name="relativeScriptPath">Project-relative script file path.</param>
        /// <param name="discoveredModules">Folder-scoped modules discovered beneath the assets root.</param>
        /// <returns><c>true</c> when the script is owned by one discovered module.</returns>
        static bool IsOwnedByDiscoveredModule(string relativeScriptPath, EditorCodeModuleManifestEntry[] discoveredModules) {
            if (string.IsNullOrWhiteSpace(relativeScriptPath)) {
                throw new ArgumentException("Relative script path must be provided.", nameof(relativeScriptPath));
            }
            if (discoveredModules == null) {
                throw new ArgumentNullException(nameof(discoveredModules));
            }

            for (int index = 0; index < discoveredModules.Length; index++) {
                string moduleFolderPrefix = discoveredModules[index].FolderPath.TrimEnd('/') + "/";
                if (relativeScriptPath.StartsWith(moduleFolderPrefix, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads one manifest file from disk.
        /// </summary>
        /// <param name="manifestFilePath">Absolute manifest file path.</param>
        /// <returns>Deserialized manifest record.</returns>
        static EditorCodeModuleManifestFileRecord ReadManifestFile(string manifestFilePath) {
            string manifestJson = File.ReadAllText(manifestFilePath);
            EditorCodeModuleManifestFileRecord manifestRecord = JsonSerializer.Deserialize<EditorCodeModuleManifestFileRecord>(manifestJson, JsonOptions);
            if (manifestRecord == null) {
                throw new InvalidOperationException($"Code module manifest '{manifestFilePath}' could not be deserialized.");
            }

            return manifestRecord;
        }

        /// <summary>
        /// Parses one authored module kind string.
        /// </summary>
        /// <param name="moduleKindText">Authored module kind text from the manifest.</param>
        /// <param name="manifestFilePath">Absolute manifest file path used for error reporting.</param>
        /// <returns>Resolved authored module kind.</returns>
        static EditorCodeModuleKind ParseModuleKind(string moduleKindText, string manifestFilePath) {
            if (string.IsNullOrWhiteSpace(moduleKindText)) {
                return EditorCodeModuleKind.Runtime;
            }
            if (string.Equals(moduleKindText, "runtime", StringComparison.OrdinalIgnoreCase)) {
                return EditorCodeModuleKind.Runtime;
            }
            if (string.Equals(moduleKindText, "editor", StringComparison.OrdinalIgnoreCase)) {
                return EditorCodeModuleKind.Editor;
            }

            throw new InvalidOperationException($"Code module manifest '{manifestFilePath}' declared unsupported moduleKind '{moduleKindText}'.");
        }

        /// <summary>
        /// Validates that runtime modules do not depend on editor-only modules.
        /// </summary>
        /// <param name="modules">Resolved module entries to validate.</param>
        static void ValidateDependencyKinds(EditorCodeModuleManifestEntry[] modules) {
            Dictionary<string, EditorCodeModuleManifestEntry> modulesById = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < modules.Length; index++) {
                modulesById[modules[index].ModuleId] = modules[index];
            }

            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++) {
                EditorCodeModuleManifestEntry module = modules[moduleIndex];
                if (module.ModuleKind != EditorCodeModuleKind.Runtime) {
                    continue;
                }

                for (int dependencyIndex = 0; dependencyIndex < module.DependencyModuleIds.Length; dependencyIndex++) {
                    string dependencyModuleId = module.DependencyModuleIds[dependencyIndex];
                    if (string.IsNullOrWhiteSpace(dependencyModuleId)) {
                        continue;
                    }

                    if (modulesById.TryGetValue(dependencyModuleId, out EditorCodeModuleManifestEntry dependencyModule)
                        && dependencyModule.ModuleKind == EditorCodeModuleKind.Editor) {
                        throw new InvalidOperationException($"Runtime code module '{module.ModuleId}' cannot depend on editor code module '{dependencyModule.ModuleId}'.");
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether one folder path is nested beneath another.
        /// </summary>
        /// <param name="parentFolderPath">Candidate parent folder path.</param>
        /// <param name="candidateFolderPath">Candidate nested folder path.</param>
        /// <returns><c>true</c> when the candidate path is nested beneath the parent path.</returns>
        static bool IsDescendantFolder(string parentFolderPath, string candidateFolderPath) {
            if (string.IsNullOrWhiteSpace(parentFolderPath) || string.IsNullOrWhiteSpace(candidateFolderPath)) {
                return false;
            }

            if (string.Equals(parentFolderPath, candidateFolderPath, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string prefix = parentFolderPath.TrimEnd('/') + "/";
            return candidateFolderPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Counts the folder depth used for stable manifest ordering.
        /// </summary>
        /// <param name="folderPath">Folder path whose depth should be counted.</param>
        /// <returns>Number of path segments in the folder path.</returns>
        static int GetFolderDepth(string folderPath) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                return 0;
            }

            return folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>
        /// Normalizes one project-relative path to the persisted manifest convention.
        /// </summary>
        /// <param name="relativePath">Relative path to normalize.</param>
        /// <returns>Normalized relative path.</returns>
        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Shared JSON serializer options used when reading manifest files.
        /// </summary>
        static readonly JsonSerializerOptions JsonOptions = new() {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// Deserializable JSON shape stored on disk for one folder-scoped module manifest.
        /// </summary>
        sealed class EditorCodeModuleManifestFileRecord {
            /// <summary>
            /// Gets or sets the stable authored module identifier.
            /// </summary>
            public string ModuleId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets ordered dependency module identifiers.
            /// </summary>
            public string[] DependencyModuleIds { get; set; } = [];

            /// <summary>
            /// Gets or sets authored runtime residency scopes.
            /// </summary>
            public string[] LoadScopes { get; set; } = [];

            /// <summary>
            /// Gets or sets the optional authored module kind text.
            /// </summary>
            public string ModuleKind { get; set; } = string.Empty;
        }
    }
}
