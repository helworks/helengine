using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads the project-local authored code-module manifests used by the shared build graph.
    /// </summary>
    public sealed class EditorCodeModuleManifestService {
        const string ManifestFileName = "code.module.json";
        const string DefaultModuleId = "gameplay";
        const string DefaultSourceRoot = "assets";
        const string DefaultLoadScope = "always-loaded";

        readonly string ProjectRootPath;

        public EditorCodeModuleManifestService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        public string ManifestFilePath => Path.Combine(ProjectRootPath, ManifestFileName);

        public EditorCodeModuleManifestDocument Load() {
            string assetsRootPath = Path.Combine(ProjectRootPath, DefaultSourceRoot);
            if (!Directory.Exists(assetsRootPath)) {
                return new EditorCodeModuleManifestDocument([]);
            }

            EditorCodeModuleManifestEntry[] discoveredModules = LoadFolderScopedManifests(assetsRootPath);
            if (discoveredModules.Length > 0) {
                if (ProjectContainsScriptsOutsideModules(assetsRootPath, discoveredModules)) {
                    return new EditorCodeModuleManifestDocument(BuildRootFallbackManifestEntries(discoveredModules));
                }

                return new EditorCodeModuleManifestDocument(discoveredModules);
            }

            if (ProjectContainsAnyScripts()) {
                return new EditorCodeModuleManifestDocument([
                    new EditorCodeModuleManifestEntry(
                        DefaultModuleId,
                        DefaultSourceRoot,
                        [],
                        [DefaultLoadScope])
                ]);
            }

            return new EditorCodeModuleManifestDocument([]);
        }

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
                    manifestRecord.LoadScopes is { Length: > 0 } ? manifestRecord.LoadScopes : [DefaultLoadScope]));
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
                    [.. nestedFolderPaths]));
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
                    [.. discoveredModules.Select(module => module.FolderPath).OrderBy(static folderPath => folderPath, StringComparer.OrdinalIgnoreCase)])
            ];
            combinedEntries.AddRange(discoveredModules);
            return [.. combinedEntries];
        }

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

        static EditorCodeModuleManifestFileRecord ReadManifestFile(string manifestFilePath) {
            string manifestJson = File.ReadAllText(manifestFilePath);
            EditorCodeModuleManifestFileRecord manifestRecord = JsonSerializer.Deserialize<EditorCodeModuleManifestFileRecord>(manifestJson, JsonOptions);
            if (manifestRecord == null) {
                throw new InvalidOperationException($"Code module manifest '{manifestFilePath}' could not be deserialized.");
            }

            return manifestRecord;
        }

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

        static int GetFolderDepth(string folderPath) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                return 0;
            }

            return folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }

        static readonly JsonSerializerOptions JsonOptions = new() {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        sealed class EditorCodeModuleManifestFileRecord {
            public string ModuleId { get; set; } = string.Empty;
            public string[] DependencyModuleIds { get; set; } = [];
            public string[] LoadScopes { get; set; } = [];
        }
    }
}
