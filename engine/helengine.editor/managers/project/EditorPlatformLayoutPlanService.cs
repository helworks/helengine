using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Computes the first-stage physical layout plan for cooked artifacts.
    /// </summary>
    internal sealed class EditorPlatformLayoutPlanService {
        /// <summary>
        /// Plans one container layout for the supplied cooked manifest.
        /// </summary>
        public PlatformBuildManifest Plan(
            PlatformBuildManifest cookedManifest,
            PlatformStorageProfileDefinition storageProfile,
            PlatformMediaProfileDefinition mediaProfile) {
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            string runtimeSpecializationId = storageProfile?.RuntimeSpecializationId ?? string.Empty;
            string containerKind = mediaProfile?.LayoutKind == PlatformMediaLayoutKind.DiscImage
                ? "disc-image"
                : "install-tree";

            PlatformBuildArtifact[] cookedArtifacts = cookedManifest.CookedArtifacts ?? Array.Empty<PlatformBuildArtifact>();
            Dictionary<string, int> scenePriorityByRelativePath = BuildScenePriorityByRelativePath(cookedManifest);
            string startupSceneRelativePath = ResolveStartupSceneRelativePath(cookedManifest);
            List<PlatformArtifactPlacement> placements = [];
            for (int index = 0; index < cookedArtifacts.Length; index++) {
                PlatformBuildArtifact artifact = cookedArtifacts[index];
                int placementPriority = ResolvePlacementPriority(
                    artifact,
                    index,
                    startupSceneRelativePath,
                    scenePriorityByRelativePath);

                placements.Add(new PlatformArtifactPlacement(
                    artifact.LogicalArtifactId,
                    artifact.VariantId,
                    "container-0",
                    0,
                    0,
                    0,
                    placementPriority));
            }

            placements.Sort((left, right) => {
                int priorityComparison = left.PlacementPriority.CompareTo(right.PlacementPriority);
                if (priorityComparison != 0) {
                    return priorityComparison;
                }

                int logicalComparison = string.Compare(left.LogicalArtifactId, right.LogicalArtifactId, StringComparison.OrdinalIgnoreCase);
                if (logicalComparison != 0) {
                    return logicalComparison;
                }

                return string.Compare(left.VariantId, right.VariantId, StringComparison.OrdinalIgnoreCase);
            });

            PlatformContainerWritePlan containerWritePlan = new(
                runtimeSpecializationId,
                [
                    new PlatformContainerArtifact("container-0", containerKind, 0)
                ]);

            PlatformBuildManifest manifest = new PlatformBuildManifest(
                cookedManifest.ManifestVersion,
                cookedManifest.ProjectId,
                cookedManifest.ProjectVersion,
                cookedManifest.RequiredEngineVersion,
                cookedManifest.PlatformName,
                cookedManifest.PlatformVersion,
                cookedManifest.StartupSceneId,
                cookedManifest.Scenes,
                cookedManifest.LooseAssets,
                cookedArtifacts,
                cookedManifest.CodeModules,
                [.. placements],
                containerWritePlan,
                cookedManifest.PlatformCookWorkItems);
            manifest.StandardPlatformInputConfiguration = cookedManifest.StandardPlatformInputConfiguration;
            return manifest;
        }

        static Dictionary<string, int> BuildScenePriorityByRelativePath(PlatformBuildManifest cookedManifest) {
            Dictionary<string, int> scenePriorityByRelativePath = new(StringComparer.OrdinalIgnoreCase);
            if (cookedManifest?.Scenes == null) {
                return scenePriorityByRelativePath;
            }

            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                string cookedRelativePath = ResolveSceneCookedRelativePath(scene);
                if (!string.IsNullOrWhiteSpace(cookedRelativePath) && !scenePriorityByRelativePath.ContainsKey(cookedRelativePath)) {
                    scenePriorityByRelativePath.Add(cookedRelativePath, index);
                }
            }

            return scenePriorityByRelativePath;
        }

        static int ResolvePlacementPriority(
            PlatformBuildArtifact artifact,
            int fallbackPriority,
            string startupSceneRelativePath,
            IReadOnlyDictionary<string, int> scenePriorityByRelativePath) {
            if (artifact == null) {
                return fallbackPriority;
            }

            if (!string.IsNullOrWhiteSpace(startupSceneRelativePath)
                && string.Equals(artifact.RelativePath, startupSceneRelativePath, StringComparison.OrdinalIgnoreCase)) {
                return 0;
            }

            if (scenePriorityByRelativePath.TryGetValue(artifact.RelativePath, out int scenePriority)) {
                return 1 + scenePriority;
            }

            return 10_000 + fallbackPriority;
        }

        static string ResolveStartupSceneRelativePath(PlatformBuildManifest cookedManifest) {
            if (cookedManifest?.Scenes == null || string.IsNullOrWhiteSpace(cookedManifest.StartupSceneId)) {
                return string.Empty;
            }

            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                if (string.Equals(scene.SceneId, cookedManifest.StartupSceneId, StringComparison.OrdinalIgnoreCase)) {
                    return ResolveSceneCookedRelativePath(scene);
                }
            }

            return string.Empty;
        }

        static string ResolveSceneCookedRelativePath(PlatformBuildScene scene) {
            if (scene?.ResolvedMetadata != null) {
                for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
                    KeyValuePair<string, string> entry = scene.ResolvedMetadata[index];
                    if (string.Equals(entry.Key, "cooked-relative-path", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(entry.Value)) {
                        return entry.Value.Replace('\\', '/');
                    }
                }
            }

            return string.Empty;
        }
    }
}
