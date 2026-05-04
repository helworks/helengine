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
                EditorCodeModuleManifestFileRecord? manifestRecord = ReadManifestFile(manifestFilePath);
                if (manifestRecord == null) {
                    continue;
                }
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

        bool ProjectContainsAnyScripts() {
            string assetsRootPath = Path.Combine(ProjectRootPath, DefaultSourceRoot);
            return Directory.Exists(assetsRootPath)
                && Directory.EnumerateFiles(assetsRootPath, "*.cs", SearchOption.AllDirectories).Any();
        }

        static EditorCodeModuleManifestFileRecord? ReadManifestFile(string manifestFilePath) {
            string manifestJson = File.ReadAllText(manifestFilePath);
            return JsonSerializer.Deserialize<EditorCodeModuleManifestFileRecord>(manifestJson, JsonOptions);
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
