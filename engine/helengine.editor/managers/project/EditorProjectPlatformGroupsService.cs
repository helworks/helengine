using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Loads, edits, validates and persists the project platform group tree in <c>settings/platform-groups.json</c>.
    /// Shaped like <see cref="EditorProjectEnvironmentsService"/>: mutation methods edit a document in memory and the caller saves.
    /// </summary>
    public sealed class EditorProjectPlatformGroupsService {
        /// <summary>File name beneath <c>settings/</c>.</summary>
        public const string SettingsFileName = "platform-groups.json";

        /// <summary>Gets the JSON formatting rules used for the platform group settings document: indented, camelCase properties, enum names as strings.</summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        readonly string ProjectRootPath;

        string SettingsFilePath => Path.Combine(ProjectRootPath, "settings", SettingsFileName);

        /// <summary>
        /// Initializes the service for one project root.
        /// </summary>
        public EditorProjectPlatformGroupsService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads the document, seeding an empty tree with the default level order when the file is missing or malformed.
        /// Writes the seeded document back to disk when seeding was needed.
        /// </summary>
        public EditorProjectPlatformGroupsDocument Load() {
            EditorProjectPlatformGroupsDocument document = ReadCore(out bool wasSeeded);
            if (wasSeeded) {
                Save(document);
            }

            return document;
        }

        /// <summary>
        /// Reads the document, seeding an empty tree with the default level order in memory when the file is missing
        /// or malformed. Never writes to disk.
        /// </summary>
        public EditorProjectPlatformGroupsDocument Read() {
            return ReadCore(out _);
        }

        /// <summary>
        /// Reads the document from disk, normalizing it and seeding a default tree in memory as needed.
        /// </summary>
        /// <param name="wasSeeded">Set to true when the file was missing or malformed and a default tree was seeded.</param>
        EditorProjectPlatformGroupsDocument ReadCore(out bool wasSeeded) {
            EditorProjectPlatformGroupsDocument document = TryLoadDocument();
            wasSeeded = document == null;
            document ??= new EditorProjectPlatformGroupsDocument();
            Normalize(document);
            return document;
        }

        /// <summary>
        /// Normalizes and writes the document.
        /// </summary>
        public void Save(EditorProjectPlatformGroupsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Normalize(document);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(document, JsonSerializerOptions));
        }

        /// <summary>
        /// Adds a group at the root (null parent) or beneath an existing group. Rejects blank or duplicate ids.
        /// </summary>
        public void AddGroup(EditorProjectPlatformGroupsDocument document, string parentGroupId, string groupId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(groupId)) {
                throw new ArgumentException("Group id must be provided.", nameof(groupId));
            }

            string normalizedId = groupId.Trim();
            if (FindGroup(document, normalizedId) != null) {
                throw new InvalidOperationException($"Platform group '{normalizedId}' already exists.");
            }

            EditorProjectPlatformGroupDefinition group = new EditorProjectPlatformGroupDefinition { Id = normalizedId, DisplayName = normalizedId };
            if (string.IsNullOrWhiteSpace(parentGroupId)) {
                document.Groups.Add(group);
                return;
            }

            EditorProjectPlatformGroupDefinition parent = FindGroup(document, parentGroupId)
                ?? throw new InvalidOperationException($"Platform group '{parentGroupId}' was not found.");
            parent.Children.Add(group);
        }

        /// <summary>
        /// Renames a group; the display name follows when it equalled the old id.
        /// </summary>
        public void RenameGroup(EditorProjectPlatformGroupsDocument document, string groupId, string newGroupId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(newGroupId)) {
                throw new ArgumentException("Group id must be provided.", nameof(newGroupId));
            }

            EditorProjectPlatformGroupDefinition group = FindGroup(document, groupId)
                ?? throw new InvalidOperationException($"Platform group '{groupId}' was not found.");
            string normalizedId = newGroupId.Trim();
            EditorProjectPlatformGroupDefinition existing = FindGroup(document, normalizedId);
            if (existing != null && !ReferenceEquals(existing, group)) {
                throw new InvalidOperationException($"Platform group '{normalizedId}' already exists.");
            }

            if (string.Equals(group.DisplayName, group.Id, StringComparison.Ordinal)) {
                group.DisplayName = normalizedId;
            }
            group.Id = normalizedId;
        }

        /// <summary>
        /// Deletes a group and its subtree; member platforms become ungrouped.
        /// </summary>
        public void DeleteGroup(EditorProjectPlatformGroupsDocument document, string groupId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (!RemoveGroup(document.Groups, groupId)) {
                throw new InvalidOperationException($"Platform group '{groupId}' was not found.");
            }
        }

        /// <summary>
        /// Puts a platform in one group, removing it from any other group first.
        /// </summary>
        public void AssignPlatform(EditorProjectPlatformGroupsDocument document, string groupId, string platformId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorProjectPlatformGroupDefinition group = FindGroup(document, groupId)
                ?? throw new InvalidOperationException($"Platform group '{groupId}' was not found.");
            UnassignPlatform(document, platformId);
            group.PlatformIds.Add(platformId.Trim());
        }

        /// <summary>
        /// Removes a platform from whichever group holds it; no-op when ungrouped.
        /// </summary>
        public void UnassignPlatform(EditorProjectPlatformGroupsDocument document, string platformId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            for (int index = 0; index < all.Count; index++) {
                all[index].PlatformIds.RemoveAll(id => string.Equals(id, platformId?.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Rejects blank ids, duplicate group ids, group ids equal to a supported platform id, and platforms in two groups.
        /// Every message names the offending ids so the build log points at the settings entry to fix.
        /// </summary>
        public static void Validate(EditorProjectPlatformGroupsDocument document, IReadOnlyList<string> supportedPlatformIds) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }

            EditorOverrideLevelOrder.Validate(document.DefaultLevelOrder);
            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            Dictionary<string, string> groupByPlatform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> firstPlatformIdSpellingById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < all.Count; index++) {
                EditorProjectPlatformGroupDefinition group = all[index];
                if (string.IsNullOrWhiteSpace(group.Id)) {
                    throw new InvalidOperationException("Platform groups must define a non-blank id.");
                }
                if (!groupIds.Add(group.Id)) {
                    throw new InvalidOperationException($"Platform group id '{group.Id}' is defined more than once.");
                }
                for (int platformIndex = 0; platformIndex < supportedPlatformIds.Count; platformIndex++) {
                    if (string.Equals(group.Id, supportedPlatformIds[platformIndex], StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"Platform group id '{group.Id}' collides with platform id '{supportedPlatformIds[platformIndex]}'.");
                    }
                }
                for (int platformIndex = 0; platformIndex < group.PlatformIds.Count; platformIndex++) {
                    string platformId = group.PlatformIds[platformIndex];
                    if (groupByPlatform.TryGetValue(platformId, out string otherGroupId)) {
                        string firstPlatformIdSpelling = firstPlatformIdSpellingById[platformId];
                        throw new InvalidOperationException($"Platform '{firstPlatformIdSpelling}' belongs to groups '{otherGroupId}' and '{group.Id}'; a platform may belong to one group.");
                    }
                    groupByPlatform.Add(platformId, group.Id);
                    firstPlatformIdSpellingById.Add(platformId, platformId);
                }
            }
        }

        /// <summary>
        /// Finds a group anywhere in the tree by id.
        /// </summary>
        public static EditorProjectPlatformGroupDefinition FindGroup(EditorProjectPlatformGroupsDocument document, string groupId) {
            if (document == null || string.IsNullOrWhiteSpace(groupId)) {
                return null;
            }

            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            for (int index = 0; index < all.Count; index++) {
                if (string.Equals(all[index].Id, groupId.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    return all[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the group ids from the root down to the group holding the platform; empty when ungrouped.
        /// </summary>
        public static IReadOnlyList<string> FindGroupChain(EditorProjectPlatformGroupsDocument document, string platformId) {
            List<string> chain = new List<string>();
            if (document == null || string.IsNullOrWhiteSpace(platformId)) {
                return chain;
            }

            return TryBuildChain(document.Groups, platformId.Trim(), chain) ? chain : new List<string>();
        }

        /// <summary>
        /// Returns every group id in depth-first order.
        /// </summary>
        public static IReadOnlyList<string> CollectGroupIds(EditorProjectPlatformGroupsDocument document) {
            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            if (document != null) {
                CollectGroups(document.Groups, all);
            }

            List<string> ids = new List<string>(all.Count);
            for (int index = 0; index < all.Count; index++) {
                ids.Add(all[index].Id);
            }

            return ids;
        }

        /// <summary>
        /// Walks the tree depth-first, appending each visited group id to <paramref name="chain"/>, until it finds the platform.
        /// </summary>
        static bool TryBuildChain(List<EditorProjectPlatformGroupDefinition> groups, string platformId, List<string> chain) {
            for (int index = 0; index < (groups?.Count ?? 0); index++) {
                EditorProjectPlatformGroupDefinition group = groups[index];
                chain.Add(group.Id);
                for (int platformIndex = 0; platformIndex < group.PlatformIds.Count; platformIndex++) {
                    if (string.Equals(group.PlatformIds[platformIndex], platformId, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                if (TryBuildChain(group.Children, platformId, chain)) {
                    return true;
                }
                chain.RemoveAt(chain.Count - 1);
            }

            return false;
        }

        /// <summary>
        /// Flattens the tree into <paramref name="into"/> in depth-first order.
        /// </summary>
        static void CollectGroups(List<EditorProjectPlatformGroupDefinition> groups, List<EditorProjectPlatformGroupDefinition> into) {
            for (int index = 0; index < (groups?.Count ?? 0); index++) {
                if (groups[index] == null) {
                    continue;
                }
                into.Add(groups[index]);
                CollectGroups(groups[index].Children, into);
            }
        }

        /// <summary>
        /// Removes the first group matching the id from the tree, searching depth-first; returns whether one was found.
        /// </summary>
        static bool RemoveGroup(List<EditorProjectPlatformGroupDefinition> groups, string groupId) {
            for (int index = 0; index < (groups?.Count ?? 0); index++) {
                if (string.Equals(groups[index].Id, groupId?.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    groups.RemoveAt(index);
                    return true;
                }
                if (RemoveGroup(groups[index].Children, groupId)) {
                    return true;
                }
            }

            return false;
        }

        EditorProjectPlatformGroupsDocument TryLoadDocument() {
            if (!File.Exists(SettingsFilePath)) {
                return null;
            }

            try {
                return JsonSerializer.Deserialize<EditorProjectPlatformGroupsDocument>(File.ReadAllText(SettingsFilePath), JsonSerializerOptions);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Fills in defaults, trims ids and de-duplicates each group's platform list in place.
        /// </summary>
        static void Normalize(EditorProjectPlatformGroupsDocument document) {
            document.Groups ??= [];
            document.DefaultLevelOrder ??= [];
            if (document.DefaultLevelOrder.Count == 0) {
                document.DefaultLevelOrder.AddRange(EditorOverrideLevelOrder.Default);
            }

            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            for (int index = 0; index < all.Count; index++) {
                EditorProjectPlatformGroupDefinition group = all[index];
                group.Id = (group.Id ?? string.Empty).Trim();
                group.DisplayName = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id : group.DisplayName.Trim();
                group.PlatformIds ??= [];
                group.Children ??= [];
                List<string> platforms = [];
                for (int platformIndex = 0; platformIndex < group.PlatformIds.Count; platformIndex++) {
                    string platformId = (group.PlatformIds[platformIndex] ?? string.Empty).Trim();
                    if (platformId.Length > 0 && !platforms.Contains(platformId, StringComparer.OrdinalIgnoreCase)) {
                        platforms.Add(platformId);
                    }
                }
                group.PlatformIds = platforms;
            }
        }
    }
}
