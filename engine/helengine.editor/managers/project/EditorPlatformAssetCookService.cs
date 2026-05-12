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
            Directory.CreateDirectory(fullOutputRootPath);
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
            packager.Package(orderedScenePaths, fullOutputRootPath);

            PlatformBuildScene[] scenes = BuildSceneEntries(orderedSceneIds, orderedScenePaths, fullOutputRootPath);
            PlatformBuildArtifact[] cookedArtifacts = BuildCookedArtifacts(fullOutputRootPath, targetIds);

            return new PlatformBuildManifest(
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
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));
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

        PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, IReadOnlyList<string> orderedScenePaths, string outputRootPath) {
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
                uint physics3DSceneFeatureFlags = ReadCookedScenePhysics3DFeatureFlags(outputRootPath, cookedRelativePath);
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
        static uint ReadCookedScenePhysics3DFeatureFlags(string outputRootPath, string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            string fullScenePath = Path.Combine(outputRootPath, cookedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            try {
                using FileStream stream = File.OpenRead(fullScenePath);
                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' did not deserialize into a SceneAsset.");
                }

                return sceneAsset.Physics3DSceneFeatureFlags;
            } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(cookedRelativePath, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' at '{fullScenePath}' could not be read for physics feature discovery.", ex);
            }
        }

        PlatformBuildArtifact[] BuildCookedArtifacts(string outputRootPath, IReadOnlyList<string> targetIds) {
            string variantId = targetIds.Count == 1 && !string.IsNullOrWhiteSpace(targetIds[0])
                ? targetIds[0]
                : "shared";

            EditorPlatformCookedArtifactPool artifactPool = new(FileHasher);
            string[] cookedFilePaths = Directory.GetFiles(outputRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(cookedFilePaths, StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < cookedFilePaths.Length; index++) {
                string fullPath = cookedFilePaths[index];
                string relativePath = NormalizeRelativePath(Path.GetRelativePath(outputRootPath, fullPath));
                artifactPool.AddFile(fullPath, relativePath, ResolveArtifactKind(fullPath, relativePath), variantId);
            }

            return artifactPool.ToArray();
        }

        static string BuildCookedSceneRelativePath(string sceneId, int sceneIndex) {
            return PackagedScenePathResolver.BuildRelativePath(sceneId, sceneIndex);
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

        static string ResolveImportedArtifactKind(string fullPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided for imported artifact classification.", nameof(fullPath));
            }
            if (!File.Exists(fullPath)) {
                throw new InvalidOperationException($"Cooked imported artifact '{relativePath}' was not found at '{fullPath}' during classification.");
            }

            try {
                using FileStream stream = File.OpenRead(fullPath);
                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is ModelAsset) {
                    return "model";
                }
                if (asset is MaterialAsset || asset is Ps2MaterialAsset) {
                    return "material";
                }
                return "asset";
            } catch (Exception ex) {
                throw new InvalidOperationException($"Cooked imported artifact '{relativePath}' at '{fullPath}' could not be classified from serialized content.", ex);
            }
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }
    }
}
