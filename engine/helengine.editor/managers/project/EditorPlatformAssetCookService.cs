using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Cooks ordered build scenes and their dependent runtime assets into packaged build-graph outputs.
    /// </summary>
    internal sealed class EditorPlatformAssetCookService {
        readonly string ProjectRootPath;
        readonly string RequiredEngineVersion;
        readonly string ProjectId;
        readonly string ProjectVersion;
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;
        readonly FontAsset DefaultFontAsset;
        readonly AssetFileHasher FileHasher;
        readonly IScriptTypeResolver ScriptTypeResolver;
        readonly EditorProjectSceneCatalogService SceneCatalogService;
        readonly EditorProfileSettingsService ProfileSettingsService;
        readonly EditorStandardPlatformInputConfigurationFactory StandardPlatformInputConfigurationFactory;

        /// <summary>
        /// Initializes one asset-cook service for the supplied project and optional script resolver.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative source project root path.</param>
        /// <param name="requiredEngineVersion">Exact engine version required by the current project build.</param>
        /// <param name="projectId">Stable project identifier reported to platform builders.</param>
        /// <param name="projectVersion">Human-visible project version reported to platform builders.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="defaultFontAsset">Default font asset packaged for player builds.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        /// <param name="fileHasher">Optional file hasher override used by tests.</param>
        public EditorPlatformAssetCookService(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            FontAsset defaultFontAsset,
            IScriptTypeResolver scriptTypeResolver = null,
            AssetFileHasher fileHasher = null) {
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath)
                ? throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath))
                : Path.GetFullPath(projectRootPath);
            RequiredEngineVersion = requiredEngineVersion ?? throw new ArgumentNullException(nameof(requiredEngineVersion));
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            ProjectVersion = projectVersion ?? throw new ArgumentNullException(nameof(projectVersion));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            DefaultFontAsset = defaultFontAsset;
            ScriptTypeResolver = scriptTypeResolver;
            FileHasher = fileHasher ?? new AssetFileHasher();
            SceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
            ProfileSettingsService = new EditorProfileSettingsService(ProjectRootPath);
            StandardPlatformInputConfigurationFactory = new EditorStandardPlatformInputConfigurationFactory();
        }

        public PlatformBuildManifest Cook(
            PlatformDefinition platformDefinition,
            IReadOnlyList<string> orderedSceneIds,
            string outputRootPath,
            IReadOnlyList<string> targetIds,
            IPlatformAssetBuilder materialBuilder = null,
            string selectedBuildProfileId = "",
            string selectedGraphicsProfileId = "") {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (orderedSceneIds.Count == 0) {
                throw new ArgumentException("At least one ordered scene id must be provided.", nameof(orderedSceneIds));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (targetIds == null) {
                throw new ArgumentNullException(nameof(targetIds));
            }

            string fullOutputRootPath = Path.GetFullPath(outputRootPath);
            string effectiveExecutionRootPath = ResolveCookExecutionRootPath(fullOutputRootPath);
            string effectiveCookRootPath = ResolveCookRootPath(fullOutputRootPath);
            Directory.CreateDirectory(effectiveExecutionRootPath);
            IPlatformAssetBuilder effectiveMaterialBuilder = ResolveEffectiveMaterialBuilder(materialBuilder);

            EditorPlatformBuildScenePackager packager = new(
                ProjectRootPath,
                Importers,
                platformDefinition,
                DefaultFontAsset,
                effectiveMaterialBuilder,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                ScriptTypeResolver);
            List<string> orderedScenePaths = ResolveOrderedScenePaths(orderedSceneIds);
            EditorPlatformBuildScenePackagerResult packagerResult = packager.Package(orderedScenePaths, effectiveExecutionRootPath);
            PlatformCookWorkItem[] platformCookWorkItems = [.. packagerResult.PlatformCookWorkItems];

            PlatformBuildScene[] scenes = BuildSceneEntries(orderedSceneIds, orderedScenePaths, effectiveCookRootPath);
            PlatformBuildArtifact[] cookedArtifacts = BuildCookedArtifacts(
                effectiveCookRootPath,
                targetIds,
                platformCookWorkItems);

            PlatformBuildManifest manifest = new PlatformBuildManifest(
                2,
                ProjectId,
                ProjectVersion,
                RequiredEngineVersion,
                ResolvePlatformName(platformDefinition, materialBuilder),
                ResolvePlatformVersion(materialBuilder),
                orderedSceneIds[0],
                scenes,
                Array.Empty<PlatformBuildAsset>(),
                cookedArtifacts,
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()),
                platformCookWorkItems);
            manifest.StandardPlatformInputConfiguration = ResolveStandardPlatformInputConfiguration(manifest.PlatformName);
            return manifest;
        }

        /// <summary>
        /// Resolves the runtime standard platform input configuration for the supplied platform id from project-shared profile settings.
        /// </summary>
        /// <param name="platformId">Stable platform identifier whose shared input settings should be loaded.</param>
        /// <returns>Runtime standard platform input configuration resolved from project settings.</returns>
        StandardPlatformInputConfiguration ResolveStandardPlatformInputConfiguration(string platformId) {
            EditorProfileSettingsDocument profileSettings = ProfileSettingsService.Load(new[] { platformId });
            for (int index = 0; index < profileSettings.Platforms.Count; index++) {
                EditorPlatformProfileSettingsDocument platformSettings = profileSettings.Platforms[index];
                if (!string.Equals(platformSettings.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return StandardPlatformInputConfigurationFactory.Create(platformSettings);
            }

            return StandardPlatformInputConfiguration.Empty;
        }

        /// <summary>
        /// Returns the builder instance that should own material cooking for the current build.
        /// </summary>
        /// <param name="materialBuilder">Builder loaded for the active platform.</param>
        /// <returns>The builder when it publishes material schemas; otherwise null to keep top-level material packaging active.</returns>
        static IPlatformAssetBuilder ResolveEffectiveMaterialBuilder(IPlatformAssetBuilder materialBuilder) {
            if (materialBuilder == null) {
                return null;
            }

            PlatformDefinition definition = materialBuilder.Definition;
            if (definition == null || definition.MaterialSchemas == null || definition.MaterialSchemas.Length == 0) {
                return null;
            }

            return materialBuilder;
        }

        /// <summary>
        /// Resolves the stable platform identifier that should be stamped into the cooked manifest.
        /// </summary>
        /// <param name="platformDefinition">Resolved platform definition selected for the current build.</param>
        /// <param name="builder">Loaded platform builder used by the build graph.</param>
        /// <returns>Stable platform identifier reported by the selected builder.</returns>
        static string ResolvePlatformName(PlatformDefinition platformDefinition, IPlatformAssetBuilder builder) {
            if (builder?.Descriptor != null && !string.IsNullOrWhiteSpace(builder.Descriptor.TargetPlatformId)) {
                return builder.Descriptor.TargetPlatformId;
            }
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }
            if (string.IsNullOrWhiteSpace(platformDefinition.PlatformId)) {
                throw new InvalidOperationException("Platform definition must declare a platform id.");
            }

            return platformDefinition.PlatformId;
        }

        /// <summary>
        /// Resolves the builder-stamped platform version that should be reported by the running artifact.
        /// </summary>
        /// <param name="builder">Loaded platform builder used by the build graph.</param>
        /// <returns>Builder-stamped runtime platform version string.</returns>
        static string ResolvePlatformVersion(IPlatformAssetBuilder builder) {
            if (builder?.Descriptor == null) {
                throw new InvalidOperationException("Platform builder descriptor is required to stamp runtime platform version metadata.");
            }
            if (string.IsNullOrWhiteSpace(builder.Descriptor.BuilderVersion)) {
                throw new InvalidOperationException("Platform builder descriptor must declare a builder version.");
            }

            return builder.Descriptor.BuilderVersion;
        }

        PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, IReadOnlyList<string> orderedScenePaths, string cookRootPath) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (orderedScenePaths == null) {
                throw new ArgumentNullException(nameof(orderedScenePaths));
            }
            if (orderedSceneIds.Count != orderedScenePaths.Count) {
                throw new InvalidOperationException("Ordered scene ids and authored scene paths must contain the same number of entries.");
            }

            PlatformBuildScene[] scenes = new PlatformBuildScene[orderedSceneIds.Count];
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                string authoredScenePath = orderedScenePaths[index];
                string cookedRelativePath = BuildCookedSceneRelativePath(authoredScenePath, index);
                uint physics3DSceneFeatureFlags = ReadCookedScenePhysics3DFeatureFlags(cookRootPath, cookedRelativePath);
                scenes[index] = new PlatformBuildScene(
                    sceneId,
                    SceneIdUtility.FromPath(authoredScenePath),
                    cookedRelativePath,
                    [
                        new PlatformBuildPayloadReference(cookedRelativePath, cookedRelativePath)
                    ],
                    [
                        new KeyValuePair<string, string>("build-order-index", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, cookedRelativePath),
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags, physics3DSceneFeatureFlags.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    ]);
            }

            return scenes;
        }

        /// <summary>
        /// Resolves the authored project-relative scene paths for the supplied stable scene ids.
        /// </summary>
        /// <param name="orderedSceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Project-relative authored scene paths in build order.</returns>
        List<string> ResolveOrderedScenePaths(IReadOnlyList<string> orderedSceneIds) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            List<string> orderedScenePaths = new List<string>(orderedSceneIds.Count);
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                orderedScenePaths.Add(SceneCatalogService.ResolveScenePath(orderedSceneIds[index]));
            }

            return orderedScenePaths;
        }

        /// <summary>
        /// Reads the compact 3D physics scene feature mask stored in one cooked scene asset.
        /// </summary>
        /// <param name="outputRootPath">Cooked output root path.</param>
        /// <param name="cookedRelativePath">Runtime-relative cooked scene payload path.</param>
        /// <returns>Compact 3D physics scene feature mask embedded in the cooked scene asset.</returns>
        static uint ReadCookedScenePhysics3DFeatureFlags(string cookRootPath, string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(cookRootPath)) {
                throw new ArgumentException("Cook root path must be provided.", nameof(cookRootPath));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            string normalizedCookedRelativePath = NormalizeCookedRelativePath(cookedRelativePath);
            string fullScenePath = Path.Combine(cookRootPath, normalizedCookedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullScenePath;
                using FileStream stream = File.OpenRead(fullScenePath);
                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' did not deserialize into a SceneAsset.");
                }

                return sceneAsset.Physics3DSceneFeatureFlags;
            } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(cookedRelativePath, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' at '{fullScenePath}' could not be read for physics feature discovery.", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        PlatformBuildArtifact[] BuildCookedArtifacts(
            string cookRootPath,
            IReadOnlyList<string> targetIds,
            IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems) {
            if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            }

            string variantId = targetIds.Count == 1 && !string.IsNullOrWhiteSpace(targetIds[0])
                ? targetIds[0]
                : "shared";

            EditorPlatformCookedArtifactPool artifactPool = new(FileHasher);
            string[] cookedFilePaths = Directory.GetFiles(cookRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(cookedFilePaths, StringComparer.OrdinalIgnoreCase);
            HashSet<string> builderOwnedOutputPaths = BuildBuilderOwnedOutputPathSet(platformCookWorkItems);

            for (int index = 0; index < cookedFilePaths.Length; index++) {
                string fullPath = cookedFilePaths[index];
                string relativePath = "cooked/" + NormalizeRelativePath(Path.GetRelativePath(cookRootPath, fullPath));
                if (builderOwnedOutputPaths.Contains(relativePath)) {
                    continue;
                }

                artifactPool.AddFile(fullPath, relativePath, ResolveArtifactKind(fullPath, relativePath), variantId);
            }

            return artifactPool.ToArray();
        }

        /// <summary>
        /// Builds the set of cooked output paths that will be produced later by builder-owned platform cook work items.
        /// </summary>
        /// <param name="platformCookWorkItems">Builder-owned platform cook work items emitted by the editor build graph.</param>
        /// <returns>Normalized runtime-relative output paths owned by the builder.</returns>
        static HashSet<string> BuildBuilderOwnedOutputPathSet(IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems) {
            if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            }

            HashSet<string> builderOwnedOutputPaths = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < platformCookWorkItems.Count; index++) {
                PlatformCookWorkItem workItem = platformCookWorkItems[index];
                if (workItem == null || string.IsNullOrWhiteSpace(workItem.OutputRelativePath)) {
                    continue;
                }

                builderOwnedOutputPaths.Add(workItem.OutputRelativePath.Replace('\\', '/'));
            }

            return builderOwnedOutputPaths;
        }

        static string ResolveCookExecutionRootPath(string outputRootPath) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            if (Path.GetFileName(outputRootPath).Equals("cooked", StringComparison.OrdinalIgnoreCase)) {
                string? parentDirectoryPath = Directory.GetParent(outputRootPath)?.FullName;
                if (string.IsNullOrWhiteSpace(parentDirectoryPath)) {
                    throw new InvalidOperationException($"Cook root '{outputRootPath}' does not have a parent execution root.");
                }

                return parentDirectoryPath;
            }

            return outputRootPath;
        }

        static string ResolveCookRootPath(string outputRootPath) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            if (Path.GetFileName(outputRootPath).Equals("cooked", StringComparison.OrdinalIgnoreCase)) {
                return outputRootPath;
            }

            return Path.Combine(outputRootPath, "cooked");
        }

        static string BuildCookedSceneRelativePath(string sceneId, int sceneIndex) {
            return PackagedScenePathResolver.BuildRelativePath(sceneId, sceneIndex);
        }

        static string NormalizeCookedRelativePath(string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            if (cookedRelativePath.StartsWith("cooked/", StringComparison.OrdinalIgnoreCase)) {
                return cookedRelativePath.Substring("cooked/".Length);
            }

            return cookedRelativePath;
        }

        static string ResolveArtifactKind(string fullPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return "asset";
            }

            if (relativePath.StartsWith("cooked/scenes/", StringComparison.OrdinalIgnoreCase)) {
                return "scene";
            }
            if (relativePath.StartsWith("cooked/fonts/", StringComparison.OrdinalIgnoreCase) || relativePath.Contains("/fonts/", StringComparison.OrdinalIgnoreCase)) {
                return "font";
            }
            if (relativePath.StartsWith("cooked/shaders/", StringComparison.OrdinalIgnoreCase)) {
                return "shader";
            }
            string serializedArtifactKind = TryResolveSerializedArtifactKind(fullPath, relativePath);
            if (!string.IsNullOrWhiteSpace(serializedArtifactKind)) {
                return serializedArtifactKind;
            }
            if (relativePath.Contains("/models/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("cooked/imported/Models/", StringComparison.OrdinalIgnoreCase)) {
                return "model";
            }
            if (relativePath.Contains("/materials/", StringComparison.OrdinalIgnoreCase)) {
                return "material";
            }
            if (relativePath.StartsWith("cooked/imported/", StringComparison.OrdinalIgnoreCase)) {
                return ResolveImportedArtifactKind(fullPath, relativePath);
            }

            return "asset";
        }

        /// <summary>
        /// Resolves one cooked serialized artifact kind directly from the payload when the runtime path points at a generic cooked asset file.
        /// </summary>
        /// <param name="fullPath">Full cooked artifact path on disk.</param>
        /// <param name="relativePath">Runtime-relative cooked artifact path.</param>
        /// <returns>Resolved serialized artifact kind, or an empty string when payload-based classification should not apply.</returns>
        static string TryResolveSerializedArtifactKind(string fullPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }
            if (!relativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase)) {
                return string.Empty;
            }
            if (!File.Exists(fullPath)) {
                return string.Empty;
            }

            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullPath;
                using FileStream stream = File.OpenRead(fullPath);
                if (!UsesGenericEditorAssetSerialization(stream)) {
                    return string.Empty;
                }

                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is ModelAsset) {
                    return "model";
                }
                if (asset is MaterialAsset) {
                    return "material";
                }
                return string.Empty;
            } catch (Exception ex) {
                throw new InvalidOperationException($"Cooked artifact '{relativePath}' at '{fullPath}' could not be classified from serialized content.", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        static string ResolveImportedArtifactKind(string fullPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided for imported artifact classification.", nameof(fullPath));
            }
            if (!File.Exists(fullPath)) {
                throw new InvalidOperationException($"Cooked imported artifact '{relativePath}' was not found at '{fullPath}' during classification.");
            }

            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullPath;
                using FileStream stream = File.OpenRead(fullPath);
                if (!UsesGenericEditorAssetSerialization(stream)) {
                    return "asset";
                }

                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is ModelAsset) {
                    return "model";
                }
                if (asset is MaterialAsset) {
                    return "material";
                }
                return "asset";
            } catch (Exception ex) {
                throw new InvalidOperationException($"Cooked imported artifact '{relativePath}' at '{fullPath}' could not be classified from serialized content.", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Returns whether the supplied cooked asset stream uses the generic HELE editor-asset serializer owned by the main engine repository.
        /// </summary>
        /// <param name="stream">Readable cooked asset stream positioned at the start of the payload.</param>
        /// <returns>True when the payload uses the generic editor-asset serializer; otherwise false.</returns>
        static bool UsesGenericEditorAssetSerialization(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if (!stream.CanSeek) {
                throw new InvalidOperationException("Serialized artifact classification requires a seekable stream.");
            }

            long previousPosition = stream.Position;
            try {
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                return header.FormatId == helengine.files.EditorAssetBinarySerializer.FormatId;
            } finally {
                stream.Position = previousPosition;
            }
        }
    }
}
