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

            EditorPlatformBuildScenePackager packager = new(
                ProjectRootPath,
                Importers,
                platformDefinition,
                DefaultFontAsset,
                materialBuilder,
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

        PlatformBuildScene[] BuildSceneEntries(IReadOnlyList<string> orderedSceneIds, string outputRootPath) {
            PlatformBuildScene[] scenes = new PlatformBuildScene[orderedSceneIds.Count];
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                string cookedRelativePath = BuildCookedSceneRelativePath(sceneId, index);
                scenes[index] = new PlatformBuildScene(
                    sceneId,
                    Path.GetFileNameWithoutExtension(sceneId),
                    cookedRelativePath,
                    [
                        new PlatformBuildPayloadReference(cookedRelativePath, cookedRelativePath)
                    ],
                    [
                        new KeyValuePair<string, string>("build-order-index", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>("cooked-relative-path", cookedRelativePath)
                    ]);
            }

            return scenes;
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
            return NormalizeRelativePath(Path.Combine("cooked", "scenes", changedExtensionPath));
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
