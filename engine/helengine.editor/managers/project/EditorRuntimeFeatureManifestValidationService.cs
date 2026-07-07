using helengine.baseplatform.Manifest;
using System.Text;

namespace helengine.editor;

/// <summary>
/// Validates one runtime feature manifest against user-selected disabled runtime features before packaging continues.
/// </summary>
public sealed class EditorRuntimeFeatureManifestValidationService {
    /// <summary>
    /// Validates that no required runtime feature has been disabled by codegen settings.
    /// </summary>
    /// <param name="manifest">Resolved runtime feature manifest for the current build.</param>
    /// <param name="disabledFeatureIds">Normalized disabled runtime feature identifiers requested by the user.</param>
    /// <exception cref="ArgumentNullException">Thrown when the manifest or disabled-feature collection is missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown when one disabled feature is still required by the build.</exception>
    public void Validate(
        PlatformBuildRuntimeFeatureManifest manifest,
        IReadOnlyList<string> disabledFeatureIds) {
        if (manifest == null) {
            throw new ArgumentNullException(nameof(manifest));
        } else if (disabledFeatureIds == null) {
            throw new ArgumentNullException(nameof(disabledFeatureIds));
        }

        if (manifest.RequiredFeatures.Length == 0 || disabledFeatureIds.Count == 0) {
            return;
        }

        HashSet<string> disabledFeatures = new(disabledFeatureIds, StringComparer.OrdinalIgnoreCase);
        List<PlatformBuildRequiredRuntimeFeature> conflicts = [];
        for (int index = 0; index < manifest.RequiredFeatures.Length; index++) {
            PlatformBuildRequiredRuntimeFeature requirement = manifest.RequiredFeatures[index];
            if (disabledFeatures.Contains(requirement.FeatureId)) {
                conflicts.Add(requirement);
            }
        }

        if (conflicts.Count == 0) {
            return;
        }

        StringBuilder messageBuilder = new("Build disabled one or more runtime features that are still required:");
        for (int index = 0; index < conflicts.Count; index++) {
            PlatformBuildRequiredRuntimeFeature conflict = conflicts[index];
            messageBuilder.Append(" ");
            messageBuilder.Append(conflict.FeatureId);
            messageBuilder.Append(" required by ");
            messageBuilder.Append(conflict.SourceKind);
            messageBuilder.Append(" '");
            messageBuilder.Append(conflict.SourceId);
            messageBuilder.Append("' (");
            messageBuilder.Append(conflict.Reason);
            messageBuilder.Append(").");
        }

        throw new InvalidOperationException(messageBuilder.ToString());
    }
}
