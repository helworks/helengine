using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists build configuration split across two files. `settings/build_config.json` is the
    /// project-shared scene package: which scenes each platform ships and in what order. `user_settings/build_config.json`
    /// is editor-local state: output folders, debug and environment selections, profile selections, the build queue and,
    /// per platform, an optional local scene override for a developer who wants to build a subset. Callers work with one
    /// composed <see cref="EditorBuildConfigDocument"/>; this service splits it again on save.
    /// </summary>
    public sealed class EditorBuildConfigService {
        /// <summary>
        /// Gets the JSON formatting rules used for both build configuration documents.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        static EditorBuildConfigService() {
            JsonSerializerOptions.Converters.Add(new SceneAssetReferenceJsonConverter());
        }

        /// <summary>
        /// Gets the absolute path to the current project root directory.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Gets the absolute path to the editor-local `user_settings/build_config.json`.
        /// </summary>
        string LocalBuildConfigFilePath {
            get {
                return Path.Combine(ProjectRootPath, "user_settings", "build_config.json");
            }
        }

        /// <summary>
        /// Gets the absolute path to the project-shared `settings/build_config.json`.
        /// </summary>
        string ProjectBuildConfigFilePath {
            get {
                return Path.Combine(ProjectRootPath, "settings", "build_config.json");
            }
        }

        /// <summary>
        /// Initializes one build-config service for the supplied project root directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative path to the current project root directory.</param>
        public EditorBuildConfigService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Attempts to load the composed build configuration without seeding new platform entries.
        /// </summary>
        /// <returns>Composed build configuration, or null when neither the project nor the local file is present and readable.</returns>
        public EditorBuildConfigDocument TryLoadExisting() {
            EditorProjectBuildConfigDocument projectDocument = TryLoadProjectDocument(out _);
            EditorBuildConfigDocument localDocument = TryLoadLocalDocument(out bool localChanged);
            if (projectDocument == null && localDocument == null) {
                return null;
            }

            EditorBuildConfigDocument document = Compose(projectDocument, localDocument);
            if (localChanged) {
                WriteLocalDocument(document);
            }

            return document;
        }

        /// <summary>
        /// Attempts to load the project-shared scene package on its own, with scene identifiers resolved.
        /// </summary>
        /// <returns>Project scene package, or null when `settings/build_config.json` is missing or unreadable.</returns>
        public EditorProjectBuildConfigDocument TryLoadProjectBuildConfig() {
            return TryLoadProjectDocument(out _);
        }

        /// <summary>
        /// Loads the composed build configuration, seeding newly enabled platforms and creating whichever file is missing.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <param name="currentSceneId">Project-relative scene identifier used to seed first-time platform packages.</param>
        /// <returns>Validated composed build configuration for the current project.</returns>
        public EditorBuildConfigDocument Load(IReadOnlyList<string> supportedPlatforms, string currentSceneId) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            EditorProjectBuildConfigDocument projectDocument = TryLoadProjectDocument(out bool projectMalformed);
            EditorBuildConfigDocument localDocument = TryLoadLocalDocument(out bool localChanged);
            EditorBuildConfigDocument document = Compose(projectDocument, localDocument);

            bool changed = EnsurePlatformEntries(document, supportedPlatforms, currentSceneId) || localChanged;
            bool projectFileMissing = !projectMalformed && !File.Exists(ProjectBuildConfigFilePath);
            if (projectFileMissing || !File.Exists(LocalBuildConfigFilePath) || changed) {
                Save(document);
            }

            return document;
        }

        /// <summary>
        /// Persists the composed document: scene packages of platforms that follow the project go to
        /// `settings/build_config.json`, everything else to `user_settings/build_config.json`.
        /// </summary>
        /// <param name="document">Composed build configuration to persist.</param>
        public void Save(EditorBuildConfigDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            SynchronizeSceneReferences(document);
            NormalizeDocument(document);
            WriteProjectDocument(document);
            WriteLocalDocument(document);
        }

        /// <summary>
        /// Drops one platform's local scene override and reloads its scenes from the project-shared package.
        /// </summary>
        /// <param name="document">Composed build configuration being edited.</param>
        /// <param name="platformId">Platform whose scenes should follow the project again.</param>
        public void ResetPlatformScenesToProject(EditorBuildConfigDocument document, string platformId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorBuildPlatformConfigDocument platform = FindPlatformEntry(document.Platforms, platformId);
            if (platform == null) {
                throw new InvalidOperationException($"No build settings exist for platform '{platformId}'.");
            }

            platform.OverridesProjectScenes = false;
            EditorProjectBuildConfigDocument projectDocument = TryLoadProjectDocument(out _);
            ApplyProjectScenes(platform, FindProjectPlatformEntry(projectDocument, platformId));
        }

        /// <summary>
        /// Merges the project scene packages into the local document to form the composed view callers edit.
        /// Platforms that follow the project receive its scenes; platforms with a local override keep their own.
        /// </summary>
        /// <param name="projectDocument">Project scene packages, or null when the file is absent.</param>
        /// <param name="localDocument">Local build state, or null when the file is absent.</param>
        /// <returns>Composed build configuration.</returns>
        EditorBuildConfigDocument Compose(EditorProjectBuildConfigDocument projectDocument, EditorBuildConfigDocument localDocument) {
            EditorBuildConfigDocument document = localDocument ?? new EditorBuildConfigDocument();
            document.Platforms ??= [];
            document.QueueItems ??= [];

            if (projectDocument != null && projectDocument.Platforms != null) {
                for (int index = 0; index < projectDocument.Platforms.Count; index++) {
                    EditorProjectPlatformBuildConfigDocument projectPlatform = projectDocument.Platforms[index];
                    if (projectPlatform == null || string.IsNullOrWhiteSpace(projectPlatform.PlatformId)) {
                        continue;
                    }
                    if (!HasPlatformEntry(document.Platforms, projectPlatform.PlatformId)) {
                        document.Platforms.Add(CreatePlatformDocument(projectPlatform.PlatformId, null));
                    }
                }
            }

            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                if (platform == null || platform.OverridesProjectScenes) {
                    continue;
                }

                ApplyProjectScenes(platform, FindProjectPlatformEntry(projectDocument, platform.PlatformId));
            }

            return document;
        }

        /// <summary>
        /// Replaces one platform's scenes and orders with copies of the project package, or clears them when the
        /// project has no package for the platform.
        /// </summary>
        /// <param name="platform">Composed platform entry to update.</param>
        /// <param name="projectPlatform">Project scene package for the platform, or null.</param>
        static void ApplyProjectScenes(EditorBuildPlatformConfigDocument platform, EditorProjectPlatformBuildConfigDocument projectPlatform) {
            platform.SelectedSceneIds = [];
            platform.SelectedSceneReferences = [];
            platform.SceneOrders = [];
            if (projectPlatform == null) {
                return;
            }

            if (projectPlatform.SelectedSceneIds != null) {
                platform.SelectedSceneIds.AddRange(projectPlatform.SelectedSceneIds);
            }
            if (projectPlatform.SelectedSceneReferences != null) {
                platform.SelectedSceneReferences.AddRange(projectPlatform.SelectedSceneReferences);
            }
            if (projectPlatform.SceneOrders != null) {
                for (int index = 0; index < projectPlatform.SceneOrders.Count; index++) {
                    EditorBuildSceneOrderDocument order = projectPlatform.SceneOrders[index];
                    if (order == null) {
                        continue;
                    }

                    platform.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                        SceneId = order.SceneId,
                        SceneReference = order.SceneReference,
                        OrderNumber = order.OrderNumber
                    });
                }
            }
        }

        /// <summary>
        /// Writes the scene packages of every platform that follows the project into `settings/build_config.json`,
        /// keeping packages of platforms absent from the composed document. A project file that exists but cannot be
        /// parsed is authored content and is left alone so its author can repair it.
        /// </summary>
        /// <param name="document">Composed build configuration being saved.</param>
        void WriteProjectDocument(EditorBuildConfigDocument document) {
            EditorProjectBuildConfigDocument projectDocument = TryLoadProjectDocument(out bool projectMalformed);
            if (projectMalformed) {
                return;
            }

            projectDocument ??= new EditorProjectBuildConfigDocument();
            projectDocument.Platforms ??= [];
            bool hasFollowingPlatform = false;
            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                if (platform == null || string.IsNullOrWhiteSpace(platform.PlatformId) || platform.OverridesProjectScenes) {
                    continue;
                }

                hasFollowingPlatform = true;
                EditorProjectPlatformBuildConfigDocument projectPlatform = FindProjectPlatformEntry(projectDocument, platform.PlatformId);
                if (projectPlatform == null) {
                    projectPlatform = new EditorProjectPlatformBuildConfigDocument {
                        PlatformId = platform.PlatformId
                    };
                    projectDocument.Platforms.Add(projectPlatform);
                }

                projectPlatform.SelectedSceneIds = new List<string>(platform.SelectedSceneIds ?? []);
                projectPlatform.SelectedSceneReferences = new List<SceneAssetReference>(platform.SelectedSceneReferences ?? []);
                projectPlatform.SceneOrders = [];
                for (int orderIndex = 0; orderIndex < platform.SceneOrders.Count; orderIndex++) {
                    EditorBuildSceneOrderDocument order = platform.SceneOrders[orderIndex];
                    if (order == null) {
                        continue;
                    }

                    projectPlatform.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                        SceneId = order.SceneId,
                        SceneReference = order.SceneReference,
                        OrderNumber = order.OrderNumber
                    });
                }
            }

            if (!hasFollowingPlatform && !File.Exists(ProjectBuildConfigFilePath)) {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ProjectBuildConfigFilePath));
            string json = JsonSerializer.Serialize(projectDocument, JsonSerializerOptions);
            File.WriteAllText(ProjectBuildConfigFilePath, json);
        }

        /// <summary>
        /// Writes the local document. Platforms that follow the project are written without scenes so the package
        /// has exactly one home; their in-memory lists are restored after serialization.
        /// </summary>
        /// <param name="document">Composed build configuration being saved.</param>
        void WriteLocalDocument(EditorBuildConfigDocument document) {
            List<EditorBuildPlatformConfigDocument> strippedPlatforms = [];
            List<List<SceneAssetReference>> stashedReferences = [];
            List<List<EditorBuildSceneOrderDocument>> stashedOrders = [];
            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                if (platform == null || platform.OverridesProjectScenes) {
                    continue;
                }

                strippedPlatforms.Add(platform);
                stashedReferences.Add(platform.SelectedSceneReferences);
                stashedOrders.Add(platform.SceneOrders);
                platform.SelectedSceneReferences = [];
                platform.SceneOrders = [];
            }

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(LocalBuildConfigFilePath));
                string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
                File.WriteAllText(LocalBuildConfigFilePath, json);
            } finally {
                for (int index = 0; index < strippedPlatforms.Count; index++) {
                    strippedPlatforms[index].SelectedSceneReferences = stashedReferences[index];
                    strippedPlatforms[index].SceneOrders = stashedOrders[index];
                }
            }
        }

        /// <summary>
        /// Attempts to load the project scene packages from disk, resolving scene identifiers from the references.
        /// </summary>
        /// <param name="malformed">Set to true when the file exists but cannot be parsed.</param>
        /// <returns>Project scene packages, or null when the file is missing or malformed.</returns>
        EditorProjectBuildConfigDocument TryLoadProjectDocument(out bool malformed) {
            malformed = false;
            if (!File.Exists(ProjectBuildConfigFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(ProjectBuildConfigFilePath);
                EditorProjectBuildConfigDocument document = JsonSerializer.Deserialize<EditorProjectBuildConfigDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    malformed = true;
                    return null;
                }

                document.Platforms ??= [];
                for (int index = 0; index < document.Platforms.Count; index++) {
                    EditorProjectPlatformBuildConfigDocument platform = document.Platforms[index];
                    if (platform == null) {
                        document.Platforms[index] = new EditorProjectPlatformBuildConfigDocument();
                        continue;
                    }

                    platform.SelectedSceneReferences ??= [];
                    platform.SceneOrders ??= [];
                    platform.SelectedSceneIds = ResolveSceneIds(platform.SelectedSceneReferences);
                    ResolveSceneOrderIds(platform.SceneOrders);
                }

                return document;
            } catch {
                malformed = true;
                return null;
            }
        }

        /// <summary>
        /// Attempts to load the local build document from disk, normalizing current-format fields.
        /// </summary>
        /// <param name="changed">Set to true when normalization altered the document and it should be written back.</param>
        /// <returns>Local build document, or null when the file is missing or malformed.</returns>
        EditorBuildConfigDocument TryLoadLocalDocument(out bool changed) {
            changed = false;
            if (!File.Exists(LocalBuildConfigFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(LocalBuildConfigFilePath);
                EditorBuildConfigDocument document = JsonSerializer.Deserialize<EditorBuildConfigDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    return null;
                }

                document.Platforms ??= [];
                document.QueueItems ??= [];
                for (int index = 0; index < document.Platforms.Count; index++) {
                    EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                    if (platform == null) {
                        document.Platforms[index] = new EditorBuildPlatformConfigDocument();
                        changed = true;
                        continue;
                    }

                    platform.SelectedSceneIds ??= [];
                    platform.SelectedSceneReferences ??= [];
                    platform.SceneOrders ??= [];
                    platform.SelectedSceneIds = ResolveSceneIds(platform.SelectedSceneReferences);
                    ResolveSceneOrderIds(platform.SceneOrders);
                    platform.SelectedBuildProfileId ??= string.Empty;
                    platform.EditorPrebuildCommandIdsByBuildProfileId ??= [];
                    platform.SelectedGraphicsProfileId ??= string.Empty;
                    platform.SelectedBuildOptionValues ??= [];
                    platform.SelectedGraphicsOptionValues ??= [];
                    platform.SelectedCodegenProfileId ??= string.Empty;
                    platform.SelectedStorageProfileId ??= string.Empty;
                    platform.SelectedMediaProfileId ??= string.Empty;
                    platform.SelectedCodegenOptionValues ??= [];
                    if (string.IsNullOrWhiteSpace(platform.SelectedEnvironmentId)) {
                        platform.SelectedEnvironmentId = "release";
                        changed = true;
                    } else {
                        platform.SelectedEnvironmentId = platform.SelectedEnvironmentId.Trim();
                    }
                    changed |= NormalizeCurrentPlatform(platform);
                }

                for (int index = 0; index < document.QueueItems.Count; index++) {
                    EditorBuildQueueItemDocument queueItem = document.QueueItems[index];
                    if (queueItem == null) {
                        document.QueueItems[index] = new EditorBuildQueueItemDocument();
                        changed = true;
                        continue;
                    }

                    queueItem.SelectedSceneIds ??= [];
                    queueItem.SelectedSceneReferences ??= [];
                    queueItem.SelectedSceneIds = ResolveSceneIds(queueItem.SelectedSceneReferences);
                    queueItem.SelectedBuildProfileId ??= string.Empty;
                    queueItem.SelectedGraphicsProfileId ??= string.Empty;
                    queueItem.SelectedBuildOptionValues ??= [];
                    queueItem.SelectedGraphicsOptionValues ??= [];
                    queueItem.SelectedCodegenProfileId ??= string.Empty;
                    queueItem.SelectedStorageProfileId ??= string.Empty;
                    queueItem.SelectedMediaProfileId ??= string.Empty;
                    queueItem.SelectedCodegenOptionValues ??= [];
                    if (string.IsNullOrWhiteSpace(queueItem.SelectedEnvironmentId)) {
                        queueItem.SelectedEnvironmentId = "release";
                        changed = true;
                    } else {
                        queueItem.SelectedEnvironmentId = queueItem.SelectedEnvironmentId.Trim();
                    }
                    changed |= NormalizeCurrentQueueItem(queueItem);
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Synchronizes persisted scene references from the editor's operational scene-id lists. Every id in the
        /// document is captured in one batch so the asset identity index is initialized once per save.
        /// </summary>
        void SynchronizeSceneReferences(EditorBuildConfigDocument document) {
            document.Platforms ??= [];
            document.QueueItems ??= [];
            List<string> sceneIds = [];
            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                if (platform == null) {
                    continue;
                }
                platform.SelectedSceneIds ??= [];
                platform.SceneOrders ??= [];
                sceneIds.AddRange(platform.SelectedSceneIds);
                for (int orderIndex = 0; orderIndex < platform.SceneOrders.Count; orderIndex++) {
                    EditorBuildSceneOrderDocument order = platform.SceneOrders[orderIndex];
                    if (order != null && !string.IsNullOrWhiteSpace(order.SceneId)) {
                        sceneIds.Add(order.SceneId);
                    }
                }
            }
            for (int index = 0; index < document.QueueItems.Count; index++) {
                EditorBuildQueueItemDocument queueItem = document.QueueItems[index];
                if (queueItem == null) {
                    continue;
                }
                queueItem.SelectedSceneIds ??= [];
                sceneIds.AddRange(queueItem.SelectedSceneIds);
            }

            List<SceneAssetReference> references = CreateSceneCatalogService().CreateSceneReferences(sceneIds);
            int cursor = 0;
            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                if (platform == null) {
                    continue;
                }
                platform.SelectedSceneReferences = references.GetRange(cursor, platform.SelectedSceneIds.Count);
                cursor += platform.SelectedSceneIds.Count;
                for (int orderIndex = 0; orderIndex < platform.SceneOrders.Count; orderIndex++) {
                    EditorBuildSceneOrderDocument order = platform.SceneOrders[orderIndex];
                    if (order != null && !string.IsNullOrWhiteSpace(order.SceneId)) {
                        order.SceneReference = references[cursor];
                        cursor++;
                    }
                }
            }
            for (int index = 0; index < document.QueueItems.Count; index++) {
                EditorBuildQueueItemDocument queueItem = document.QueueItems[index];
                if (queueItem == null) {
                    continue;
                }
                queueItem.SelectedSceneReferences = references.GetRange(cursor, queueItem.SelectedSceneIds.Count);
                cursor += queueItem.SelectedSceneIds.Count;
            }
        }

        /// <summary>Resolves operational ids from one current persisted reference list in one batch.</summary>
        List<string> ResolveSceneIds(IReadOnlyList<SceneAssetReference> references) {
            return CreateSceneCatalogService().ResolveSceneIds(references ?? Array.Empty<SceneAssetReference>());
        }

        /// <summary>Resolves the operational id of every order entry that carries a reference, in one batch.</summary>
        /// <param name="orders">Scene order entries to update in place.</param>
        void ResolveSceneOrderIds(List<EditorBuildSceneOrderDocument> orders) {
            List<EditorBuildSceneOrderDocument> referencedOrders = [];
            List<SceneAssetReference> references = [];
            for (int index = 0; index < orders.Count; index++) {
                EditorBuildSceneOrderDocument order = orders[index];
                if (order?.SceneReference != null) {
                    referencedOrders.Add(order);
                    references.Add(order.SceneReference);
                }
            }

            List<string> sceneIds = ResolveSceneIds(references);
            for (int index = 0; index < referencedOrders.Count; index++) {
                referencedOrders[index].SceneId = sceneIds[index];
            }
        }

        EditorProjectSceneCatalogService CreateSceneCatalogService() {
            return new EditorProjectSceneCatalogService(ProjectRootPath);
        }

        /// <summary>
        /// Ensures the composed document contains one platform entry for each supported platform.
        /// </summary>
        /// <param name="document">Composed build configuration to normalize.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <param name="currentSceneId">Project-relative scene identifier used when seeding new platform packages.</param>
        /// <returns>True when the document changed; otherwise false.</returns>
        bool EnsurePlatformEntries(EditorBuildConfigDocument document, IReadOnlyList<string> supportedPlatforms, string currentSceneId) {
            bool changed = false;

            if (document.Platforms == null) {
                document.Platforms = [];
                changed = true;
            }

            if (document.QueueItems == null) {
                document.QueueItems = [];
                changed = true;
            }

            for (int i = 0; i < supportedPlatforms.Count; i++) {
                string platformId = supportedPlatforms[i];
                if (HasPlatformEntry(document.Platforms, platformId)) {
                    continue;
                }

                document.Platforms.Add(CreatePlatformDocument(platformId, currentSceneId));
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Normalizes one composed document before it is returned to callers or persisted to disk.
        /// </summary>
        /// <param name="document">Composed build configuration to normalize.</param>
        void NormalizeDocument(EditorBuildConfigDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            document.Platforms ??= [];
            document.QueueItems ??= [];
            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                if (platform != null) {
                    NormalizeCurrentPlatform(platform);
                }
            }

            for (int index = 0; index < document.QueueItems.Count; index++) {
                EditorBuildQueueItemDocument queueItem = document.QueueItems[index];
                if (queueItem != null) {
                    queueItem.SelectedEnvironmentId = string.IsNullOrWhiteSpace(queueItem.SelectedEnvironmentId)
                        ? "release"
                        : queueItem.SelectedEnvironmentId.Trim();
                    NormalizeCurrentQueueItem(queueItem);
                }
            }
        }

        /// <summary>
        /// Normalizes current environment metadata in one platform configuration record.
        /// </summary>
        /// <param name="platform">Platform configuration record to normalize.</param>
        static bool NormalizeCurrentPlatform(EditorBuildPlatformConfigDocument platform) {
            if (platform == null) {
                throw new ArgumentNullException(nameof(platform));
            }

            string currentEnvironmentId = string.IsNullOrWhiteSpace(platform.SelectedEnvironmentId)
                ? "release"
                : platform.SelectedEnvironmentId.Trim();
            bool changed = !string.Equals(platform.SelectedEnvironmentId, currentEnvironmentId, StringComparison.Ordinal);
            platform.SelectedEnvironmentId = currentEnvironmentId;
            return changed;
        }

        /// <summary>
        /// Normalizes current environment metadata in one persisted queued build record.
        /// </summary>
        /// <param name="queueItem">Queued build record to normalize.</param>
        static bool NormalizeCurrentQueueItem(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            string currentEnvironmentId = string.IsNullOrWhiteSpace(queueItem.SelectedEnvironmentId)
                ? "release"
                : queueItem.SelectedEnvironmentId.Trim();
            bool changed = !string.Equals(queueItem.SelectedEnvironmentId, currentEnvironmentId, StringComparison.Ordinal);
            queueItem.SelectedEnvironmentId = currentEnvironmentId;
            return changed;
        }

        /// <summary>
        /// Returns true when the supplied platform collection already contains an entry for the requested platform identifier.
        /// </summary>
        /// <param name="platforms">Platform configuration collection to inspect.</param>
        /// <param name="platformId">Platform identifier to search for.</param>
        /// <returns>True when a matching platform configuration already exists; otherwise false.</returns>
        static bool HasPlatformEntry(IReadOnlyList<EditorBuildPlatformConfigDocument> platforms, string platformId) {
            return FindPlatformEntry(platforms, platformId) != null;
        }

        /// <summary>
        /// Finds one composed platform entry by platform identifier.
        /// </summary>
        /// <param name="platforms">Platform configuration collection to inspect.</param>
        /// <param name="platformId">Platform identifier to search for.</param>
        /// <returns>Matching platform configuration, or null.</returns>
        static EditorBuildPlatformConfigDocument FindPlatformEntry(IReadOnlyList<EditorBuildPlatformConfigDocument> platforms, string platformId) {
            if (platforms == null) {
                return null;
            }

            for (int i = 0; i < platforms.Count; i++) {
                EditorBuildPlatformConfigDocument platform = platforms[i];
                if (platform != null && string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platform;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds one project scene package by platform identifier.
        /// </summary>
        /// <param name="projectDocument">Project scene packages, or null.</param>
        /// <param name="platformId">Platform identifier to search for.</param>
        /// <returns>Matching scene package, or null.</returns>
        static EditorProjectPlatformBuildConfigDocument FindProjectPlatformEntry(EditorProjectBuildConfigDocument projectDocument, string platformId) {
            if (projectDocument == null || projectDocument.Platforms == null) {
                return null;
            }

            for (int i = 0; i < projectDocument.Platforms.Count; i++) {
                EditorProjectPlatformBuildConfigDocument platform = projectDocument.Platforms[i];
                if (platform != null && string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platform;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates one default composed entry for the supplied platform identifier. The entry follows the project
        /// package, so a seeded current scene is written to the project file on the next save.
        /// </summary>
        /// <param name="platformId">Platform identifier the new configuration belongs to.</param>
        /// <param name="currentSceneId">Project-relative scene identifier used for first-time seeding, or null.</param>
        /// <returns>New platform configuration document seeded for first-time use.</returns>
        static EditorBuildPlatformConfigDocument CreatePlatformDocument(string platformId, string currentSceneId) {
            EditorBuildPlatformConfigDocument document = new EditorBuildPlatformConfigDocument {
                PlatformId = platformId,
                OutputDirectoryPath = string.Empty,
                DebugBuild = false,
                OverridesProjectScenes = false,
                SelectedBuildProfileId = string.Empty,
                SelectedGraphicsProfileId = string.Empty,
                SelectedBuildOptionValues = [],
                SelectedGraphicsOptionValues = [],
                SelectedCodegenProfileId = string.Empty,
                SelectedStorageProfileId = string.Empty,
                SelectedMediaProfileId = string.Empty,
                SelectedCodegenOptionValues = []
            };

            if (!string.IsNullOrWhiteSpace(currentSceneId)) {
                document.SelectedSceneIds.Add(currentSceneId);
            }

            return document;
        }
    }
}
