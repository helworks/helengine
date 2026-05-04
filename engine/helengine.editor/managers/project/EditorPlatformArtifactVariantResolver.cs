using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Represents the resolved shared and platform-specific artifact view for one cooked asset set.
    /// </summary>
    internal sealed class EditorResolvedArtifactSet {
        public EditorResolvedArtifactSet(PlatformBuildArtifact[] sharedArtifacts, PlatformBuildArtifact[] platformVariants) {
            SharedArtifacts = sharedArtifacts ?? [];
            PlatformVariants = platformVariants ?? [];
        }

        public PlatformBuildArtifact[] SharedArtifacts { get; }
        public PlatformBuildArtifact[] PlatformVariants { get; }
    }

    /// <summary>
    /// Resolves identical cooked artifacts into shared entries and preserves differing outputs as platform variants.
    /// </summary>
    internal sealed class EditorPlatformArtifactVariantResolver {
        public EditorResolvedArtifactSet Resolve(IReadOnlyList<PlatformBuildArtifact> artifacts) {
            if (artifacts == null) {
                throw new ArgumentNullException(nameof(artifacts));
            }

            Dictionary<string, List<PlatformBuildArtifact>> artifactsByIdentity = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < artifacts.Count; index++) {
                PlatformBuildArtifact artifact = artifacts[index];
                if (artifact == null) {
                    continue;
                }

                string identityKey = BuildIdentityKey(artifact);
                if (!artifactsByIdentity.TryGetValue(identityKey, out List<PlatformBuildArtifact>? bucket)) {
                    bucket = [];
                    artifactsByIdentity.Add(identityKey, bucket);
                }

                bucket.Add(artifact);
            }

            List<PlatformBuildArtifact> sharedArtifacts = [];
            List<PlatformBuildArtifact> platformVariants = [];

            foreach (KeyValuePair<string, List<PlatformBuildArtifact>> identityGroup in artifactsByIdentity) {
                List<PlatformBuildArtifact> groupedArtifacts = identityGroup.Value;
                HashSet<string> variantIds = new(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < groupedArtifacts.Count; index++) {
                    variantIds.Add(groupedArtifacts[index].VariantId);
                }

                if (variantIds.Count <= 1) {
                    platformVariants.AddRange(groupedArtifacts);
                    continue;
                }

                PlatformBuildArtifact representativeArtifact = groupedArtifacts[0];
                sharedArtifacts.Add(new PlatformBuildArtifact(
                    representativeArtifact.RelativePath,
                    representativeArtifact.LogicalArtifactId,
                    representativeArtifact.ContentHash,
                    representativeArtifact.ArtifactKind,
                    "shared"));
            }

            return new EditorResolvedArtifactSet([.. sharedArtifacts], [.. platformVariants]);
        }

        static string BuildIdentityKey(PlatformBuildArtifact artifact) {
            return string.Join("|", artifact.LogicalArtifactId, artifact.ArtifactKind, artifact.ContentHash);
        }
    }
}
