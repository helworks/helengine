using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Targets;

namespace helengine.baseplatform.Requests;

/// <summary>
/// Describes a single asset-platform build invocation using a fully resolved manifest, target variants, and filesystem roots.
/// </summary>
public class PlatformBuildRequest {
    /// <summary>
    /// Initializes a new build request for one target platform output.
    /// </summary>
    /// <param name="manifest">The fully resolved manifest the builder must transform.</param>
    /// <param name="targetVariants">The requested runtime target variants that share the build request.</param>
    /// <param name="cookProfiles">The shared cook profiles referenced by the requested target variants.</param>
    /// <param name="outputRoot">The root directory where cooked outputs should be written.</param>
    /// <param name="workingRoot">The temporary working directory the builder may use during execution.</param>
    /// <param name="selectedBuildProfileId">The builder-selected build profile identifier.</param>
    /// <param name="selectedGraphicsProfileId">The builder-selected graphics profile identifier.</param>
    /// <param name="selectedBuildOptionValues">The selected build-profile option values.</param>
    /// <param name="selectedGraphicsOptionValues">The selected graphics-profile option values.</param>
    /// <exception cref="ArgumentNullException">Thrown when the manifest, target variants, cook profiles, or filesystem roots are missing.</exception>
    /// <exception cref="ArgumentException">Thrown when any required string value is missing or a referenced cook profile is unavailable.</exception>
    public PlatformBuildRequest(
        PlatformBuildManifest manifest,
        PlatformBuildTargetVariant[] targetVariants,
        PlatformCookProfile[] cookProfiles,
        string outputRoot,
        string workingRoot)
        : this(manifest, targetVariants, cookProfiles, outputRoot, workingRoot, string.Empty, string.Empty, null, null) {
    }

    /// <summary>
    /// Initializes a new build request for one target platform output.
    /// </summary>
    /// <param name="manifest">The fully resolved manifest the builder must transform.</param>
    /// <param name="targetVariants">The requested runtime target variants that share the build request.</param>
    /// <param name="cookProfiles">The shared cook profiles referenced by the requested target variants.</param>
    /// <param name="outputRoot">The root directory where cooked outputs should be written.</param>
    /// <param name="workingRoot">The temporary working directory the builder may use during execution.</param>
    /// <param name="selectedBuildProfileId">The builder-selected build profile identifier.</param>
    /// <param name="selectedGraphicsProfileId">The builder-selected graphics profile identifier.</param>
    /// <param name="selectedBuildOptionValues">The selected build-profile option values.</param>
    /// <param name="selectedGraphicsOptionValues">The selected graphics-profile option values.</param>
    /// <exception cref="ArgumentNullException">Thrown when the manifest, target variants, cook profiles, or filesystem roots are missing.</exception>
    /// <exception cref="ArgumentException">Thrown when any required string value is missing or a referenced cook profile is unavailable.</exception>
    public PlatformBuildRequest(
        PlatformBuildManifest manifest,
        PlatformBuildTargetVariant[] targetVariants,
        PlatformCookProfile[] cookProfiles,
        string outputRoot,
        string workingRoot,
        string selectedBuildProfileId,
        string selectedGraphicsProfileId,
        IReadOnlyDictionary<string, string> selectedBuildOptionValues,
        IReadOnlyDictionary<string, string> selectedGraphicsOptionValues) {
        if (manifest == null) {
            throw new ArgumentNullException(nameof(manifest));
        } else if (targetVariants == null) {
            throw new ArgumentNullException(nameof(targetVariants), "Target variants are required.");
        } else if (targetVariants.Length == 0) {
            throw new ArgumentException("At least one target variant is required.", nameof(targetVariants));
        } else if (Array.Exists(targetVariants, targetVariant => targetVariant == null)) {
            throw new ArgumentException("Target variants cannot contain null entries.", nameof(targetVariants));
        } else if (cookProfiles == null) {
            throw new ArgumentNullException(nameof(cookProfiles), "Cook profiles are required.");
        } else if (cookProfiles.Length == 0) {
            throw new ArgumentException("At least one cook profile is required.", nameof(cookProfiles));
        } else if (Array.Exists(cookProfiles, cookProfile => cookProfile == null)) {
            throw new ArgumentException("Cook profiles cannot contain null entries.", nameof(cookProfiles));
        } else if (string.IsNullOrWhiteSpace(outputRoot)) {
            throw new ArgumentException("Output root is required.", nameof(outputRoot));
        } else if (string.IsNullOrWhiteSpace(workingRoot)) {
            throw new ArgumentException("Working root is required.", nameof(workingRoot));
        }

        HashSet<string> cookProfileIds = new(StringComparer.Ordinal);
        for (int index = 0; index < cookProfiles.Length; index++) {
            PlatformCookProfile cookProfile = cookProfiles[index];
            if (!cookProfileIds.Add(cookProfile.CookProfileId)) {
                throw new ArgumentException($"Duplicate cook profile id '{cookProfile.CookProfileId}' is not allowed.", nameof(cookProfiles));
            }
        }

        HashSet<string> targetVariantIds = new(StringComparer.Ordinal);
        for (int index = 0; index < targetVariants.Length; index++) {
            PlatformBuildTargetVariant targetVariant = targetVariants[index];
            if (!targetVariantIds.Add(targetVariant.TargetVariantId)) {
                throw new ArgumentException($"Duplicate target variant id '{targetVariant.TargetVariantId}' is not allowed.", nameof(targetVariants));
            }

            if (!cookProfileIds.Contains(targetVariant.CookProfileId)) {
                throw new ArgumentException(
                    $"Target variant '{targetVariant.TargetVariantId}' references missing cook profile '{targetVariant.CookProfileId}'.",
                    nameof(targetVariants));
            }
        }

        Manifest = manifest;
        TargetVariants = [.. targetVariants];
        CookProfiles = [.. cookProfiles];
        OutputRoot = outputRoot;
        WorkingRoot = workingRoot;
        SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
        SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;
        SelectedBuildOptionValues = selectedBuildOptionValues != null
            ? new Dictionary<string, string>(selectedBuildOptionValues)
            : [];
        SelectedGraphicsOptionValues = selectedGraphicsOptionValues != null
            ? new Dictionary<string, string>(selectedGraphicsOptionValues)
            : [];
    }

    /// <summary>
    /// Gets the fully resolved platform build manifest that the builder must process.
    /// </summary>
    public PlatformBuildManifest Manifest { get; }

    /// <summary>
    /// Gets the runtime target variants requested for this build.
    /// </summary>
    public PlatformBuildTargetVariant[] TargetVariants { get; }

    /// <summary>
    /// Gets the cook profiles referenced by the requested target variants.
    /// </summary>
    public PlatformCookProfile[] CookProfiles { get; }

    /// <summary>
    /// Gets the root directory where final cooked outputs should be written.
    /// </summary>
    public string OutputRoot { get; }

    /// <summary>
    /// Gets the temporary working directory the builder may use during execution.
    /// </summary>
    public string WorkingRoot { get; }

    /// <summary>
    /// Gets the selected builder-provided build profile identifier.
    /// </summary>
    public string SelectedBuildProfileId { get; }

    /// <summary>
    /// Gets the selected builder-provided graphics profile identifier.
    /// </summary>
    public string SelectedGraphicsProfileId { get; }

    /// <summary>
    /// Gets the selected builder-provided build option values.
    /// </summary>
    public IReadOnlyDictionary<string, string> SelectedBuildOptionValues { get; }

    /// <summary>
    /// Gets the selected builder-provided graphics option values.
    /// </summary>
    public IReadOnlyDictionary<string, string> SelectedGraphicsOptionValues { get; }
}
