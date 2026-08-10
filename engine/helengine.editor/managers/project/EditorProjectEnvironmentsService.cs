using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists project-defined build environments stored in `settings/environments.json`.
    /// </summary>
    public sealed class EditorProjectEnvironmentsService {
        /// <summary>
        /// Stable identifier of the protected debug environment.
        /// </summary>
        public const string DebugEnvironmentId = "debug";

        /// <summary>
        /// Stable identifier of the protected release environment.
        /// </summary>
        public const string ReleaseEnvironmentId = "release";

        /// <summary>
        /// Gets the JSON formatting rules used for the project environment settings document.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        /// <summary>
        /// Gets the absolute path to the current project root directory.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Gets the absolute path to the project environment settings file.
        /// </summary>
        string EnvironmentsFilePath {
            get {
                return Path.Combine(ProjectRootPath, "settings", "environments.json");
            }
        }

        /// <summary>
        /// Initializes one environment service for the supplied project root directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative path to the current project root directory.</param>
        public EditorProjectEnvironmentsService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads and normalizes the project environment settings, seeding protected built-ins when needed.
        /// </summary>
        /// <returns>Normalized project environment settings.</returns>
        public EditorProjectEnvironmentsDocument Load() {
            EditorProjectEnvironmentsDocument document = TryLoadDocument() ?? CreateDefaultDocument();
            Normalize(document);
            Save(document);
            return document;
        }

        /// <summary>
        /// Persists the supplied project environment settings after normalization.
        /// </summary>
        /// <param name="document">Environment settings to persist.</param>
        public void Save(EditorProjectEnvironmentsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Normalize(document);
            string settingsDirectoryPath = Path.GetDirectoryName(EnvironmentsFilePath);
            Directory.CreateDirectory(settingsDirectoryPath);
            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(EnvironmentsFilePath, json);
        }

        /// <summary>
        /// Adds one custom environment to the supplied document.
        /// </summary>
        /// <param name="document">Environment document to mutate.</param>
        /// <param name="environmentId">Identifier of the custom environment.</param>
        public void Add(EditorProjectEnvironmentsDocument document, string environmentId) {
            EnsureDocument(document);
            string normalizedEnvironmentId = NormalizeEnvironmentId(environmentId);
            if (ContainsEnvironment(document.Environments, normalizedEnvironmentId)) {
                throw new InvalidOperationException($"Environment '{normalizedEnvironmentId}' already exists.");
            }

            document.Environments.Add(new EditorProjectEnvironmentDefinition {
                Id = normalizedEnvironmentId,
                IsProtected = false
            });
        }

        /// <summary>
        /// Renames one custom environment in the supplied document.
        /// </summary>
        /// <param name="document">Environment document to mutate.</param>
        /// <param name="environmentId">Existing environment identifier.</param>
        /// <param name="newEnvironmentId">New custom environment identifier.</param>
        public void Rename(EditorProjectEnvironmentsDocument document, string environmentId, string newEnvironmentId) {
            EnsureDocument(document);
            string normalizedEnvironmentId = NormalizeEnvironmentId(environmentId);
            string normalizedNewEnvironmentId = NormalizeEnvironmentId(newEnvironmentId);
            EditorProjectEnvironmentDefinition environment = FindEnvironment(document.Environments, normalizedEnvironmentId);
            if (environment == null) {
                throw new InvalidOperationException($"Environment '{normalizedEnvironmentId}' does not exist.");
            }
            if (environment.IsProtected || IsProtectedEnvironmentId(environment.Id)) {
                throw new InvalidOperationException($"Environment '{environment.Id}' is protected and cannot be renamed.");
            }
            if (!string.Equals(normalizedEnvironmentId, normalizedNewEnvironmentId, StringComparison.OrdinalIgnoreCase)
                && ContainsEnvironment(document.Environments, normalizedNewEnvironmentId)) {
                throw new InvalidOperationException($"Environment '{normalizedNewEnvironmentId}' already exists.");
            }

            environment.Id = normalizedNewEnvironmentId;
        }

        /// <summary>
        /// Deletes one custom environment from the supplied document.
        /// </summary>
        /// <param name="document">Environment document to mutate.</param>
        /// <param name="environmentId">Environment identifier to delete.</param>
        public void Delete(EditorProjectEnvironmentsDocument document, string environmentId) {
            EnsureDocument(document);
            string normalizedEnvironmentId = NormalizeEnvironmentId(environmentId);
            EditorProjectEnvironmentDefinition environment = FindEnvironment(document.Environments, normalizedEnvironmentId);
            if (environment == null) {
                throw new InvalidOperationException($"Environment '{normalizedEnvironmentId}' does not exist.");
            }
            if (environment.IsProtected || IsProtectedEnvironmentId(environment.Id)) {
                throw new InvalidOperationException($"Environment '{environment.Id}' is protected and cannot be deleted.");
            }

            document.Environments.Remove(environment);
        }

        /// <summary>
        /// Returns whether the supplied identifier is one of the protected built-ins.
        /// </summary>
        /// <param name="environmentId">Environment identifier to inspect.</param>
        /// <returns>True when the identifier is protected.</returns>
        public static bool IsProtectedEnvironmentId(string environmentId) {
            return string.Equals(environmentId, DebugEnvironmentId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentId, ReleaseEnvironmentId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to load the project environment settings file without creating or repairing it.
        /// </summary>
        /// <returns>Loaded document, or null when the file is missing or malformed.</returns>
        EditorProjectEnvironmentsDocument TryLoadDocument() {
            if (!File.Exists(EnvironmentsFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(EnvironmentsFilePath);
                return JsonSerializer.Deserialize<EditorProjectEnvironmentsDocument>(json, JsonSerializerOptions);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Creates the default document containing protected debug and release environments.
        /// </summary>
        /// <returns>Default environment document.</returns>
        EditorProjectEnvironmentsDocument CreateDefaultDocument() {
            return new EditorProjectEnvironmentsDocument {
                Environments = [
                    CreateProtectedEnvironment(DebugEnvironmentId),
                    CreateProtectedEnvironment(ReleaseEnvironmentId)
                ]
            };
        }

        /// <summary>
        /// Normalizes the document and guarantees canonical protected built-ins.
        /// </summary>
        /// <param name="document">Document to normalize.</param>
        void Normalize(EditorProjectEnvironmentsDocument document) {
            EnsureDocument(document);
            List<EditorProjectEnvironmentDefinition> normalizedEnvironments = [
                CreateProtectedEnvironment(DebugEnvironmentId),
                CreateProtectedEnvironment(ReleaseEnvironmentId)
            ];
            HashSet<string> environmentIds = new(StringComparer.OrdinalIgnoreCase) {
                DebugEnvironmentId,
                ReleaseEnvironmentId
            };

            if (document.Environments != null) {
                for (int index = 0; index < document.Environments.Count; index++) {
                    EditorProjectEnvironmentDefinition environment = document.Environments[index];
                    if (environment == null || string.IsNullOrWhiteSpace(environment.Id)) {
                        continue;
                    }

                    string normalizedEnvironmentId = environment.Id.Trim();
                    if (!environmentIds.Add(normalizedEnvironmentId)) {
                        continue;
                    }

                    normalizedEnvironments.Add(new EditorProjectEnvironmentDefinition {
                        Id = normalizedEnvironmentId,
                        IsProtected = false
                    });
                }
            }

            document.Environments = normalizedEnvironments;
        }

        /// <summary>
        /// Ensures a document was supplied to a mutating operation.
        /// </summary>
        /// <param name="document">Document to validate.</param>
        static void EnsureDocument(EditorProjectEnvironmentsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
        }

        /// <summary>
        /// Normalizes one environment identifier and validates its required value.
        /// </summary>
        /// <param name="environmentId">Environment identifier to normalize.</param>
        /// <returns>Trimmed environment identifier.</returns>
        static string NormalizeEnvironmentId(string environmentId) {
            if (string.IsNullOrWhiteSpace(environmentId)) {
                throw new ArgumentException("Environment id must be provided.", nameof(environmentId));
            }

            return environmentId.Trim();
        }

        /// <summary>
        /// Finds an environment by case-insensitive identifier.
        /// </summary>
        /// <param name="environments">Environment collection to search.</param>
        /// <param name="environmentId">Identifier to locate.</param>
        /// <returns>Matching environment, or null when absent.</returns>
        static EditorProjectEnvironmentDefinition FindEnvironment(IReadOnlyList<EditorProjectEnvironmentDefinition> environments, string environmentId) {
            if (environments == null) {
                return null;
            }

            for (int index = 0; index < environments.Count; index++) {
                EditorProjectEnvironmentDefinition environment = environments[index];
                if (environment != null && string.Equals(environment.Id, environmentId, StringComparison.OrdinalIgnoreCase)) {
                    return environment;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether an environment collection contains one identifier.
        /// </summary>
        /// <param name="environments">Environment collection to search.</param>
        /// <param name="environmentId">Identifier to locate.</param>
        /// <returns>True when the identifier exists.</returns>
        static bool ContainsEnvironment(IReadOnlyList<EditorProjectEnvironmentDefinition> environments, string environmentId) {
            return FindEnvironment(environments, environmentId) != null;
        }

        /// <summary>
        /// Creates one protected built-in environment definition.
        /// </summary>
        /// <param name="environmentId">Built-in identifier.</param>
        /// <returns>Protected environment definition.</returns>
        static EditorProjectEnvironmentDefinition CreateProtectedEnvironment(string environmentId) {
            return new EditorProjectEnvironmentDefinition {
                Id = environmentId,
                IsProtected = true
            };
        }
    }
}
