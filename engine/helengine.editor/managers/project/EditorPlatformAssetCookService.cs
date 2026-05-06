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

        public EditorPlatformAssetCookService(
            string projectRootPath,
            string requiredEngineVersion,
            string projectId,
            string projectVersion,
            IReadOnlyList<IAssetImporterRegistration> importers,
            FontAsset defaultFontAsset,
            AssetFileHasher fileHasher = null) {
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath)
                ? throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath))
                : Path.GetFullPath(projectRootPath);
            RequiredEngineVersion = requiredEngineVersion ?? throw new ArgumentNullException(nameof(requiredEngineVersion));
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            ProjectVersion = projectVersion ?? throw new ArgumentNullException(nameof(projectVersion));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            DefaultFontAsset = defaultFontAsset;
            FileHasher = fileHasher ?? new AssetFileHasher();
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
                selectedGraphicsProfileId);
            packager.Package(orderedSceneIds, fullOutputRootPath);

            PlatformBuildScene[] scenes = BuildSceneEntries(orderedSceneIds, fullOutputRootPath);
            PlatformBuildArtifact[] cookedArtifacts = BuildCookedArtifacts(fullOutputRootPath, targetIds);

            return new PlatformBuildManifest(
                2,
                ProjectId,
                ProjectVersion,
                RequiredEngineVersion,
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
        /// <returns>The builder when it publishes material schemas; otherwise null to keep compatibility material packaging active.</returns>
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

        PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, string outputRootPath) {
            PlatformBuildScene[] scenes = new PlatformBuildScene[orderedSceneIds.Count];
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                string cookedRelativePath = BuildCookedSceneRelativePath(sceneId, index);
                uint physics3DSceneFeatureFlags = ReadCookedScenePhysics3DFeatureFlags(outputRootPath, cookedRelativePath);
                scenes[index] = new PlatformBuildScene(
                    sceneId,
                    Path.GetFileNameWithoutExtension(sceneId),
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
            using FileStream stream = File.OpenRead(fullScenePath);
            Asset asset = AssetSerializer.Deserialize(stream);
            if (asset is not SceneAsset sceneAsset) {
                throw new InvalidOperationException($"Cooked scene '{cookedRelativePath}' did not deserialize into a SceneAsset.");
            }

            return sceneAsset.Physics3DSceneFeatureFlags;
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
                artifactPool.AddFile(fullPath, relativePath, ResolveArtifactKind(relativePath), variantId);
            }

            return artifactPool.ToArray();
        }

        static string BuildCookedSceneRelativePath(string sceneId, int sceneIndex) {
            if (sceneIndex == 0) {
                return EditorPlatformBuildScenePackager.MainSceneRelativePath;
            }

            string normalizedSceneId = NormalizeRelativePath(sceneId);
            string changedExtensionPath = Path.ChangeExtension(normalizedSceneId, ".hasset");
            return NormalizeRelativePath(Path.Combine("scenes", changedExtensionPath));
        }

        static string ResolveArtifactKind(string relativePath) {
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
            if (relativePath.Contains("/models/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("cooked/imported/Models/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("cooked/imported/", StringComparison.OrdinalIgnoreCase)) {
                return "model";
            }
            if (relativePath.Contains("/materials/", StringComparison.OrdinalIgnoreCase)) {
                return "material";
            }

            return "asset";
        }

        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }
    }
}
