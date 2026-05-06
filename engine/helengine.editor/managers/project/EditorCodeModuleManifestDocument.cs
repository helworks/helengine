using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Describes one authored code module declared by the game project.
    /// </summary>
    public sealed class EditorCodeModuleManifestEntry {
        /// <summary>
        /// Initializes one manifest entry with the default runtime module kind.
        /// </summary>
        /// <param name="moduleId">Stable authored module identifier.</param>
        /// <param name="folderPath">Project-relative folder path owned by the module.</param>
        /// <param name="dependencyModuleIds">Ordered dependency module identifiers.</param>
        /// <param name="loadScopes">Authored runtime residency scopes.</param>
        public EditorCodeModuleManifestEntry(
            string moduleId,
            string folderPath,
            string[] dependencyModuleIds,
            string[] loadScopes)
            : this(moduleId, folderPath, dependencyModuleIds, loadScopes, [], EditorCodeModuleKind.Runtime) {
        }

        /// <summary>
        /// Initializes one manifest entry with the supplied module kind and no nested folders.
        /// </summary>
        /// <param name="moduleId">Stable authored module identifier.</param>
        /// <param name="folderPath">Project-relative folder path owned by the module.</param>
        /// <param name="dependencyModuleIds">Ordered dependency module identifiers.</param>
        /// <param name="loadScopes">Authored runtime residency scopes.</param>
        /// <param name="moduleKind">Declares whether the module is runtime or editor-only.</param>
        public EditorCodeModuleManifestEntry(
            string moduleId,
            string folderPath,
            string[] dependencyModuleIds,
            string[] loadScopes,
            EditorCodeModuleKind moduleKind)
            : this(moduleId, folderPath, dependencyModuleIds, loadScopes, [], moduleKind) {
        }

        /// <summary>
        /// Initializes one manifest entry with the default runtime module kind.
        /// </summary>
        /// <param name="moduleId">Stable authored module identifier.</param>
        /// <param name="folderPath">Project-relative folder path owned by the module.</param>
        /// <param name="dependencyModuleIds">Ordered dependency module identifiers.</param>
        /// <param name="loadScopes">Authored runtime residency scopes.</param>
        /// <param name="nestedModuleFolderPaths">Nested module folder paths excluded from this module boundary.</param>
        public EditorCodeModuleManifestEntry(
            string moduleId,
            string folderPath,
            string[] dependencyModuleIds,
            string[] loadScopes,
            string[] nestedModuleFolderPaths)
            : this(moduleId, folderPath, dependencyModuleIds, loadScopes, nestedModuleFolderPaths, EditorCodeModuleKind.Runtime) {
        }

        /// <summary>
        /// Initializes one manifest entry with the full authored metadata.
        /// </summary>
        /// <param name="moduleId">Stable authored module identifier.</param>
        /// <param name="folderPath">Project-relative folder path owned by the module.</param>
        /// <param name="dependencyModuleIds">Ordered dependency module identifiers.</param>
        /// <param name="loadScopes">Authored runtime residency scopes.</param>
        /// <param name="nestedModuleFolderPaths">Nested module folder paths excluded from this module boundary.</param>
        /// <param name="moduleKind">Declares whether the module is runtime or editor-only.</param>
        [JsonConstructor]
        public EditorCodeModuleManifestEntry(
            string moduleId,
            string folderPath,
            string[] dependencyModuleIds,
            string[] loadScopes,
            string[] nestedModuleFolderPaths,
            EditorCodeModuleKind moduleKind) {
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
            ModuleKind = moduleKind;
        }

        /// <summary>
        /// Gets the stable authored module identifier.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the project-relative folder path owned by the module.
        /// </summary>
        public string FolderPath { get; }

        /// <summary>
        /// Gets nested module folder paths excluded from this module boundary.
        /// </summary>
        public string[] NestedModuleFolderPaths { get; }

        /// <summary>
        /// Gets authored runtime residency scopes.
        /// </summary>
        public string[] LoadScopes { get; }

        /// <summary>
        /// Gets ordered dependency module identifiers.
        /// </summary>
        public string[] DependencyModuleIds { get; }

        /// <summary>
        /// Gets whether the module participates in runtime packaging or editor-only tooling.
        /// </summary>
        public EditorCodeModuleKind ModuleKind { get; }

        /// <summary>
        /// Normalizes one folder path to the persisted manifest convention.
        /// </summary>
        /// <param name="folderPath">Folder path to normalize.</param>
        /// <returns>Normalized persisted folder path.</returns>
        static string NormalizeFolderPath(string folderPath) {
            return folderPath.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// Normalizes a folder path array to the persisted manifest convention.
        /// </summary>
        /// <param name="folderPaths">Folder paths to normalize.</param>
        /// <returns>Normalized persisted folder paths.</returns>
        static string[] NormalizeFolderPaths(string[] folderPaths) {
            string[] normalizedFolderPaths = new string[folderPaths.Length];
            for (int index = 0; index < folderPaths.Length; index++) {
                normalizedFolderPaths[index] = NormalizeFolderPath(folderPaths[index]);
            }

            return normalizedFolderPaths;
        }
    }

    /// <summary>
    /// Stores the project-authored code-module layout consumed by the editor and build graph.
    /// </summary>
    public sealed class EditorCodeModuleManifestDocument {
        /// <summary>
        /// Initializes one manifest document from discovered module entries.
        /// </summary>
        /// <param name="modules">Discovered authored code modules.</param>
        [JsonConstructor]
        public EditorCodeModuleManifestDocument(EditorCodeModuleManifestEntry[] modules) {
            Modules = modules ?? [];
        }

        /// <summary>
        /// Gets the discovered authored code modules.
        /// </summary>
        public EditorCodeModuleManifestEntry[] Modules { get; }
    }
}
