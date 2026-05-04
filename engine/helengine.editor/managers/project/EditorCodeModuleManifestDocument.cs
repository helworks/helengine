using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Describes one authored code module declared by the game project.
    /// </summary>
    public sealed class EditorCodeModuleManifestEntry {
        public EditorCodeModuleManifestEntry(
            string moduleId,
            string folderPath,
            string[] dependencyModuleIds,
            string[] loadScopes)
            : this(moduleId, folderPath, dependencyModuleIds, loadScopes, []) {
        }

        [JsonConstructor]
        public EditorCodeModuleManifestEntry(
            string moduleId,
            string folderPath,
            string[] dependencyModuleIds,
            string[] loadScopes,
            string[] nestedModuleFolderPaths) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Code module id must be provided.", nameof(moduleId));
            }
            if (string.IsNullOrWhiteSpace(folderPath)) {
                throw new ArgumentException("Code module folder path must be provided.", nameof(folderPath));
            }
            if (dependencyModuleIds == null) {
                throw new ArgumentNullException(nameof(dependencyModuleIds));
            }
            if (loadScopes == null) {
                throw new ArgumentNullException(nameof(loadScopes));
            }
            if (nestedModuleFolderPaths == null) {
                throw new ArgumentNullException(nameof(nestedModuleFolderPaths));
            }

            ModuleId = moduleId;
            FolderPath = NormalizeFolderPath(folderPath);
            NestedModuleFolderPaths = NormalizeFolderPaths(nestedModuleFolderPaths);
            LoadScopes = [.. loadScopes];
            DependencyModuleIds = [.. dependencyModuleIds];
        }

        public string ModuleId { get; }
        public string FolderPath { get; }
        public string[] NestedModuleFolderPaths { get; }
        public string[] LoadScopes { get; }
        public string[] DependencyModuleIds { get; }

        static string NormalizeFolderPath(string folderPath) {
            return folderPath.Replace('\\', '/').TrimEnd('/');
        }

        static string[] NormalizeFolderPaths(string[] folderPaths) {
            string[] normalizedFolderPaths = new string[folderPaths.Length];
            for (int index = 0; index < folderPaths.Length; index++) {
                normalizedFolderPaths[index] = NormalizeFolderPath(folderPaths[index]);
            }

            return normalizedFolderPaths;
        }
    }

    /// <summary>
    /// Stores the project-authored runtime code-module layout consumed by the shared build graph.
    /// </summary>
    public sealed class EditorCodeModuleManifestDocument {
        [JsonConstructor]
        public EditorCodeModuleManifestDocument(EditorCodeModuleManifestEntry[] modules) {
            Modules = modules ?? [];
        }

        public EditorCodeModuleManifestEntry[] Modules { get; }
    }
}
